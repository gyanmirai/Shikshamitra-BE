using System;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public static class LeavePolicyProjection
{
	public static LeavePolicyItem ToPolicyItem(LeavePolicy policy) => new()
	{
		Id = policy.Id,
		PolicyTitle = policy.PolicyName,
		LeaveType = new LeaveTypeReference
		{
			Id = policy.LeaveTypeId,
			Name = policy.LeaveType?.Type ?? string.Empty
		},
		AccrualPolicyType = ToAccrualPolicyType(policy.AccrualPolicyType),
		LeaveCount = policy.LeaveCount,
		CarryForward = policy.CarryForward,
		EncashmentAllowed = policy.Encashable,
		EncashmentTiming = ToEncashmentTiming(policy.EncashmentTiming),
		PaidType = ToPaidType(policy.PaidType),
		RequiresApproval = policy.RequiresApproval,
		Description = policy.Description
	};

	public static LeavePolicyDetailResponse ToDetail(LeavePolicy policy) => new()
	{
		Id = policy.Id,
		PolicyTitle = policy.PolicyName,
		LeaveType = new LeaveTypeReference
		{
			Id = policy.LeaveTypeId,
			Name = policy.LeaveType?.Type ?? string.Empty
		},
		AccrualPolicyType = ToAccrualPolicyType(policy.AccrualPolicyType),
		LeaveCount = policy.LeaveCount,
		PaidType = ToPaidType(policy.PaidType),
		CarryForward = policy.CarryForward,
		EncashmentAllowed = policy.Encashable,
		EncashmentTiming = ToEncashmentTiming(policy.EncashmentTiming),
		RequiresApproval = policy.RequiresApproval,
		Description = policy.Description
	};

	public static LeavePolicyResponse ToCreateResponse(LeavePolicy policy) => new()
	{
		Id = policy.Id,
		PolicyTitle = policy.PolicyName,
		LeaveType = policy.LeaveTypeId,
		AccrualPolicyType = ToAccrualPolicyType(policy.AccrualPolicyType),
		LeaveCount = policy.LeaveCount,
		PaidType = ToPaidType(policy.PaidType),
		CarryForward = policy.CarryForward,
		EncashmentAllowed = policy.Encashable,
		EncashmentTiming = ToEncashmentTiming(policy.EncashmentTiming),
		RequiresApproval = policy.RequiresApproval,
		Description = policy.Description,
		CreatedAt = policy.CreationTime
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
