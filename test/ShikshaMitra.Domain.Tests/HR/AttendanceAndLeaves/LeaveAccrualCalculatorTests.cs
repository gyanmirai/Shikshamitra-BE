using System;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Policies;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveAccrualCalculatorTests
{
	[Fact]
	public void Yearly_Grant_Uses_Full_Annual_Entitlement_After_Eligibility_Start()
	{
		var policy = Policy();
		var rule = Rule(policy.Id, LeaveAccrualPolicyType.Yearly, leaveCount: 12);

		var accrued = LeaveAccrualCalculator.CalculateAccruedDays(
			policy,
			rule,
			new DateTime(2026, 4, 1),
			new DateTime(2027, 3, 31),
			new DateOnly(2026, 7, 15),
			new DateTime(2026, 7, 15));

		Assert.Equal(12m, accrued);
	}

	[Fact]
	public void Yearly_Grant_Is_Zero_Before_Employee_Is_Eligible()
	{
		var policy = Policy();
		var rule = Rule(policy.Id, LeaveAccrualPolicyType.Yearly, leaveCount: 12);

		var accrued = LeaveAccrualCalculator.CalculateAccruedDays(
			policy,
			rule,
			new DateTime(2026, 4, 1),
			new DateTime(2027, 3, 31),
			new DateOnly(2026, 7, 15),
			new DateTime(2026, 6, 30));

		Assert.Equal(0m, accrued);
	}

	[Fact]
	public void Monthly_Accrual_Uses_Eligible_Months_From_Latest_Start_Without_Day_Prorating()
	{
		var policy = Policy(effectiveFrom: new DateTime(2026, 5, 10));
		var rule = Rule(policy.Id, LeaveAccrualPolicyType.Monthly, leaveCount: 12);

		var accrued = LeaveAccrualCalculator.CalculateAccruedDays(
			policy,
			rule,
			new DateTime(2026, 4, 1),
			new DateTime(2027, 3, 31),
			new DateOnly(2026, 6, 30),
			new DateTime(2026, 8, 1));

		Assert.Equal(3m, accrued);
	}

	[Fact]
	public void Monthly_Accrual_Is_Capped_By_Policy_Effective_To()
	{
		var policy = Policy(
			effectiveFrom: new DateTime(2026, 5, 1),
			effectiveTo: new DateTime(2026, 7, 15));
		var rule = Rule(policy.Id, LeaveAccrualPolicyType.Monthly, leaveCount: 12);

		var accrued = LeaveAccrualCalculator.CalculateAccruedDays(
			policy,
			rule,
			new DateTime(2026, 4, 1),
			new DateTime(2027, 3, 31),
			new DateOnly(2026, 4, 1),
			new DateTime(2026, 12, 1));

		Assert.Equal(3m, accrued);
	}

	[Fact]
	public void Unlimited_Unpaid_Returns_Null_Accrued_Days()
	{
		var policy = Policy();
		var rule = Rule(
			policy.Id,
			LeaveAccrualPolicyType.Yearly,
			leaveCount: null,
			paidType: LeavePaidType.Unpaid,
			entitlementMode: LeaveEntitlementMode.Unlimited,
			allowNegativeBalance: true);

		var accrued = LeaveAccrualCalculator.CalculateAccruedDays(
			policy,
			rule,
			new DateTime(2026, 4, 1),
			new DateTime(2027, 3, 31),
			new DateOnly(2026, 4, 1),
			new DateTime(2026, 8, 1));

		Assert.Null(accrued);
	}

	private static LeavePolicy Policy(
		DateTime? effectiveFrom = null,
		DateTime? effectiveTo = null) =>
		new(
			Guid.NewGuid(),
			tenantId: null,
			Guid.NewGuid(),
			"Annual Leave Policy",
			effectiveFrom ?? new DateTime(2026, 4, 1),
			effectiveTo,
			HrPolicyStatus.Active);

	private static LeavePolicyRule Rule(
		Guid policyId,
		LeaveAccrualPolicyType accrualPolicyType,
		int? leaveCount,
		LeavePaidType paidType = LeavePaidType.Paid,
		LeaveEntitlementMode entitlementMode = LeaveEntitlementMode.Limited,
		bool allowNegativeBalance = false) =>
		new(
			Guid.NewGuid(),
			tenantId: null,
			policyId,
			paidType,
			entitlementMode,
			accrualPolicyType,
			leaveCount,
			carryForward: false,
			carryForwardLimit: null,
			encashable: false,
			encashmentTiming: null,
			requiresApproval: true,
			minNoticeDays: 0,
			maxDaysPerRequest: 30,
			allowHalfDay: true,
			attachmentRequiredAfterDays: null,
			allowNegativeBalance);
}
