using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.YearEndSettlements;

public class LeaveYearEndSettlementItem : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid RunId { get; private set; }
	public Guid SourceAcademicYearId { get; private set; }
	public Guid? TargetAcademicYearId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public Guid SourceLeavePolicyId { get; private set; }
	public Guid SourceLeavePolicyRuleId { get; private set; }
	public Guid? TargetLeavePolicyRuleId { get; private set; }
	public decimal AccruedDays { get; private set; }
	public decimal CarryInDays { get; private set; }
	public decimal ApprovedUsedDays { get; private set; }
	public decimal PendingDays { get; private set; }
	public decimal UnusedDays { get; private set; }
	public decimal CarriedDays { get; private set; }
	public decimal EncashedDays { get; private set; }
	public decimal ForfeitedDays { get; private set; }
	public string? PayrollMonth { get; private set; }
	public decimal Amount { get; private set; }
	public LeaveYearEndSettlementItemStatus Status { get; private set; }
	public string IssueCodesJson { get; private set; } = "[]";
	public Guid? PayrollAdjustmentId { get; private set; }

	private LeaveYearEndSettlementItem()
	{
	}

	public LeaveYearEndSettlementItem(
		Guid id,
		Guid? tenantId,
		Guid runId,
		Guid sourceAcademicYearId,
		Guid? targetAcademicYearId,
		Guid employeeId,
		Guid sourceLeavePolicyId,
		Guid sourceLeavePolicyRuleId) : base(id)
	{
		TenantId = tenantId;
		RunId = runId;
		SourceAcademicYearId = sourceAcademicYearId;
		TargetAcademicYearId = targetAcademicYearId;
		EmployeeId = employeeId;
		SourceLeavePolicyId = sourceLeavePolicyId;
		SourceLeavePolicyRuleId = sourceLeavePolicyRuleId;
		Status = LeaveYearEndSettlementItemStatus.Draft;
	}

	public void UpdateDraft(
		Guid? targetAcademicYearId,
		Guid? targetLeavePolicyRuleId,
		decimal accruedDays,
		decimal carryInDays,
		decimal approvedUsedDays,
		decimal pendingDays,
		decimal unusedDays,
		decimal carriedDays,
		decimal encashedDays,
		decimal forfeitedDays,
		string? payrollMonth,
		decimal amount,
		IEnumerable<string> issueCodes)
	{
		TargetAcademicYearId = targetAcademicYearId;
		TargetLeavePolicyRuleId = targetLeavePolicyRuleId;
		AccruedDays = accruedDays;
		CarryInDays = carryInDays;
		ApprovedUsedDays = approvedUsedDays;
		PendingDays = pendingDays;
		UnusedDays = unusedDays;
		CarriedDays = carriedDays;
		EncashedDays = encashedDays;
		ForfeitedDays = forfeitedDays;
		PayrollMonth = string.IsNullOrWhiteSpace(payrollMonth) ? null : payrollMonth;
		Amount = amount;
		SetIssueCodes(issueCodes);
		Status = HasIssues ? LeaveYearEndSettlementItemStatus.Blocked : LeaveYearEndSettlementItemStatus.Draft;
		PayrollAdjustmentId = null;
	}

	public void SetIssueCodes(IEnumerable<string> issueCodes)
	{
		var normalized = issueCodes
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
			.ToList();
		IssueCodesJson = JsonSerializer.Serialize(normalized);
		Status = Status == LeaveYearEndSettlementItemStatus.Finalized
			? Status
			: normalized.Count > 0
				? LeaveYearEndSettlementItemStatus.Blocked
				: LeaveYearEndSettlementItemStatus.Draft;
	}

	public IReadOnlyList<string> GetIssueCodes() =>
		JsonSerializer.Deserialize<List<string>>(IssueCodesJson) ?? new List<string>();

	public bool HasIssues => GetIssueCodes().Count > 0;

	public void MarkFinalized(Guid? payrollAdjustmentId)
	{
		PayrollAdjustmentId = payrollAdjustmentId;
		Status = LeaveYearEndSettlementItemStatus.Finalized;
	}
}
