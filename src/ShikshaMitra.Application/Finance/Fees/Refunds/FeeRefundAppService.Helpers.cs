using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public partial class FeeRefundAppService
{
	private sealed class RefundStudentSummary
	{
		public Guid Id { get; init; }
		public string? FirstName { get; init; }
		public string? MiddleName { get; init; }
		public string? LastName { get; init; }
		public int? GrNo { get; init; }
		public int? RollNo { get; init; }
		public Guid? StandardId { get; init; }
		public Guid? DivisionId { get; init; }
		public string? StandardName { get; init; }
		public string? DivisionName { get; init; }
		public string? PrimaryContactNo { get; init; }
		public Guid? AcademicBoardId { get; init; }
		public string? BoardName { get; init; }
		public Guid AcademicMediumId { get; init; }
		public string? AcademicMediumName { get; init; }
	}

	private async Task<Dictionary<Guid, RefundStudentSummary>> BuildStudentMapAsync(IEnumerable<Guid?> studentIds)
	{
		var ids = studentIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
		if (ids.Count == 0) return [];
		var students = await AsyncExecuter.ToListAsync((await studentRepository.GetQueryableAsync())
			.Where(s => ids.Contains(s.Id))
			.Select(s => new RefundStudentSummary
			{
				Id = s.Id, FirstName = s.FirstName, MiddleName = s.MiddleName,
				LastName = s.LastName, GrNo = s.GrNo, RollNo = s.RollNo,
				StandardId = s.StandardId, DivisionId = s.DivisionId,
				StandardName = s.Standard == null ? null : s.Standard.Name,
				DivisionName = s.Division == null ? null : s.Division.Name,
				PrimaryContactNo = s.ContactDetail == null ? null : s.ContactDetail.PrimaryContactNo,
				AcademicBoardId = s.AcademicBoardId,
				BoardName = s.AcademicBoard == null ? null : s.AcademicBoard.Type.ToString(),
				AcademicMediumId = s.AcademicMediumId,
				AcademicMediumName = s.AcademicMedium == null ? null : s.AcademicMedium.Name
			}));
		return students.ToDictionary(s => s.Id);
	}

	private async Task<Dictionary<Guid, FinanceFeeAccount>> BuildAccountMapAsync(IEnumerable<Guid?> accountIds)
	{
		var ids = accountIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
		if (ids.Count == 0) return [];
		return (await accountRepository.GetListAsync(x => ids.Contains(x.Id))).ToDictionary(x => x.Id);
	}

	private async Task<Dictionary<Guid, AcademicYear>> BuildYearMapAsync(IEnumerable<Guid> yearIds)
	{
		var ids = yearIds.Distinct().ToList();
		if (ids.Count == 0) return [];
		return (await academicYearRepository.GetListAsync(x => ids.Contains(x.Id))).ToDictionary(x => x.Id);
	}

	private static FeeRefundRequestDto MapToDto(FeeRefundRequest r, Dictionary<Guid, RefundStudentSummary> studentMap,
		Dictionary<Guid, FinanceFeeAccount> accountMap, Dictionary<Guid, AcademicYear> yearMap)
	{
		var student = r.StudentId.HasValue ? studentMap.GetValueOrDefault(r.StudentId.Value) : null;
		var account = r.FinanceFeeAccountId.HasValue ? accountMap.GetValueOrDefault(r.FinanceFeeAccountId.Value) : null;
		var year = yearMap.GetValueOrDefault(r.AcademicYearId);
		var isLegacy = !r.RefundPolicyVersionId.HasValue && !r.ConcessionRequestId.HasValue;
		var policy = string.IsNullOrWhiteSpace(r.PolicySnapshotJson)
			? null : JsonSerializer.Deserialize<RefundPolicySnapshot>(r.PolicySnapshotJson);
		var legacyFinancialAmount = isLegacy && r.Status == FeeRefundStatus.Approved ? r.TotalAmount : 0m;
		var fullName = student is null ? account?.ApplicantName ?? string.Empty
			: string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
				.Where(p => !string.IsNullOrWhiteSpace(p)));

		return new FeeRefundRequestDto
		{
			Id = r.Id, ConcurrencyStamp = r.ConcurrencyStamp, StudentId = r.StudentId, FinanceFeeAccountId = r.FinanceFeeAccountId, AcademicYearId = r.AcademicYearId,
			AcademicYearName = year?.Name,
			AcademicYearStartDate = year == null ? null : DateTime.SpecifyKind(year.StartDate, DateTimeKind.Utc),
			AcademicYearEndDate = year == null ? null : DateTime.SpecifyKind(year.EndDate, DateTimeKind.Utc),
			Status = r.Status, WorkflowVersion = r.WorkflowVersion,
			ReplacesRefundRequestId = r.ReplacesRefundRequestId,
			VoucherNumber = r.VoucherNumber, TotalAmount = r.TotalAmount,
			CalculatedAmount = isLegacy ? r.TotalAmount : r.CalculatedAmount,
			StudentFeeStructureId = r.StudentFeeStructureId, RefundPolicyId = r.RefundPolicyId,
			RefundPolicyVersionId = r.RefundPolicyVersionId, RefundPolicyName = policy?.PolicyName,
			ConcessionRequestId = r.ConcessionRequestId,
			OriginalReceiptId = r.OriginalReceiptId,
			OriginalReceiptNumber = r.OriginalReceiptNumber, Currency = string.IsNullOrWhiteSpace(r.Currency) ? "INR" : r.Currency,
			ApprovedAmount = isLegacy ? legacyFinancialAmount : r.ApprovedAmount,
			ProcessedAmount = isLegacy ? legacyFinancialAmount : r.ProcessedAmount, ReversedAmount = r.ReversedAmount,
			ExpenseCategoryId = r.ExpenseCategoryId, PayingBankAccountId = r.PayingBankAccountId,
            ExpenseCategoryName = r.ExpenseCategoryName, PayingBankLabel = r.PayingBankLabel,
            DefaultExpenseCategoryId = policy?.ExpenseCategoryId,
            DefaultPayingBankAccountId = policy?.DefaultPayingBankAccountId,
            AllowedPayoutMethods = (r.ConcessionRequestId.HasValue
                ? Enum.GetValues<RefundPayoutMethod>()
                : policy?.AllowedPayoutMethods ?? []).Select(ToPayoutMethodLabel).ToList(),
            RefundMethod = r.RefundMethod, PaymentDetails = MaskPaymentDetails(MapRefundPaymentDetailsToDict(r.RefundPaymentDetails)),
			Reason = r.Reason, ApproverNotes = r.ApproverNotes,
			ProcessedByUserId = r.ProcessedByUserId, ProcessedAt = r.ProcessedAt,
			CreationTime = r.CreationTime, SubmittedAt = r.SubmittedAt, ReviewStartedAt = r.ReviewStartedAt,
			ReversedAt = r.ReversedAt, FailureReason = r.FailureReason, ReversalReason = r.ReversalReason,
			AvailableActions = isLegacy ? [] : GetAvailableActions(r),
			Lines = r.Lines.Select(l => new FeeRefundLineDto { Id = l.Id, DueFeeId = l.DueFeeId, ChargeId = l.ChargeId, LateFeeDueFeeId = l.LateFeeDueFeeId,
				SourceAllocationId = l.SourceAllocationId, SourceReceiptId = l.SourceReceiptId,
				FeeHeadId = l.FeeHeadId, FeeHeadName = l.FeeHeadName, InstallmentLabel = l.InstallmentLabel,
				SettledPaidAmount = l.SettledPaidAmount, Amount = l.Amount,
				CalculatedAmount = isLegacy ? l.Amount : l.CalculatedAmount,
				ApprovedAmount = isLegacy && r.Status == FeeRefundStatus.Approved ? l.Amount : l.ApprovedAmount,
				ProcessedAmount = isLegacy && r.Status == FeeRefundStatus.Approved ? l.Amount : l.ProcessedAmount,
				ReversedAmount = l.ReversedAmount }).ToList(),
			Settlements = r.Settlements.OrderBy(x => x.AttemptNumber).Select(x => new FeeRefundSettlementDto
			{ Id = x.Id, AttemptNumber = x.AttemptNumber, Status = x.Status, Method = x.Method,
				Amount = x.Amount, SettlementDate = x.SettlementDate,
				ExternalReference = x.ExternalReference == null ? null : Mask(x.ExternalReference),
				FailureReason = x.FailureReason, BankAccountId = x.BankAccountId,
				EvidenceReference = x.EvidenceReference, VerifiedByUserId = x.VerifiedByUserId,
				VerifiedAt = x.VerifiedAt }).ToList(),
			StudentName = fullName, StudentGrNo = student?.GrNo, StudentRollNo = student?.RollNo,
			StandardId = student?.StandardId ?? account?.StandardId, DivisionId = student?.DivisionId ?? account?.DivisionId,
			StandardName = student?.StandardName, DivisionName = student?.DivisionName,
			StudentContactNo = student?.PrimaryContactNo,
			BoardId = student?.AcademicBoardId ?? account?.BoardId, BoardName = student?.BoardName,
			EducationMediumId = student?.AcademicMediumId ?? account?.EducationMediumId, EducationMediumName = student?.AcademicMediumName,
		};
	}

	private static List<string> GetAvailableActions(FeeRefundRequest request)
	{
		if (request.WorkflowVersion == 1)
			return request.Status switch
			{
				FeeRefundStatus.Submitted when request.CanEdit => ["edit", "approve", "reject"],
				FeeRefundStatus.Submitted => ["approve", "reject"],
				FeeRefundStatus.Approved => ["recordPayout"],
				FeeRefundStatus.Failed => ["createReplacement"],
				FeeRefundStatus.Processed when request.RefundMethod != "Cash" => ["returnPayout"],
				_ => []
			};
		return request.Status switch
		{
		FeeRefundStatus.Draft => ["edit", "delete", "submit", "cancel"],
		FeeRefundStatus.Submitted => ["startReview", "approve", "reject", "cancel"],
		FeeRefundStatus.UnderReview => ["approve", "reject", "cancel"],
		FeeRefundStatus.Approved => ["process", "cancel"],
		FeeRefundStatus.Failed => ["retryProcessing", "cancel"],
		FeeRefundStatus.Processed => ["reverse"],
		_ => []
		};
	}

	private async Task FilterAvailableActionsAsync(FeeRefundRequest request, FeeRefundRequestDto dto)
	{
		var required = new Dictionary<string, string>
		{
			["edit"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Edit,
			["approve"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve,
			["reject"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reject,
			["recordPayout"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Process,
			["confirmPayout"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.VerifyPayout,
			["failPayout"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.FailPayout,
			["createReplacement"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add,
			["returnPayout"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reverse,
		};
		var filtered = new List<string>();
		foreach (var action in dto.AvailableActions)
		{
			if (action == "edit" && !request.CanEdit) continue;
			if (!required.TryGetValue(action, out var permission) ||
				!await AuthorizationService.IsGrantedAsync(permission)) continue;
			if ((action == "approve" || action == "reject") &&
				(request.SubmittedByUserId == CurrentUser.Id || request.CreatorId == CurrentUser.Id)) continue;
			if ((action == "confirmPayout" || action == "failPayout") &&
				request.Settlements.LastOrDefault()?.InitiatedByUserId == CurrentUser.Id &&
				(action == "failPayout" || request.RefundMethod != "Cash")) continue;
			filtered.Add(action);
		}
		dto.AvailableActions = filtered;
	}

    private static Dictionary<string, object?>? PreserveMaskedPaymentDetails(FeeRefundRequest request,
        string? method, Dictionary<string, object?>? input)
    {
        if (input == null || method != request.RefundMethod) return input;
        var saved = MapRefundPaymentDetailsToDict(request.RefundPaymentDetails);
        if (saved == null) return input;
        var result = new Dictionary<string, object?>(input);
        foreach (var key in new[] { "beneficiaryAccountNumber", "upiId", "chequeNumber", "ddNumber" })
            if (saved.TryGetValue(key, out var original) && original != null &&
                result.TryGetValue(key, out var submitted) && submitted?.ToString() == Mask(original.ToString()!))
                result[key] = original;
        return result;
    }

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
				["refundDate"] = cash.RefundDate == default ? null : cash.RefundDate.ToString("yyyy-MM-dd"),
				["receivedBy"] = cash.ReceivedBy, ["remarks"] = cash.Remarks,
			},
			ChequeRefundDetails cheque => new()
			{
				["refundDate"] = cheque.ChequeIssueDate == default ? null : cheque.ChequeIssueDate.ToString("yyyy-MM-dd"),
				["chequeNumber"] = cheque.ChequeNumber, ["payeeName"] = cheque.PayeeName,
				["sourceBankAccount"] = cheque.SourceBankAccount, ["remarks"] = cheque.Remarks,
			},
			DemandDraftRefundDetails dd => new()
			{
				["issueDate"] = dd.IssueDate == default ? null : dd.IssueDate.ToString("yyyy-MM-dd"),
				["ddNumber"] = dd.DdNumber, ["bankName"] = dd.BankName,
				["branchName"] = dd.BranchName, ["payeeName"] = dd.PayeeName,
				["sourceBankAccount"] = dd.SourceBankAccount, ["remarks"] = dd.Remarks,
			},
			BankTransferRefundDetails bt => new()
			{
				["refundDate"] = bt.TransferDate == default ? null : bt.TransferDate.ToString("yyyy-MM-dd"),
				["transactionId"] = bt.TransactionId, ["sourceBankAccount"] = bt.SourceBankAccount,
				["beneficiaryName"] = bt.BeneficiaryName, ["beneficiaryBankName"] = bt.BeneficiaryBankName,
				["beneficiaryAccountNumber"] = bt.BeneficiaryAccountNumber,
				["ifscCode"] = bt.IfscCode, ["remarks"] = bt.Remarks,
			},
			UpiRefundDetails upi => new()
			{
				["refundDate"] = upi.RefundDate == default ? null : upi.RefundDate.ToString("yyyy-MM-dd"),
				["transactionId"] = upi.TransactionId, ["sourceBankAccount"] = upi.SourceBankAccount,
				["beneficiaryName"] = upi.BeneficiaryName, ["upiId"] = upi.UpiId, ["remarks"] = upi.Remarks,
			},
			_ => null,
		};

	private async Task PopulateRefundDetailsAsync(FeeRefundRequestDto dto, FeeRefundRequest request)
	{
		var dueIds = request.Lines.Where(line => line.DueFeeId.HasValue)
			.Select(line => line.DueFeeId!.Value).Distinct().ToArray();
		Dictionary<Guid, RefundDueDisplayFacts> facts = [];
		if (dueIds.Length > 0)
		{
			var dues = await dueFeeRepository.GetListAsync(due => Enumerable.Contains(dueIds, due.Id) &&
				(request.FinanceFeeAccountId.HasValue ? due.FinanceFeeAccountId == request.FinanceFeeAccountId
					: due.StudentId == request.StudentId &&
					(!request.StudentFeeStructureId.HasValue || due.StudentFeeStructureId == request.StudentFeeStructureId)));
			facts = dues.ToDictionary(due => due.Id,
				due => new RefundDueDisplayFacts(due.OriginalAmount, (int)due.Status));
		}
		RefundRequestDetailProjection.Populate(dto, facts);
	}

}
