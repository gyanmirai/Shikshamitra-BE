using System;
using System.Linq;
using ShikshaMitra.Application.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

/// <summary>Read-only receipt projection; payout and authorization facts remain owned by Refunds.</summary>
public static class RefundReceiptDocumentFactory
{
	public static ReceiptDocumentModel Create(FeeRefundRequestDto refund, ReceiptSchoolIdentity school, string? grNumber = null)
	{
		var provisional = refund.Status == FeeRefundStatus.Processing;
		var settlement = refund.Settlements.Where(item => item.Status ==
			(provisional ? FeeRefundSettlementStatus.Processing : FeeRefundSettlementStatus.Processed))
			.OrderByDescending(item => item.AttemptNumber).FirstOrDefault();
		if ((provisional && settlement == null) || (!provisional &&
			(refund.Status != FeeRefundStatus.Processed || refund.ProcessedAmount <= 0)))
			throw new BusinessException(FeeRefundErrorCodes.PaymentNotSettled);
		var date = settlement?.VerifiedAt ?? refund.ProcessedAt ?? settlement?.SettlementDate ?? refund.CreationTime;
		return new ReceiptDocumentModel
		{
			School = school, Title = provisional ? "PROVISIONAL REFUND RECEIPT" : "REFUND RECEIPT",
			Number = refund.VoucherNumber, Date = DateOnly.FromDateTime(date.DateTime), AcademicYear = refund.AcademicYearName,
			IsRefund = true, Amount = provisional ? settlement!.Amount : refund.ProcessedAmount,
			StudentFields = [new("Student name", refund.StudentName), new("GR No.", grNumber ?? refund.StudentGrNo?.ToString()),
				new("Class / section", string.Join(" - ", new[] { refund.StandardName, refund.DivisionName }.Where(value => !string.IsNullOrWhiteSpace(value)))),
				new("Roll no.", refund.StudentRollNo?.ToString())],
			PaymentFields = [new("Payment mode", settlement?.Method ?? refund.RefundMethod),
				new("Payment date", (settlement?.SettlementDate ?? refund.ProcessedAt)?.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)),
				new("Bank", refund.PayingBankLabel), new("Transaction / instrument no.", settlement?.ExternalReference)],
			Lines = refund.Lines.Select(line => new ReceiptDocumentLine(line.FeeHeadName, line.InstallmentLabel,
				line.ApprovedAmount, line.ProcessedAmount, Math.Max(0, line.ApprovedAmount - line.ProcessedAmount),
				line.ProcessedAmount, line.ChargeId.HasValue || line.LateFeeDueFeeId.HasValue)).ToList(),
			RelatedReceipt = refund.SourceReceipts.Count > 0 ? string.Join(", ", refund.SourceReceipts.Select(item => item.ReceiptNumber).Distinct())
				: refund.OriginalReceiptNumber
		};
	}
}
