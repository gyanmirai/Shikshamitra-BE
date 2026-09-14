using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Statutory;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class EsiStatutoryPreviewProjection_Tests
{
	[Fact]
	public void Should_trim_search_cap_page_size_and_reject_unsupported_sort()
	{
		var input = new StatutoryImpactPreviewRequestDto
		{
			MonthYear = "08-2026",
			Search = "  Patel  ",
			PageSize = 500,
			SortBy = "unsupported"
		};

		input.Search.ShouldBe("Patel");
		input.PageSize.ShouldBe(100);
		new StatutoryImpactPreviewRequestDtoValidator().Validate(input).IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Should_filter_sort_and_page_without_changing_full_summary()
	{
		var summary = new StatutoryImpactSummaryDto
		{
			EmployeeCount = 3,
			CoveredEmployeeCount = 2,
			EmployeeContributionTotal = 240m,
			EmployerContributionTotal = 1_040m
		};
		var calculation = new EsiStatutoryPreviewCalculation("08-2026",
		[
			Employee("3", "Zara", true, 12_800m, "Covered"),
			Employee("2", "Bhavin", false, 22_400m, "NotCovered"),
			Employee("1", "Aarav", true, 14_400m, "RegistrationPending",
				EsiStatutoryPresentation.CreateIssue(EsiStatutoryCalculator.InsuranceNumberIssue))
		], summary, new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc), new string('A', 64));

		var result = EsiStatutoryPreviewReader.Project(calculation, new StatutoryImpactPreviewRequestDto
		{
			MonthYear = "08-2026",
			Coverage = "covered",
			Readiness = "needsAttention",
			EnrollmentStatuses = ["RegistrationPending"],
			Page = 0,
			PageSize = 10,
			SortBy = "statutoryWages",
			SortDirection = "desc"
		});

		result.TotalCount.ShouldBe(1);
		result.Items.Single().EmployeeName.ShouldBe("Aarav");
		result.Summary.ShouldBeSameAs(summary);
		result.Summary.EmployeeCount.ShouldBe(3);
		result.Summary.EmployeeContributionTotal.ShouldBe(240m);
		result.GeneratedAtUtc.ShouldBe(new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc));
		result.PreviewFingerprint.ShouldBe(new string('A', 64));
	}

	[Theory]
	[InlineData(EsiStatutoryCalculator.EstablishmentInactiveIssue, "establishment")]
	[InlineData(EsiStatutoryCalculator.WageClassificationIssue, "earningClassifications")]
	[InlineData(EsiStatutoryCalculator.EnrollmentReviewIssue, "employeeEnrollment")]
	[InlineData(EsiStatutoryCalculator.InsuranceNumberIssue, "employeeEnrollment")]
	[InlineData(EsiStatutoryCalculator.DisabilityReliefEvidenceIssue, "employeeEnrollment")]
	[InlineData(IssueCodes.CompensationRevisionRequired, "compensationRevision")]
	[InlineData("UNEXPECTED_ISSUE", "general")]
	public void Should_present_every_issue_as_actionable_copy(string code, string target)
	{
		var issue = EsiStatutoryPresentation.CreateIssue(code, 2);

		issue.Code.ShouldBe(code);
		issue.Title.ShouldNotBeNullOrWhiteSpace();
		issue.Message.ShouldNotBeNullOrWhiteSpace();
		issue.ResolutionTarget.ShouldBe(target);
		issue.AffectedEmployeeCount.ShouldBe(2);
		issue.Title.ShouldNotContain("_");
	}

	[Theory]
	[InlineData("ESTABLISHMENT_INACTIVE")]
	[InlineData("WAGE_CLASSIFICATION_REQUIRED")]
	[InlineData("FORCE_NOT_COVERED_OVERRIDE")]
	[InlineData("FORCE_COVERED_OVERRIDE")]
	[InlineData("CONTRIBUTION_PERIOD_CONTINUATION")]
	[InlineData("WITHIN_WAGE_CEILING")]
	[InlineData("ABOVE_WAGE_CEILING")]
	[InlineData(IssueCodes.CompensationRevisionRequired)]
	[InlineData("UNEXPECTED_DECISION")]
	public void Should_present_every_coverage_decision_as_readable_copy(string code)
	{
		var label = EsiStatutoryPresentation.GetCoverageDecisionLabel(code);

		label.ShouldNotBeNullOrWhiteSpace();
		label.ShouldNotContain("_");
	}

	private static EmployeeStatutoryImpactDto Employee(
		string code,
		string name,
		bool covered,
		decimal wages,
		string enrollment,
		params StatutoryIssueDto[] issues) => new()
	{
		EmployeeId = Guid.NewGuid(),
		EmployeeIdentifier = code,
		EmployeeName = name,
		IsCovered = covered,
		CanCalculate = true,
		StatutoryWages = wages,
		EnrollmentStatus = enrollment,
		Issues = issues.ToList()
	};
}
