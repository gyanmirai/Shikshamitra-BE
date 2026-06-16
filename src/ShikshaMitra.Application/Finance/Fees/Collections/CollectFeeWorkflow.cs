using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace ShikshaMitra.Finance.Fees.Collections;

[RemoteService(false)]
public class CollectFeeWorkflow(
	IReadOnlyRepository<Student, Guid> studentRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
	IRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<Charge, Guid> chargeRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	FeeCollectionManager feeCollectionManager,
	FinanceAuditWriter financeAuditWriter,
	FinanceScopeAccessService financeScopeAccessService,
	IFeeReceiptPdfGenerator pdfGenerator,
	FeeReceiptBlobManager blobManager,
	IClock clock
) : ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)]
	public async Task<CollectFeeOutput> CollectFeeAsync(CollectFeeInput input)
	{
		var (student, academicYear) = await ValidateAndLoadStudentForCollectionAsync(input);
		var dueFeeContext = await LoadAndValidateDueFeesContextAsync(input);
		var chargeContext = await LoadAndValidateChargesContextAsync(input);

		var paymentMethods = input.Payments.Select(p => p.PaymentMethod).ToList();
		var received = input.DueFeePayments.Sum(dfp => dfp.Amount) + input.ChargePayments.Sum(cp => cp.Amount);
		var status = DetermineCollectionStatus(
			input.DueFeePayments,
			dueFeeContext.DueFeeMap,
			input.ChargePayments,
			chargeContext.PendingChargeMap,
			paymentMethods,
			input.PaymentConfirmed ?? true);

		var dueFeeBeforeSnapshots = BuildDueFeeBeforeSnapshots(input.DueFeePayments, dueFeeContext.DueFeeMap);

		var feeHeadGroups = input.DueFeePayments
			.GroupBy(dfp => dfp.FeeHeadId)
			.Select(g => (FeeHeadId: g.Key, Amount: g.Sum(dfp => dfp.Amount)))
			.ToList();

		var allAllocations = input.DueFeePayments
			.Select(dfp => (dfp.DueFeeId, dfp.Amount))
			.ToList();

		var paymentRows = input.Payments
			.Select(p => (
				p.PaymentMethod,
				p.Amount,
				FeeCollectionManager.MapPaymentDetails(p.PaymentMethod, p.Details)))
			.ToList();

		var transaction = await feeCollectionManager.ExecuteCollectionTransactionAsync(
			new FeeCollectionManager.CollectFeeTransactionCommand(
				StudentId: input.StudentId,
				AcademicYearId: input.AcademicYearId,
				BoardId: student.AcademicBoardId ?? Guid.Empty,
				EducationMediumId: student.AcademicMediumId,
				Status: status,
				ReceivedAmount: received,
				Notes: input.Notes,
				BankReceiptConfirmed: input.PaymentConfirmed,
				FeeHeadGroups: feeHeadGroups,
				ChargePayments: input.ChargePayments.Select(cp => (cp.ChargeId, cp.Amount)).ToList(),
				DueFeeAllocations: allAllocations,
				Payments: paymentRows));

		var correlationId = $"fc-{transaction.Metadata.Id:N}";
		var studentContext = new FinanceAuditStudentContext
		{
			StudentId = input.StudentId,
			StudentName = BuildFullName(student.FirstName, student.MiddleName, student.LastName),
			GrNo = student.GrNo?.ToString(),
			RollNo = student.RollNo?.ToString()
		};

		// ── Eager PDF generation ─────────────────────────────────────────────────────────────────
		// Generates and persists receipt PDFs before SaveChanges so PdfBlobName is written
		// atomically with the receipt row, making the download URL available in the response.
		await GenerateAndStoreReceiptPdfsAsync(transaction.Receipts, student, academicYear);

		var allocationLines = new List<FinanceAuditAllocationLineDto>();
		var paymentChangedFields = new List<FinanceAuditChangedFieldDto>
		{
			new() { Field = "totalAmount", OldValue = null, NewValue = received.ToString("F2") },
			new() { Field = "status", OldValue = null, NewValue = status.ToString() }
		};

		foreach (var dfp in input.DueFeePayments)
		{
			var df = dueFeeContext.DueFeeMap[dfp.DueFeeId];
			var pendingBefore = dueFeeBeforeSnapshots[dfp.DueFeeId];
			var pendingAfter = Math.Max(0, pendingBefore - dfp.Amount);
			var feeHeadName = dueFeeContext.FeeHeadMap[dfp.FeeHeadId].Name;
			var installmentLabel = $"{df.Month} {df.Year}";
			allocationLines.Add(new FinanceAuditAllocationLineDto
			{
				DueFeeId = dfp.DueFeeId,
				FeeHeadId = dfp.FeeHeadId,
				FeeHeadName = feeHeadName,
				InstallmentLabel = installmentLabel,
				AmountPaid = dfp.Amount,
				PendingBefore = pendingBefore,
				PendingAfter = pendingAfter
			});
			paymentChangedFields.Add(new FinanceAuditChangedFieldDto
			{
				Field = $"{feeHeadName} {df.Month.ToString()} - {df.Year}",
				OldValue = pendingBefore.ToString("F2"),
				NewValue = pendingAfter.ToString("F2")
			});
		}

		var paymentSummary =
			$"Fee payment of ₹{received:N0} received ({transaction.ReceiptNumber})";
		var paymentWrite = await financeAuditWriter.WriteAsync(
			input.AcademicYearId,
			FinanceAuditEntityTypes.FeePayment,
			transaction.Metadata.Id,
			transaction.ReceiptNumber,
			FinanceAuditActions.Payment,
			paymentSummary,
			studentContext,
			input.Notes,
			paymentChangedFields,
			new
			{
				paymentMethods = paymentMethods.Select(m => m.ToString()).ToList(),
				receiptNumber = transaction.ReceiptNumber,
				received,
				status = status.ToString()
			},
			correlationId);

		var auditEventIds = new List<Guid> { paymentWrite.EventId };

		var feeReceipt = transaction.Receipts.FirstOrDefault(r => r.ReceiptScope == FeeReceiptScope.Fee);
		if (feeReceipt != null)
		{
			var receiptWrite = await financeAuditWriter.WriteAsync(
				input.AcademicYearId,
				FinanceAuditEntityTypes.FeeReceipt,
				feeReceipt.Id,
				feeReceipt.ReceiptNumber,
				FinanceAuditActions.GenerateReceipt,
				$"Receipt {feeReceipt.ReceiptNumber} generated",
				studentContext,
				null,
				[
					new FinanceAuditChangedFieldDto
					{
						Field = "receiptNumber",
						OldValue = null,
						NewValue = feeReceipt.ReceiptNumber
					},
					new FinanceAuditChangedFieldDto
					{
						Field = "amount",
						OldValue = null,
						NewValue = transaction.FeeLinesTotal.ToString("F2")
					}
				],
				new { feeReceiptId = feeReceipt.Id, feeReceiptNumber = feeReceipt.ReceiptNumber },
				correlationId,
				paymentWrite.EventId);
			auditEventIds.Add(receiptWrite.EventId);
		}

		// ── Charge receipt audit ─────────────────────────────────────────────────────────────────
		var chargeReceipt = transaction.Receipts.FirstOrDefault(r => r.ReceiptScope == FeeReceiptScope.Charge);
		if (chargeReceipt != null)
		{
			var chargeReceiptWrite = await financeAuditWriter.WriteAsync(
				input.AcademicYearId,
				FinanceAuditEntityTypes.FeeReceipt,
				chargeReceipt.Id,
				chargeReceipt.ReceiptNumber,
				FinanceAuditActions.GenerateReceipt,
				$"Charge receipt {chargeReceipt.ReceiptNumber} generated",
				studentContext,
				null,
				[
					new FinanceAuditChangedFieldDto
					{
						Field    = "receiptNumber",
						OldValue = null,
						NewValue = chargeReceipt.ReceiptNumber
					},
					new FinanceAuditChangedFieldDto
					{
						Field    = "amount",
						OldValue = null,
						NewValue = transaction.ChargeLinesTotal.ToString("F2")
					}
				],
				new { chargeReceiptId = chargeReceipt.Id, chargeReceiptNumber = chargeReceipt.ReceiptNumber },
				correlationId,
				paymentWrite.EventId);
			auditEventIds.Add(chargeReceiptWrite.EventId);
		}

		await CurrentUnitOfWork!.SaveChangesAsync();

		Logger.LogInformation(
			"Fee collected for student {StudentId}: receipt {ReceiptNumber}, amount {Amount}, receipts created {ReceiptCount}",
			input.StudentId, transaction.ReceiptNumber, received, transaction.Receipts.Count);

		return new CollectFeeOutput
		{
			Id = transaction.Metadata.Id,
			ReceiptNumber = transaction.ReceiptNumber,
			FeeReceiptNumber = feeReceipt?.ReceiptNumber,
			FeeReceiptId = feeReceipt?.Id,
			ChargeReceiptId = chargeReceipt?.Id,
			ChargeReceiptNumber = chargeReceipt?.ReceiptNumber,
			StudentId = input.StudentId,
			AcademicYearId = input.AcademicYearId,
			TotalAmount = received,
			ReceivedAmount = received,
			Status = status,
			CollectedBy = CurrentUser.Id ?? Guid.Empty,
			CollectedAt = clock.Now,
			CreatedAt = clock.Now,
			ReceiptUrl = null,
			PaidDueFees = input.DueFeePayments
				.Select(dfp => new PaidDueFeeInfoDto
				{
					DueFeeId = dfp.DueFeeId,
					FeeHeadId = dfp.FeeHeadId,
					AmountPaid = dfp.Amount,
				})
				.ToList(),
			PaidFeeHeads = transaction.FeeCollectionRecords
				.Select(r => new PaidFeeHeadInfoDto
				{
					FeeHeadId = r.FeeHeadId,
					FeeHeadName = dueFeeContext.FeeHeadMap[r.FeeHeadId].Name,
					AmountPaid = r.Amount,
				})
				.ToList(),
			PaidCharges = transaction.ChargeCollectionRecords.Count > 0
				? transaction.ChargeCollectionRecords
					.Select(r => new PaidChargeInfoDto
					{
						ChargeId = r.ChargeId,
						ChargeName = string.IsNullOrWhiteSpace(chargeContext.ChargeMap[r.ChargeId].ChargeName)
							? chargeContext.ChargeMap[r.ChargeId].PurposeDescription
							: chargeContext.ChargeMap[r.ChargeId].ChargeName,
						AmountPaid = r.Amount,
					})
					.ToList()
				: null,
			CorrelationId = correlationId,
			AuditEventIds = auditEventIds,
			AuditSummary = new FinanceAuditSummaryDto
			{
				Headline = status == FeeCollectionStatus.InReconciliation
					? "Payment saved as pending verification"
					: "Fee collected successfully",
				TotalAmount = received,
				ChangedFields = paymentChangedFields,
				AllocationLines = allocationLines
			}
		};
	}

	/// <summary>
	/// Validates the collection context and loads both the <see cref="Student"/> and
	/// <see cref="AcademicYear"/> needed for domain validation and eager receipt PDF generation,
	/// avoiding a second round-trip later in the workflow.
	/// </summary>
	private async Task<(Student Student, AcademicYear AcademicYear)> ValidateAndLoadStudentForCollectionAsync(CollectFeeInput input)
	{
		var academicYear = await academicYearRepository.GetAsync(input.AcademicYearId);

		var student = await studentRepository.GetAsync(input.StudentId);
		if (student.AcademicYearId != input.AcademicYearId)
			throw new BusinessException(FeeCollectionErrorCodes.StudentAcademicYearMismatch);

		await financeScopeAccessService.EnsureStudentAccessibleAsync(input.StudentId);
		return (student, academicYear);
	}

	private async Task<DueFeeValidationContext> LoadAndValidateDueFeesContextAsync(CollectFeeInput input)
	{
		var dueFeeMap = new Dictionary<Guid, DueFee>();
		var feeHeadMap = new Dictionary<Guid, FeeHead>();

		if (input.DueFeePayments is not { Count: > 0 })
			return new DueFeeValidationContext(dueFeeMap, feeHeadMap);

		var dueFeeIds = input.DueFeePayments.Select(dfp => dfp.DueFeeId).Distinct().ToList();
		var dueFees = await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id));
		if (dueFees.Count != dueFeeIds.Count)
			throw new BusinessException(DueFeeErrorCodes.DueFeeNotFound);

		dueFeeMap = dueFees.ToDictionary(df => df.Id);

		var feeHeadIds = input.DueFeePayments.Select(dfp => dfp.FeeHeadId).Distinct().ToList();
		var feeHeadsList = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
		if (feeHeadsList.Count != feeHeadIds.Count)
			throw new BusinessException(FeeCollectionErrorCodes.InvalidFeeHead);

		feeHeadMap = feeHeadsList.ToDictionary(fh => fh.Id);

		foreach (var dfp in input.DueFeePayments)
		{
			var df = dueFeeMap[dfp.DueFeeId];
			if (df.StudentId != input.StudentId)
				throw new BusinessException(FeeCollectionErrorCodes.InvalidDueFee);

			if (df.FeeHeadId != dfp.FeeHeadId)
				throw new BusinessException(FeeCollectionErrorCodes.InvalidFeeHead);

			if (dfp.Amount > df.OutstandingAmount)
			{
				throw new BusinessException(FeeCollectionErrorCodes.AmountExceedsOutstanding)
					.WithData("DueFeeId", df.Id)
					.WithData("Outstanding", df.OutstandingAmount)
					.WithData("Amount", dfp.Amount);
			}
		}

		return new DueFeeValidationContext(dueFeeMap, feeHeadMap);
	}

	private async Task<ChargeValidationContext> LoadAndValidateChargesContextAsync(CollectFeeInput input)
	{
		var chargeMap = new Dictionary<Guid, Charge>();
		var pendingChargeMap = new Dictionary<Guid, double>();

		if (input.ChargePayments is not { Count: > 0 })
			return new ChargeValidationContext(chargeMap, pendingChargeMap);

		var chargeIds = input.ChargePayments.Select(cp => cp.ChargeId).Distinct().ToList();
		var charges = await chargeRepository.GetListAsync(c =>
			chargeIds.Contains(c.Id) && c.Status == ChargeStatus.Approved);
		if (charges.Count != chargeIds.Count)
			throw new BusinessException(FeeCollectionErrorCodes.InvalidCharge);

		chargeMap = charges.ToDictionary(c => c.Id);

		var chargeCollQ = await chargeCollectionRecordRepository.GetQueryableAsync();
		var chargeMetaQ = await feeRecordMetadataRepository.GetQueryableAsync();
		var reversedTxIds = chargeMetaQ
			.Where(m => m.Status == FeeCollectionStatus.Reversed)
			.Select(m => m.Id);

		var paidPerCharge = chargeCollQ
			.Where(r => r.StudentId == input.StudentId
					 && !reversedTxIds.Contains(r.FeeRecordMetadataId)
					 && chargeIds.Contains(r.ChargeId))
			.GroupBy(r => r.ChargeId)
			.Select(g => new { ChargeId = g.Key, Paid = g.Sum(r => r.Amount) })
			.ToDictionary(x => x.ChargeId, x => x.Paid);

		foreach (var cp in input.ChargePayments)
		{
			var charge = chargeMap[cp.ChargeId];
			if (charge.StudentId != input.StudentId || charge.AcademicYearId != input.AcademicYearId)
				throw new BusinessException(FeeCollectionErrorCodes.InvalidCharge);

			var paid = paidPerCharge.GetValueOrDefault(cp.ChargeId, 0d);
			var pending = Math.Max(0d, (double)charge.Amount - paid);
			pendingChargeMap[cp.ChargeId] = pending;

			if (cp.Amount > pending)
			{
				throw new BusinessException(FeeCollectionErrorCodes.AmountExceedsOutstanding)
					.WithData("ChargeId", charge.Id)
					.WithData("Pending", pending)
					.WithData("Amount", cp.Amount);
			}
		}

		return new ChargeValidationContext(chargeMap, pendingChargeMap);
	}

	private static FeeCollectionStatus DetermineCollectionStatus(
		IReadOnlyList<DueFeePaymentDto> dueFeePayments,
		IReadOnlyDictionary<Guid, DueFee> dueFeeMap,
		IReadOnlyList<ChargePaymentDto> chargePayments,
		IReadOnlyDictionary<Guid, double> pendingChargeMap,
		IReadOnlyList<PaymentMethodEnum> paymentMethods,
		bool isBankReceiptConfirmed = true)
	{
		var isAnyPartial = false;
		foreach (var dfp in dueFeePayments)
		{
			var df = dueFeeMap[dfp.DueFeeId];
			if (dfp.Amount < df.OutstandingAmount)
			{
				isAnyPartial = true;
				break;
			}
		}

		if (!isAnyPartial)
		{
			foreach (var cp in chargePayments)
			{
				var pending = pendingChargeMap[cp.ChargeId];
				if (cp.Amount < pending)
				{
					isAnyPartial = true;
					break;
				}
			}
		}

		return FeeReceiptPaymentPolicy.RequiresVerification(paymentMethods) || !isBankReceiptConfirmed
			? FeeCollectionStatus.InReconciliation
			: (isAnyPartial ? FeeCollectionStatus.Partial : FeeCollectionStatus.Paid);
	}

	/// <summary>
	/// Eagerly generates a PDF for every <see cref="FeePaymentReceipt"/> produced by this
	/// collection event and persists each document to ABP Blob Storing, setting
	/// <see cref="FeePaymentReceipt.PdfBlobName"/> before the Unit-of-Work is saved.
	/// <para>
	/// By generating PDFs inside the same UoW scope, the blob name is committed atomically
	/// with the receipt row — the client can request the download URL immediately from the
	/// post-payment response without triggering a separate lazy-generation round-trip.
	/// </para>
	/// </summary>
	private async Task GenerateAndStoreReceiptPdfsAsync(
		IReadOnlyList<FeePaymentReceipt> receipts,
		Student student,
		AcademicYear academicYear,
		CancellationToken cancellationToken = default)
	{
		foreach (var receipt in receipts)
		{
			var pdfBytes = await pdfGenerator.GenerateAsync(receipt, student, academicYear, cancellationToken);
			var blobName = await blobManager.SavePdfAsync(receipt.Id, receipt.ReceiptKind, pdfBytes, cancellationToken);
			receipt.SetPdfBlobName(blobName);
		}
	}

	private static Dictionary<Guid, double> BuildDueFeeBeforeSnapshots(
		IReadOnlyList<DueFeePaymentDto> dueFeePayments,
		IReadOnlyDictionary<Guid, DueFee> dueFeeMap)
	{
		return dueFeePayments
			.Select(dfp =>
			{
				var df = dueFeeMap[dfp.DueFeeId];
				return (dfp.DueFeeId, df.OutstandingAmount);
			})
			.ToDictionary(
				x => x.DueFeeId,
				x => x.OutstandingAmount);
	}

	private static string BuildFullName(string? first, string? middle, string? last)
	{
		var parts = new[] { first, middle, last };
		return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
	}

	private sealed record DueFeeValidationContext(
		Dictionary<Guid, DueFee> DueFeeMap,
		Dictionary<Guid, FeeHead> FeeHeadMap);

	private sealed record ChargeValidationContext(
		Dictionary<Guid, Charge> ChargeMap,
		Dictionary<Guid, double> PendingChargeMap);
}
