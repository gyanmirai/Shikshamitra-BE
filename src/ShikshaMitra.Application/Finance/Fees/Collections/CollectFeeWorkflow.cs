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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Data;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Fees.Collections;

[RemoteService(false)]
public partial class CollectFeeWorkflow(
	IReadOnlyRepository<Student, Guid> studentRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
	IRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository,
	IRepository<ChargeWaiverApplication, Guid> chargeWaiverRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<Charge, Guid> chargeRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	ShikshaMitra.Domain.Finance.Accounts.Settings.FinanceRoutingManager routingManager,
 ShikshaMitra.Finance.Accounts.AutomaticTransactionManager automaticTransactions,
 LateFeeBalanceService lateFeeBalanceService,
	CollectionPolicyResolver policyResolver,
	FeeCollectionManager feeCollectionManager,
	FinanceAuditWriter financeAuditWriter,
	FinanceScopeAccessService financeScopeAccessService,
	IFeeReceiptPdfGenerator pdfGenerator,
	FeeReceiptBlobManager blobManager,
	IDistributedEventBus eventBus,
	IClock clock,
	IUnitOfWorkManager unitOfWorkManager
) : ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)]
	public async Task<CollectFeeOutput> CollectFeeAsync(CollectFeeInput input)
	{
		using var serializableUow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
			{ IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var admissionEntry = input.FinanceFeeAccountId.HasValue;
		var commandFingerprint = ComputeCommandFingerprint(input);
		if (!admissionEntry && input.StudentId.HasValue)
		{
			// Finance's existing Student collection entry joins the same ledger after
			// activation. Preserve its original retry fingerprint for legacy clients.
			var assignment = await studentFeeStructureRepository.FindAsync(a => a.StudentId == input.StudentId.Value && a.AcademicYearId == input.AcademicYearId);
			var resolvedAccountId = assignment?.FinanceFeeAccountId ??
				(await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>().FindAsync(a =>
					a.StudentId == input.StudentId.Value && a.AcademicYearId == input.AcademicYearId && a.AssignmentSnapshotJson != null))?.Id;
			if (resolvedAccountId.HasValue)
			{
				// Keep the caller's command unchanged, so an in-process retry uses the
				// same legacy fingerprint as a fresh HTTP request.
				input = new CollectFeeInput
				{
					IdempotencyKey = input.IdempotencyKey, StudentId = input.StudentId,
					FinanceFeeAccountId = resolvedAccountId, AcademicYearId = input.AcademicYearId,
					Notes = input.Notes, PaymentConfirmed = input.PaymentConfirmed,
					DueFeePayments = input.DueFeePayments, ChargePayments = input.ChargePayments,
					LateFeePayments = input.LateFeePayments, Payments = input.Payments
				};
			}
		}
		var account = input.FinanceFeeAccountId.HasValue
			? await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>().GetAsync(input.FinanceFeeAccountId.Value) : null;
		if (account != null)
		{
			await financeScopeAccessService.EnsureFeeAccountAccessibleAsync(account);
		}
		var idempotencyKey = input.IdempotencyKey.Trim();
		var replay = await feeRecordMetadataRepository.FirstOrDefaultAsync(x =>
			x.TenantId == CurrentTenant.Id && x.IdempotencyKey == idempotencyKey);
		if (replay != null)
		{
			if ((admissionEntry || replay.FinanceFeeAccountId.HasValue) && replay.FinanceFeeAccountId != input.FinanceFeeAccountId)
				throw new BusinessException(AdmissionFeeCollectionErrorCodes.RetryConflict);
			if (replay.FinanceFeeAccountId.HasValue) await financeScopeAccessService.EnsureFeeAccountAccessibleAsync(account!);
			else await financeScopeAccessService.EnsureStudentAccessibleAsync(replay.StudentId!.Value);
			if (!string.Equals(replay.CommandFingerprint, commandFingerprint, StringComparison.Ordinal))
				throw new UserFriendlyException("This retry key was already used for a different fee collection request.",
					code: FeeCollectionErrorCodes.IdempotencyConflict);

			if (string.IsNullOrWhiteSpace(replay.CommandResultJson))
				throw new UserFriendlyException("This fee collection request is still being processed. Please retry shortly.");

			var replayOutput = JsonSerializer.Deserialize<CollectFeeOutput>(replay.CommandResultJson)
				?? throw new UserFriendlyException("The saved fee collection result could not be read.");
			await serializableUow.CompleteAsync();
			return replayOutput;
		}

		if (account != null && (admissionEntry || !account.StudentFeeStructureId.HasValue))
		{
			var source = await LazyServiceProvider.LazyGetRequiredService<IAdmissionFinanceContextReader>().GetAsync(account.ApplicationId);
			if (!source.CanCollect || source.StudentId != account.StudentId || source.AcademicYearId != account.AcademicYearId)
				throw new BusinessException(AdmissionFeeCollectionErrorCodes.InvalidState);
			var saved = account.AssignmentSnapshotJson == null ? null :
				JsonSerializer.Deserialize<ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos.StudentFeeAssignmentPreviewDto>(account.AssignmentSnapshotJson);
			if (saved == null || !AdmissionFeeCollectionAppService.SelectionMatches(source, saved))
				throw new BusinessException(AdmissionFeeCollectionErrorCodes.SelectionChanged);
		}
		var student = account == null ? (await ValidateAndLoadStudentForCollectionAsync(input)).Student :
			account.StudentId.HasValue ? await studentRepository.FindAsync(account.StudentId.Value) : null;
		var academicYear = await academicYearRepository.GetAsync(input.AcademicYearId);
		await ValidatePaymentChannelPolicyAsync(input);
		var dueFeeContext = await LoadAndValidateDueFeesContextAsync(input);
		var chargeContext = await LoadAndValidateChargesContextAsync(input);
		var lateFeeMap = await ValidateLateFeePaymentsAsync(input);
		// Snapshot all accrued charges before principal can change, even when charges are unselected.
		await lateFeeBalanceService.GetBalancesAsync(dueFeeContext.DueFeeMap.Values.ToList(),
			input.AcademicYearId, clock.Now, preserveAssessment: true);

		var paymentMethods = input.Payments.Select(p => p.PaymentMethod).ToList();
		var received = input.DueFeePayments.Sum(dfp => dfp.Amount) + input.ChargePayments.Sum(cp => cp.Amount)
			+ input.LateFeePayments.Sum(lfp => lfp.Amount);
		var status = DetermineCollectionStatus(
			input.DueFeePayments, dueFeeContext.DueFeeMap,
			input.ChargePayments, chargeContext.PendingChargeMap,
			paymentMethods, input.PaymentConfirmed ?? true);

		var routing = await PrepareIncomeRoutingAsync(input, dueFeeContext, chargeContext, lateFeeMap);
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
				BoardId: account?.BoardId ?? student!.AcademicBoardId ?? Guid.Empty, EducationMediumId: account?.EducationMediumId ?? student!.AcademicMediumId,
				Status: status, ReceivedAmount: received, Notes: input.Notes,
				BankReceiptConfirmed: input.PaymentConfirmed, FeeHeadGroups: feeHeadGroups,
				ChargePayments: input.ChargePayments.Select(cp => (cp.ChargeId, cp.Amount)).ToList(),
				DueFeeAllocations: allAllocations, Payments: paymentRows,
				LateFeeAllocations: input.LateFeePayments.Select(lfp => (lfp.DueFeeId, lfp.Amount)).ToList(),
				IdempotencyKey: idempotencyKey, CommandFingerprint: commandFingerprint, FinanceFeeAccountId: account?.Id));
		if (account != null)
		{
			account.RecordPaymentEvidence();
			await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>().UpdateAsync(account, false);
			await LazyServiceProvider.LazyGetRequiredService<IAdmissionFinanceContextReader>().RecordPaymentEvidenceAsync(account.ApplicationId, account.Id);
		}

		await SaveIncomeRoutingAsync(input, transaction, routing, status != FeeCollectionStatus.InReconciliation);
        // Flush the unique retry key before creating external blob side effects. A racing
		// duplicate fails here and the unit of work rolls back all collection mutations.
		await CurrentUnitOfWork!.SaveChangesAsync();

		await GenerateAndStoreReceiptPdfsAsync(transaction.Receipts, student, academicYear);

		var (auditEventIds, paymentChangedFields, allocationLines) =
			await WriteAuditTrailAsync(input, received, status, dueFeeContext, dueFeeBeforeSnapshots,
				paymentMethods, transaction, student, account);

		var feeReceipt = transaction.Receipts.FirstOrDefault(r => r.ReceiptScope == FeeReceiptScope.Fee);
		var chargeReceipt = transaction.Receipts.FirstOrDefault(r => r.ReceiptScope == FeeReceiptScope.Charge);
		var collectedAt = clock.Now;

		var output = new CollectFeeOutput
		{
			Id = transaction.Metadata.Id, ReceiptNumber = transaction.ReceiptNumber,
			FeeReceiptNumber = feeReceipt?.ReceiptNumber, FeeReceiptId = feeReceipt?.Id,
			ChargeReceiptId = chargeReceipt?.Id, ChargeReceiptNumber = chargeReceipt?.ReceiptNumber,
			StudentId = input.StudentId, FinanceFeeAccountId = account?.Id, AcademicYearId = input.AcademicYearId,
			TotalAmount = received, ReceivedAmount = received, Status = status,
			CollectedBy = CurrentUser.Id ?? Guid.Empty, CollectedAt = collectedAt, CreatedAt = collectedAt,
			ReceiptUrl = null,
			PaidDueFees = input.DueFeePayments.Select(dfp => new PaidDueFeeInfoDto
			{
				DueFeeId = dfp.DueFeeId, FeeHeadId = dfp.FeeHeadId, AmountPaid = dfp.Amount,
			}).ToList(),
			PaidLateFees = input.LateFeePayments.Select(lfp => new PaidDueFeeInfoDto
			{
				DueFeeId = lfp.DueFeeId,
				FeeHeadId = lateFeeMap[lfp.DueFeeId].FeeHeadId,
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

		transaction.Metadata.SetCommandResult(JsonSerializer.Serialize(output));
		await CurrentUnitOfWork.SaveChangesAsync();

		await eventBus.PublishAsync(new FeeCollectedEto
		{
			FeeCollectionTransactionId = transaction.Metadata.Id,
			StudentId      = input.StudentId,
			FinanceFeeAccountId = account?.Id,
			AcademicYearId = input.AcademicYearId,
			TotalPaidFee   = received,
			Status         = status,
			PaymentMethods = paymentMethods,
			TenantId       = CurrentTenant.Id,
			CollectedAt    = collectedAt,
		});

		Logger.LogInformation(
			"Fee collected for student {StudentId}: receipt {ReceiptNumber}, amount {Amount}, receipts created {ReceiptCount}",
			input.StudentId, transaction.ReceiptNumber, received, transaction.Receipts.Count);

		await serializableUow.CompleteAsync();
		return output;
	}

	private static string ComputeCommandFingerprint(CollectFeeInput input)
	{
		// Student identity can be linked later. The application's retry fingerprint
		// contains payment facts and its stable Finance account, never the derived link.
		var json = input.FinanceFeeAccountId.HasValue ? JsonSerializer.Serialize(new {
			input.FinanceFeeAccountId, input.AcademicYearId, input.IdempotencyKey, input.Notes, input.PaymentConfirmed,
			input.DueFeePayments, input.ChargePayments, input.LateFeePayments, input.Payments }) : JsonSerializer.Serialize(input);
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
	}

	private sealed record DueFeeValidationContext(
		Dictionary<Guid, DueFee> DueFeeMap, Dictionary<Guid, FeeHead> FeeHeadMap);

	private sealed record ChargeValidationContext(
		Dictionary<Guid, Charge> ChargeMap, Dictionary<Guid, double> PendingChargeMap);
}
