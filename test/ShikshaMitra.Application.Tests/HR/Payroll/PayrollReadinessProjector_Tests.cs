using System;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollReadinessProjector_Tests
{
	[Theory]
	[InlineData(IssueCodes.EsiWageClassificationRequired)]
	[InlineData(IssueCodes.EsiEnrollmentReviewRequired)]
	[InlineData(IssueCodes.CompensationTargetVariance)]
	public void Reviewed_row_with_lock_blocker_is_not_ready(string code)
	{
		var summary = PayrollReadinessProjector.BuildSummary("2026-10",
			[new() { ReviewStatus = ReviewStatuses.Reviewed, IssueCodes = [code] }], false);
		summary.BlockingIssues.ShouldContain(code);
		summary.CanLock.ShouldBeFalse();
		summary.RecommendedActions.ShouldContain("review-payroll-rows");
	}

	[Theory]
	[InlineData("WITHIN_WAGE_CEILING", null, true)]
	[InlineData("WITHIN_WAGE_CEILING", "1234567890", true)]
	[InlineData("ABOVE_WAGE_CEILING", null, true)]
	public void Registration_number_does_not_change_readiness_when_no_blockers_are_recorded(string decision, string? number, bool canLock)
	{
		var summary = PayrollReadinessProjector.BuildSummary("2026-10",
			[new() { ReviewStatus = ReviewStatuses.Reviewed, EmployeeStateInsuranceRuleId = Guid.NewGuid(),
				EmployeeStateInsuranceCoverageDecision = decision, EmployeeStateInsuranceNumber = number }], false);
		summary.CanLock.ShouldBe(canLock);
	}

	[Theory]
	[InlineData(IssueCodes.NoWorkingDays)]
	[InlineData(IssueCodes.AttendanceSalaryDeductionConfiguration)]
	[InlineData(IssueCodes.NoAttendance)]
	[InlineData(IssueCodes.MissingSalaryStructure)]
	[InlineData(IssueCodes.NegativeNetPay)]
	[InlineData(IssueCodes.PayrollPolicyConfiguration)]
	[InlineData("PF_WAGE_BASIS_REQUIRED")]
	[InlineData("PF_MEMBERSHIP_REVIEW_REQUIRED")]
	[InlineData("PF_ENROLLMENT_REQUIRED")]
	[InlineData("PF_CONSENT_REQUIRED")]
	[InlineData("PF_ESTABLISHMENT_NOT_COVERED")]
	[InlineData("ESI_BASIC_COMPONENT_REQUIRED")]
	[InlineData("ESI_COMPONENT_REQUIRED")]
	[InlineData(IssueCodes.EsiEstablishmentInactive)]
	[InlineData(IssueCodes.CompensationRevisionRequired)]
	public void Readiness_retains_backend_lock_blockers_after_review(string code)
	{
		PayrollLockPreconditionGuard.IsBlockingLockIssue(code).ShouldBeTrue();
		var summary = PayrollReadinessProjector.BuildSummary("2026-10",
			[new() { ReviewStatus = ReviewStatuses.Reviewed, IssueCodes = [code] }], false);
		summary.CanLock.ShouldBeFalse();
		summary.BlockingIssues.ShouldContain(code);
	}

	[Theory]
	[InlineData("PF_NUMBER_MISSING")]
	[InlineData("ESI_NUMBER_MISSING")]
	[InlineData(IssueCodes.EsiInsuranceNumberRequired)]
	public void Registration_warnings_remain_visible_without_blocking_reviewed_payroll(string code)
	{
		var summary = PayrollReadinessProjector.BuildSummary("2026-10",
			[new() { ReviewStatus = ReviewStatuses.Reviewed, IssueCodes = [code] }], false);
		summary.CanLock.ShouldBeTrue();
		summary.BlockingIssues.ShouldBeEmpty();
	}
}
