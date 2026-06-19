using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Students;
using System;
using System.Collections.Generic;
using System.Linq;
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
		var fullName = student is null ? string.Empty
			: string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
				.Where(p => !string.IsNullOrWhiteSpace(p)));

		return new FeeRefundRequestDto
		{
			Id = r.Id, StudentId = r.StudentId, AcademicYearId = r.AcademicYearId,
			Status = r.Status, VoucherNumber = r.VoucherNumber, TotalAmount = r.TotalAmount,
			RefundMethod = r.RefundMethod, PaymentDetails = MapRefundPaymentDetailsToDict(r.RefundPaymentDetails),
			Reason = r.Reason, ApproverNotes = r.ApproverNotes,
			ProcessedByUserId = r.ProcessedByUserId, ProcessedAt = r.ProcessedAt,
			CreationTime = r.CreationTime,
			Lines = r.Lines.Select(l => new FeeRefundLineDto { Id = l.Id, DueFeeId = l.DueFeeId, Amount = l.Amount }).ToList(),
			StudentName = fullName, StudentGrNo = student?.GrNo, StudentRollNo = student?.RollNo,
			StandardId = student?.StandardId, DivisionId = student?.DivisionId,
			StandardName = student?.Standard?.Name, DivisionName = student?.Division?.Name,
			StudentContactNo = student?.ContactDetail?.PrimaryContactNo,
			BoardId = student?.AcademicBoardId, BoardName = student?.AcademicBoard?.Type.ToString(),
			EducationMediumId = student?.AcademicMediumId, EducationMediumName = student?.AcademicMedium?.Name,
		};
	}

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
						Total = df.OriginalAmount, Paid = df.PaidAmount,
						RefundAmount = lineMap.TryGetValue(df.Id, out var amt) ? amt : 0,
						Status = (int)df.Status
					}).ToList();
				return new RefundFeeHeadDto
				{
					FeeHeadId = g.Key,
					FeeHeadName = feeHeadMap.TryGetValue(g.Key, out var name) ? name : g.Key.ToString(),
					TotalPaid = items.Sum(i => i.Paid),
					TotalRefunded = g.Sum(df => df.RefundedAmount),
					Items = items
				};
			}).ToList();

		dto.FeeHeads = grouped;
		dto.TotalFeeHeads = grouped.Count;
		dto.TotalPaidAmount = grouped.Sum(fh => fh.TotalPaid);
		dto.TotalRefundedAmount = grouped.Sum(fh => fh.TotalRefunded);
	}

	private bool IsPrivilegedUser() =>
		CurrentUser.IsInRole("admin") || CurrentUser.IsInRole("Finance Officer");
}
