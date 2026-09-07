using ShikshaMitra.Finance.Audit;
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

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class CollectFeeWorkflow
{
	private async Task<(List<Guid> AuditEventIds, List<FinanceAuditChangedFieldDto> PaymentChangedFields, List<FinanceAuditAllocationLineDto> AllocationLines)>
		WriteAuditTrailAsync(
			CollectFeeInput input,
			double received,
			FeeCollectionStatus status,
			DueFeeValidationContext dueFeeContext,
			Dictionary<Guid, double> dueFeeBeforeSnapshots,
			List<PaymentMethodEnum> paymentMethods,
			FeeCollectionManager.CollectFeeTransactionResult transaction,
			Student student)
	{
		var correlationId = $"fc-{transaction.Metadata.Id:N}";
		var studentContext = new FinanceAuditStudentContext
		{
			StudentId = input.StudentId,
			StudentName = BuildFullName(student.FirstName, student.MiddleName, student.LastName),
			GrNo = student.GrNo?.ToString(),
			RollNo = student.RollNo?.ToString()
		};

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
			var installmentLabel = df.GetDisplayInstallmentLabel();
			allocationLines.Add(new FinanceAuditAllocationLineDto
			{
				DueFeeId = dfp.DueFeeId, FeeHeadId = dfp.FeeHeadId, FeeHeadName = feeHeadName,
				InstallmentLabel = installmentLabel, AmountPaid = dfp.Amount,
				PendingBefore = pendingBefore, PendingAfter = pendingAfter
			});
			paymentChangedFields.Add(new FinanceAuditChangedFieldDto
			{
				Field = $"{feeHeadName} — {installmentLabel}",
				OldValue = pendingBefore.ToString("F2"),
				NewValue = pendingAfter.ToString("F2")
			});
		}

		var paymentSummary = $"Fee payment of ₹{received:N0} received ({transaction.ReceiptNumber})";
		var paymentWrite = await financeAuditWriter.WriteAsync(
			input.AcademicYearId, FinanceAuditEntityTypes.FeePayment,
			transaction.Metadata.Id, transaction.ReceiptNumber,
			FinanceAuditActions.Payment, paymentSummary, studentContext,
			input.Notes, paymentChangedFields,
			new { paymentMethods = paymentMethods.Select(m => m.ToString()).ToList(), receiptNumber = transaction.ReceiptNumber, received, status = status.ToString() },
			correlationId);

		var auditEventIds = new List<Guid> { paymentWrite.EventId };

		var feeReceipt = transaction.Receipts.FirstOrDefault(r => r.ReceiptScope == FeeReceiptScope.Fee);
		if (feeReceipt != null)
		{
			var receiptWrite = await financeAuditWriter.WriteAsync(
				input.AcademicYearId, FinanceAuditEntityTypes.FeeReceipt,
				feeReceipt.Id, feeReceipt.ReceiptNumber,
				FinanceAuditActions.GenerateReceipt, $"Receipt {feeReceipt.ReceiptNumber} generated",
				studentContext, null,
				[
					new FinanceAuditChangedFieldDto { Field = "receiptNumber", OldValue = null, NewValue = feeReceipt.ReceiptNumber },
					new FinanceAuditChangedFieldDto { Field = "amount", OldValue = null, NewValue = transaction.FeeLinesTotal.ToString("F2") }
				],
				new { feeReceiptId = feeReceipt.Id, feeReceiptNumber = feeReceipt.ReceiptNumber },
				correlationId, paymentWrite.EventId);
			auditEventIds.Add(receiptWrite.EventId);
		}

		var chargeReceipt = transaction.Receipts.FirstOrDefault(r => r.ReceiptScope == FeeReceiptScope.Charge);
		if (chargeReceipt != null)
		{
			var chargeReceiptWrite = await financeAuditWriter.WriteAsync(
				input.AcademicYearId, FinanceAuditEntityTypes.FeeReceipt,
				chargeReceipt.Id, chargeReceipt.ReceiptNumber,
				FinanceAuditActions.GenerateReceipt, $"Charge receipt {chargeReceipt.ReceiptNumber} generated",
				studentContext, null,
				[
					new FinanceAuditChangedFieldDto { Field = "receiptNumber", OldValue = null, NewValue = chargeReceipt.ReceiptNumber },
					new FinanceAuditChangedFieldDto { Field = "amount", OldValue = null, NewValue = transaction.ChargeLinesTotal.ToString("F2") }
				],
				new { chargeReceiptId = chargeReceipt.Id, chargeReceiptNumber = chargeReceipt.ReceiptNumber },
				correlationId, paymentWrite.EventId);
			auditEventIds.Add(chargeReceiptWrite.EventId);
		}

		return (auditEventIds, paymentChangedFields, allocationLines);
	}

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

	private static FeeCollectionStatus DetermineCollectionStatus(
		IReadOnlyList<DueFeePaymentDto> dueFeePayments,
		IReadOnlyDictionary<Guid, DueFee> dueFeeMap,
		IReadOnlyList<ChargePaymentDto> chargePayments,
		IReadOnlyDictionary<Guid, double> pendingChargeMap,
		IReadOnlyList<PaymentMethodEnum> paymentMethods,
		bool isBankReceiptConfirmed = true)
	{
		var isAnyPartial = dueFeePayments.Any(dfp => (decimal)dfp.Amount < dueFeeMap[dfp.DueFeeId].OutstandingAmount)
			|| chargePayments.Any(cp => cp.Amount < pendingChargeMap[cp.ChargeId]);

		return FeeReceiptPaymentPolicy.RequiresVerification(paymentMethods) || !isBankReceiptConfirmed
			? FeeCollectionStatus.InReconciliation
			: (isAnyPartial ? FeeCollectionStatus.Partial : FeeCollectionStatus.Paid);
	}

	private static Dictionary<Guid, double> BuildDueFeeBeforeSnapshots(
		IReadOnlyList<DueFeePaymentDto> dueFeePayments,
		IReadOnlyDictionary<Guid, DueFee> dueFeeMap) =>
		dueFeePayments.ToDictionary(dfp => dfp.DueFeeId, dfp => (double)dueFeeMap[dfp.DueFeeId].OutstandingAmount);

	private static string BuildFullName(string? first, string? middle, string? last) =>
		string.Join(" ", new[] { first, middle, last }.Where(p => !string.IsNullOrWhiteSpace(p)));
}
