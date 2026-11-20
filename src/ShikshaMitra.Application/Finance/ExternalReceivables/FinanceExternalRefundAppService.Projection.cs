using System.Linq;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;

namespace ShikshaMitra.Finance.ExternalReceivables;

public partial class FinanceExternalRefundAppService
{
	private static FeeRefundRequestDto Map(FeeRefundRequest request, FinanceExternalSettlementContext context) => new()
	{
		Id = request.Id, ConcurrencyStamp = request.ConcurrencyStamp, StudentId = request.StudentId, EmployeeId = request.EmployeeId,
		FinanceExternalSourceLinkId = request.FinanceExternalSourceLinkId, PayerName = context.Source.Payer.DisplayName,
		AcademicYearId = request.AcademicYearId, Status = request.Status, WorkflowVersion = request.WorkflowVersion,
		RefundPolicyId = request.RefundPolicyId, RefundPolicyVersionId = request.RefundPolicyVersionId,
		OriginalReceiptId = request.OriginalReceiptId, OriginalReceiptNumber = request.OriginalReceiptNumber, Currency = request.Currency,
		VoucherNumber = request.VoucherNumber, TotalAmount = request.TotalAmount, CalculatedAmount = request.CalculatedAmount,
		ApprovedAmount = request.ApprovedAmount, ProcessedAmount = request.ProcessedAmount, ReversedAmount = request.ReversedAmount,
		RefundMethod = request.RefundMethod, Reason = request.Reason, CreationTime = request.CreationTime, SubmittedAt = request.SubmittedAt,
		Lines = request.Lines.Select(line => new FeeRefundLineDto
		{
			Id = line.Id, ChargeId = line.ChargeId, SourceAllocationId = line.SourceAllocationId, SourceReceiptId = line.SourceReceiptId,
			FeeHeadId = line.FeeHeadId, FeeHeadName = line.FeeHeadName, InstallmentLabel = line.InstallmentLabel,
			SettledPaidAmount = line.SettledPaidAmount, Amount = line.Amount, CalculatedAmount = line.CalculatedAmount,
			ApprovedAmount = line.ApprovedAmount, ProcessedAmount = line.ProcessedAmount, ReversedAmount = line.ReversedAmount
		}).ToList()
	};
}
