using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Events;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Timing;

namespace ShikshaMitra.Finance.Fees.Collections;

[RemoteService(false)]
public partial class CollectFeeWorkflow(
	IReadOnlyRepository<Student, Guid> studentRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
	IRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<Charge, Guid> chargeRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	LateFeeWaiverLookupService waiverLookupService,
	CollectionPolicyResolver policyResolver,
	LateFeeCalculationService lateFeeCalculationService,
	ExceptionWindowResolver exceptionWindowResolver,
	FeeCollectionManager feeCollectionManager,
	FinanceAuditWriter financeAuditWriter,
	FinanceScopeAccessService financeScopeAccessService,
	IFeeReceiptPdfGenerator pdfGenerator,
	FeeReceiptBlobManager blobManager,
	IDistributedEventBus eventBus,
	IClock clock
) : ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)]
	public async Task<CollectFeeOutput> CollectFeeAsync(CollectFeeInput input)
	{
		var (student, academicYear) = await ValidateAndLoadStudentForCollectionAsync(input);
		var dueFeeContext = await LoadAndValidateDueFeesContextAsync(input);
		var chargeContext = await LoadAndValidateChargesContextAsync(input);
		await ValidateLateFeePaymentsAsync(input);

		var paymentMethods = input.Payments.Select(p => p.PaymentMethod).ToList();
		var received = input.DueFeePayments.Sum(dfp => dfp.Amount) + input.ChargePayments.Sum(cp => cp.Amount)
			+ input.LateFeePayments.Sum(lfp => lfp.Amount);
		var status = DetermineCollectionStatus(
			input.DueFeePayments, dueFeeContext.DueFeeMap,
			input.ChargePayments, chargeContext.PendingChargeMap,
			paymentMethods, input.PaymentConfirmed ?? true);

		var dueFeeBeforeSnapshots = BuildDueFeeBeforeSnapshots(input.DueFeePayments, dueFeeContext.DueFeeMap);

		var feeHeadGroups = input.DueFeePayments
			.GroupBy(dfp => dfp.FeeHeadId)
			.Select(g => (FeeHeadId: g.Key, Amount: g.Sum(dfp => dfp.Amount))).ToList();
		var allAllocations = input.DueFeePayments.Select(dfp => (dfp.DueFeeId, dfp.Amount)).ToList();
		var paymentRows = input.Payments
			.Select(p => (p.PaymentMethod, p.Amount, FeeCollectionManager.MapPaymentDetails(p.PaymentMethod, p.Details)))
			.ToList();

		var transaction = await feeCollectionManager.ExecuteCollectionTransactionAsync(
			new FeeCollectionManager.CollectFeeTransactionCommand(
				StudentId: input.StudentId, AcademicYearId: input.AcademicYearId,
				BoardId: student.AcademicBoardId ?? Guid.Empty, EducationMediumId: student.AcademicMediumId,
				Status: status, ReceivedAmount: received, Notes: input.Notes,
				BankReceiptConfirmed: input.PaymentConfirmed, FeeHeadGroups: feeHeadGroups,
				ChargePayments: input.ChargePayments.Select(cp => (cp.ChargeId, cp.Amount)).ToList(),
				DueFeeAllocations: allAllocations, Payments: paymentRows,
				LateFeeAllocations: input.LateFeePayments.Select(lfp => (lfp.DueFeeId, lfp.Amount)).ToList()));

		await GenerateAndStoreReceiptPdfsAsync(transaction.Receipts, student, academicYear);

		var (auditEventIds, paymentChangedFields, allocationLines) =
			await WriteAuditTrailAsync(input, received, status, dueFeeContext, dueFeeBeforeSnapshots,
				paymentMethods, transaction, student);

		await CurrentUnitOfWork!.SaveChangesAsync();

		await eventBus.PublishAsync(new FeeCollectedEto
		{
			FeeCollectionTransactionId = transaction.Metadata.Id,
			StudentId      = input.StudentId,
			AcademicYearId = input.AcademicYearId,
			TotalPaidFee   = received,
			Status         = status,
			PaymentMethods = paymentMethods,
			TenantId       = CurrentTenant.Id,
			CollectedAt    = clock.Now,
		});

		Logger.LogInformation(
			"Fee collected for student {StudentId}: receipt {ReceiptNumber}, amount {Amount}, receipts created {ReceiptCount}",
			input.StudentId, transaction.ReceiptNumber, received, transaction.Receipts.Count);

		var feeReceipt = transaction.Receipts.FirstOrDefault(r => r.ReceiptScope == FeeReceiptScope.Fee);
		var chargeReceipt = transaction.Receipts.FirstOrDefault(r => r.ReceiptScope == FeeReceiptScope.Charge);

		return new CollectFeeOutput
		{
			Id = transaction.Metadata.Id, ReceiptNumber = transaction.ReceiptNumber,
			FeeReceiptNumber = feeReceipt?.ReceiptNumber, FeeReceiptId = feeReceipt?.Id,
			ChargeReceiptId = chargeReceipt?.Id, ChargeReceiptNumber = chargeReceipt?.ReceiptNumber,
			StudentId = input.StudentId, AcademicYearId = input.AcademicYearId,
			TotalAmount = received, ReceivedAmount = received, Status = status,
			CollectedBy = CurrentUser.Id ?? Guid.Empty, CollectedAt = clock.Now, CreatedAt = clock.Now,
			ReceiptUrl = null,
			PaidDueFees = input.DueFeePayments.Select(dfp => new PaidDueFeeInfoDto
			{
				DueFeeId = dfp.DueFeeId, FeeHeadId = dfp.FeeHeadId, AmountPaid = dfp.Amount,
			}).ToList(),
			PaidLateFees = input.LateFeePayments.Select(lfp => new PaidDueFeeInfoDto
			{
				DueFeeId = lfp.DueFeeId,
				FeeHeadId = dueFeeContext.DueFeeMap.GetValueOrDefault(lfp.DueFeeId)?.FeeHeadId ?? Guid.Empty,
				AmountPaid = lfp.Amount,
			}).ToList(),
			PaidFeeHeads = transaction.FeeCollectionRecords.Select(r => new PaidFeeHeadInfoDto
			{
				FeeHeadId = r.FeeHeadId,
				FeeHeadName = dueFeeContext.FeeHeadMap[r.FeeHeadId].Name,
				AmountPaid = r.Amount,
			}).ToList(),
			PaidCharges = transaction.ChargeCollectionRecords.Count > 0
				? transaction.ChargeCollectionRecords.Select(r => new PaidChargeInfoDto
				{
					ChargeId = r.ChargeId,
					ChargeName = string.IsNullOrWhiteSpace(chargeContext.ChargeMap[r.ChargeId].ChargeName)
						? chargeContext.ChargeMap[r.ChargeId].PurposeDescription
						: chargeContext.ChargeMap[r.ChargeId].ChargeName,
					AmountPaid = r.Amount,
				}).ToList() : null,
			CorrelationId = $"fc-{transaction.Metadata.Id:N}", AuditEventIds = auditEventIds,
			AuditSummary = new FinanceAuditSummaryDto
			{
				Headline = status == FeeCollectionStatus.InReconciliation
					? "Payment saved as pending verification" : "Fee collected successfully",
				TotalAmount = received, ChangedFields = paymentChangedFields, AllocationLines = allocationLines,
			},
		};
	}

	private sealed record DueFeeValidationContext(
		Dictionary<Guid, DueFee> DueFeeMap, Dictionary<Guid, FeeHead> FeeHeadMap);

	private sealed record ChargeValidationContext(
		Dictionary<Guid, Charge> ChargeMap, Dictionary<Guid, double> PendingChargeMap);
}
