using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Students;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public partial class FeeRefundAppService
{
	private async Task<Dictionary<Guid, Student>> BuildStudentMapAsync(IEnumerable<Guid> studentIds)
	{
		var ids = studentIds.ToList();
		if (ids.Count == 0) return [];
		var students = await studentRepository.GetListAsync(s => ids.Contains(s.Id));
		return students.ToDictionary(s => s.Id);
	}

	private static FeeRefundRequestDto MapToDto(FeeRefundRequest r, Dictionary<Guid, Student> studentMap)
	{
		studentMap.TryGetValue(r.StudentId, out var student);
		var isLegacy = !r.RefundPolicyVersionId.HasValue && !r.ConcessionRequestId.HasValue;
		var legacyFinancialAmount = isLegacy && r.Status == FeeRefundStatus.Approved ? r.TotalAmount : 0m;
		var fullName = student is null ? string.Empty
			: string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
				.Where(p => !string.IsNullOrWhiteSpace(p)));

		return new FeeRefundRequestDto
		{
			Id = r.Id, StudentId = r.StudentId, AcademicYearId = r.AcademicYearId,
			Status = r.Status, VoucherNumber = r.VoucherNumber, TotalAmount = r.TotalAmount,
			CalculatedAmount = isLegacy ? r.TotalAmount : r.CalculatedAmount,
			StudentFeeStructureId = r.StudentFeeStructureId, RefundPolicyId = r.RefundPolicyId,
			RefundPolicyVersionId = r.RefundPolicyVersionId, RefundPolicyName = GetPolicyName(r.PolicySnapshotJson),
			ConcessionRequestId = r.ConcessionRequestId,
			OriginalReceiptId = r.OriginalReceiptId,
			OriginalReceiptNumber = r.OriginalReceiptNumber, Currency = string.IsNullOrWhiteSpace(r.Currency) ? "INR" : r.Currency,
			ApprovedAmount = isLegacy ? legacyFinancialAmount : r.ApprovedAmount,
			ProcessedAmount = isLegacy ? legacyFinancialAmount : r.ProcessedAmount, ReversedAmount = r.ReversedAmount,
			RefundMethod = r.RefundMethod, PaymentDetails = MaskPaymentDetails(MapRefundPaymentDetailsToDict(r.RefundPaymentDetails)),
			Reason = r.Reason, ApproverNotes = r.ApproverNotes,
			ProcessedByUserId = r.ProcessedByUserId, ProcessedAt = r.ProcessedAt,
			CreationTime = r.CreationTime, SubmittedAt = r.SubmittedAt, ReviewStartedAt = r.ReviewStartedAt,
			ReversedAt = r.ReversedAt, FailureReason = r.FailureReason, ReversalReason = r.ReversalReason,
			AvailableActions = isLegacy ? [] : GetAvailableActions(r.Status),
			Lines = r.Lines.Select(l => new FeeRefundLineDto { Id = l.Id, DueFeeId = l.DueFeeId,
				SourceAllocationId = l.SourceAllocationId, SourceReceiptId = l.SourceReceiptId,
				FeeHeadId = l.FeeHeadId, FeeHeadName = l.FeeHeadName, InstallmentLabel = l.InstallmentLabel,
				SettledPaidAmount = l.SettledPaidAmount, Amount = l.Amount,
				CalculatedAmount = isLegacy ? l.Amount : l.CalculatedAmount,
				ApprovedAmount = isLegacy && r.Status == FeeRefundStatus.Approved ? l.Amount : l.ApprovedAmount,
				ProcessedAmount = isLegacy && r.Status == FeeRefundStatus.Approved ? l.Amount : l.ProcessedAmount,
				ReversedAmount = l.ReversedAmount }).ToList(),
			Settlements = r.Settlements.OrderBy(x => x.AttemptNumber).Select(x => new FeeRefundSettlementDto
			{ Id = x.Id, AttemptNumber = x.AttemptNumber, Status = x.Status, Method = x.Method,
				Amount = x.Amount, SettlementDate = x.SettlementDate, ExternalReference = x.ExternalReference,
				FailureReason = x.FailureReason }).ToList(),
			StudentName = fullName, StudentGrNo = student?.GrNo, StudentRollNo = student?.RollNo,
			StandardId = student?.StandardId, DivisionId = student?.DivisionId,
			StandardName = student?.Standard?.Name, DivisionName = student?.Division?.Name,
			StudentContactNo = student?.ContactDetail?.PrimaryContactNo,
			BoardId = student?.AcademicBoardId, BoardName = student?.AcademicBoard?.Type.ToString(),
			EducationMediumId = student?.AcademicMediumId, EducationMediumName = student?.AcademicMedium?.Name,
		};
	}

	private static string? GetPolicyName(string? snapshotJson)
	{
		if (string.IsNullOrWhiteSpace(snapshotJson)) return null;
		try { return JsonSerializer.Deserialize<RefundPolicySnapshot>(snapshotJson)?.PolicyName; }
		catch (JsonException) { return null; }
	}

	private static List<string> GetAvailableActions(FeeRefundStatus status) => status switch
	{
		FeeRefundStatus.Draft => ["edit", "delete", "submit", "cancel"],
		FeeRefundStatus.Submitted => ["startReview", "approve", "reject", "cancel"],
		FeeRefundStatus.UnderReview => ["approve", "reject", "cancel"],
		FeeRefundStatus.Approved => ["process", "cancel"],
		FeeRefundStatus.Failed => ["retryProcessing", "cancel"],
		FeeRefundStatus.Processed => ["reverse"],
		_ => []
	};

	private static Dictionary<string, object?>? MaskPaymentDetails(Dictionary<string, object?>? source)
	{
		if (source == null) return null;
		var sensitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{ "beneficiaryAccountNumber", "upiId", "chequeNumber", "ddNumber" };
		return source.ToDictionary(x => x.Key, x => sensitive.Contains(x.Key) && x.Value is not null
			? Mask(x.Value.ToString()!) : x.Value);
	}

	private static string Mask(string value) => value.Length <= 4 ? "****" : new string('*', value.Length - 4) + value[^4..];

	private static Dictionary<string, object?>? MapRefundPaymentDetailsToDict(RefundPaymentDetails? details) =>
		details switch
		{
			null => null,
			CashRefundDetails cash => new()
			{
				["refundDate"] = cash.RefundDate.ToString("yyyy-MM-dd"),
				["receivedBy"] = cash.ReceivedBy, ["remarks"] = cash.Remarks,
			},
			ChequeRefundDetails cheque => new()
			{
				["refundDate"] = cheque.ChequeIssueDate.ToString("yyyy-MM-dd"),
				["chequeNumber"] = cheque.ChequeNumber, ["payeeName"] = cheque.PayeeName,
				["sourceBankAccount"] = cheque.SourceBankAccount, ["remarks"] = cheque.Remarks,
			},
			DemandDraftRefundDetails dd => new()
			{
				["issueDate"] = dd.IssueDate.ToString("yyyy-MM-dd"),
				["ddNumber"] = dd.DdNumber, ["bankName"] = dd.BankName,
				["branchName"] = dd.BranchName, ["payeeName"] = dd.PayeeName,
				["sourceBankAccount"] = dd.SourceBankAccount, ["remarks"] = dd.Remarks,
			},
			BankTransferRefundDetails bt => new()
			{
				["refundDate"] = bt.TransferDate.ToString("yyyy-MM-dd"),
				["transactionId"] = bt.TransactionId, ["sourceBankAccount"] = bt.SourceBankAccount,
				["beneficiaryName"] = bt.BeneficiaryName, ["beneficiaryBankName"] = bt.BeneficiaryBankName,
				["beneficiaryAccountNumber"] = bt.BeneficiaryAccountNumber,
				["ifscCode"] = bt.IfscCode, ["remarks"] = bt.Remarks,
			},
			UpiRefundDetails upi => new()
			{
				["refundDate"] = upi.RefundDate.ToString("yyyy-MM-dd"),
				["transactionId"] = upi.TransactionId, ["sourceBankAccount"] = upi.SourceBankAccount,
				["beneficiaryName"] = upi.BeneficiaryName, ["upiId"] = upi.UpiId, ["remarks"] = upi.Remarks,
			},
			_ => null,
		};

	private async Task PopulateRefundDetailsAsync(FeeRefundRequestDto dto, FeeRefundRequest request)
	{
		dto.RefundRequestId = request.Id;

		var lineDueFeeIds = request.Lines.Select(l => l.DueFeeId).ToList();
		var dueFees = await dueFeeRepository.GetListAsync(
			df => df.StudentId == request.StudentId && (df.PaidAmount > 0 || lineDueFeeIds.Contains(df.Id)));

		if (dueFees.Count == 0)
		{
			dto.TotalFeeHeads = 0; dto.TotalPaidAmount = 0; dto.TotalRefundedAmount = 0; dto.FeeHeads = [];
			return;
		}

		var feeHeadIds = dueFees.Select(df => df.FeeHeadId).Distinct().ToList();
		var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
		var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);
		var lineMap = request.Lines.ToDictionary(l => l.DueFeeId, l => l.Amount);

		var grouped = dueFees.GroupBy(df => df.FeeHeadId)
			.Select(g =>
			{
				var items = g.OrderBy(df => df.Year).ThenBy(df => df.Month)
					.Select(df => new RefundMonthItemDto
					{
						InstallmentId = df.Id, Month = df.Month.ToString(),
						Total = (double)df.OriginalAmount, Paid = (double)df.PaidAmount,
						RefundAmount = lineMap.TryGetValue(df.Id, out var amt) ? Convert.ToDouble(amt) : 0,
						Status = (int)df.Status
					}).ToList();
				return new RefundFeeHeadDto
				{
					FeeHeadId = g.Key,
					FeeHeadName = feeHeadMap.TryGetValue(g.Key, out var name) ? name : g.Key.ToString(),
					TotalPaid = items.Sum(i => i.Paid),
					TotalRefunded = (double)g.Sum(df => df.RefundedAmount),
					Items = items
				};
			}).ToList();

		dto.FeeHeads = grouped;
		dto.TotalFeeHeads = grouped.Count;
		dto.TotalPaidAmount = grouped.Sum(fh => fh.TotalPaid);
		dto.TotalRefundedAmount = grouped.Sum(fh => fh.TotalRefunded);
	}

}
