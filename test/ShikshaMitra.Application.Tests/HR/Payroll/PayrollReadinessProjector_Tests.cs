using System;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollReadinessProjector_Tests
{
	[Theory]
	[InlineData(IssueCodes.EsiInsuranceNumberRequired)]
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
	[InlineData("WITHIN_WAGE_CEILING", null, false)]
	[InlineData("WITHIN_WAGE_CEILING", "1234567890", true)]
	[InlineData("ABOVE_WAGE_CEILING", null, true)]
	public void Readiness_checks_resolved_coverage_even_without_recorded_issues(string decision, string? number, bool canLock)
	{
		var summary = PayrollReadinessProjector.BuildSummary("2026-10",
			[new() { ReviewStatus = ReviewStatuses.Reviewed, EmployeeStateInsuranceRuleId = Guid.NewGuid(),
				EmployeeStateInsuranceCoverageDecision = decision, EmployeeStateInsuranceNumber = number }], false);
		summary.CanLock.ShouldBe(canLock);
	}
}