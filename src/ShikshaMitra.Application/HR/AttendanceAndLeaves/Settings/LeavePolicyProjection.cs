using System;
using System.Linq;
using ShikshaMitra.HR.Policies;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public static class LeavePolicyProjection
{
	public static LeavePolicyItem ToPolicyItem(LeavePolicy policy) => new()
	{
		Id = policy.Id,
		PolicyTitle = policy.PolicyName,
		Status = policy.Status.ToString(),
		EffectiveFrom = policy.EffectiveFrom,
		EffectiveTo = policy.EffectiveTo,
		RuleCount = policy.Rules.Count(rule => rule.IsActive),
		Description = policy.Description,
		IsDefault = policy.IsDefault,
		Rules = policy.Rules.Select(ToRuleItem).ToList()
	};

	public static LeavePolicyDetailResponse ToDetail(LeavePolicy policy) => new()
	{
		Id = policy.Id,
		PolicyTitle = policy.PolicyName,
		Status = policy.Status.ToString(),
		EffectiveFrom = policy.EffectiveFrom,
		EffectiveTo = policy.EffectiveTo,
		RuleCount = policy.Rules.Count(rule => rule.IsActive),
		Description = policy.Description,
		IsDefault = policy.IsDefault,
		Rules = policy.Rules.Select(ToRuleItem).ToList()
	};

	public static LeavePolicyResponse ToCreateResponse(LeavePolicy policy)
	{
		var item = ToPolicyItem(policy);
		return new LeavePolicyResponse
		{
			Id = item.Id,
			PolicyTitle = item.PolicyTitle,
			Status = item.Status,
			EffectiveFrom = item.EffectiveFrom,
			EffectiveTo = item.EffectiveTo,
			RuleCount = item.RuleCount,
			Description = item.Description,
			IsDefault = item.IsDefault,
			Rules = item.Rules,
			CreatedAt = policy.CreationTime
		};
	}

	public static LeavePolicyRuleItem ToRuleItem(LeavePolicyRule rule) => new()
	{
		Id = rule.Id,
		LeaveType = new LeaveTypeReference
		{
			Id = rule.LeaveTypeId,
			Name = rule.LeaveType?.Type ?? string.Empty,
			Color = rule.LeaveType?.Color
		},
		AccrualPolicyType = ToAccrualPolicyType(rule.AccrualPolicyType),
		LeaveCount = rule.LeaveCount,
		CarryForward = rule.CarryForward,
		CarryForwardLimit = rule.CarryForwardLimit,
		EncashmentAllowed = rule.Encashable,
		EncashmentTiming = ToEncashmentTiming(rule.EncashmentTiming),
		PaidType = ToPaidType(rule.PaidType),
		RequiresApproval = rule.RequiresApproval,
		MinNoticeDays = rule.MinNoticeDays,
		MaxDaysPerRequest = rule.MaxDaysPerRequest,
		AllowHalfDay = rule.AllowHalfDay,
		AttachmentRequiredAfterDays = rule.AttachmentRequiredAfterDays,
		AllowNegativeBalance = rule.AllowNegativeBalance,
		IsActive = rule.IsActive
	};

	public static LeavePolicyRuleInput ToRuleInput(LeavePolicyRuleRequest request) => new(
		request.LeaveTypeId,
		ParseAccrualPolicyType(request.AccrualPolicyType),
		request.LeaveCount,
		ParsePaidType(request.PaidType),
		request.CarryForward,
		request.CarryForwardLimit,
		request.EncashmentAllowed,
		ParseEncashmentTiming(request.EncashmentTiming),
		request.RequiresApproval,
		request.MinNoticeDays,
		request.MaxDaysPerRequest,
		request.AllowHalfDay,
		request.AttachmentRequiredAfterDays,
		request.AllowNegativeBalance,
		request.IsActive);

	public static HrPolicyStatus ParseStatus(string status) => status switch
	{
		"Draft" => HrPolicyStatus.Draft,
		"Archived" => HrPolicyStatus.Archived,
		_ => HrPolicyStatus.Active
	};

	public static LeavePaidType ParsePaidType(string paidType) =>
		string.Equals(paidType, "Paid", StringComparison.OrdinalIgnoreCase)
			? LeavePaidType.Paid
			: LeavePaidType.Unpaid;

	public static string ToPaidType(LeavePaidType paidType) => paidType == LeavePaidType.Paid ? "Paid" : "Unpaid";

	public static LeaveAccrualPolicyType ParseAccrualPolicyType(string value) =>
		string.Equals(value, "Yearly", StringComparison.OrdinalIgnoreCase)
			? LeaveAccrualPolicyType.Yearly
			: LeaveAccrualPolicyType.Monthly;

	public static string ToAccrualPolicyType(LeaveAccrualPolicyType value) =>
		value == LeaveAccrualPolicyType.Yearly ? "Yearly" : "Monthly";

	public static LeaveEncashmentTiming? ParseEncashmentTiming(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		return value switch
		{
			"next_payroll" => LeaveEncashmentTiming.NextPayroll,
			"end_of_year" => LeaveEncashmentTiming.EndOfYear,
			_ => null
		};
	}

	public static string? ToEncashmentTiming(LeaveEncashmentTiming? value) => value switch
	{
		LeaveEncashmentTiming.NextPayroll => "next_payroll",
		LeaveEncashmentTiming.EndOfYear => "end_of_year",
		_ => null
	};
}
