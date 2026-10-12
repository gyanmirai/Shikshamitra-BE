using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class StatutoryCalculationSettings_Tests
{
	[Theory]
	[InlineData(24999, "MANDATORY", false)]
	[InlineData(25000, "MANDATORY", false)]
	[InlineData(25001, "VOLUNTARY", true)]
	public void Pf_coverage_boundary_uses_pf_wages(decimal wages, string status, bool canSave)
	{
		var result = PfEligibilityCalculator.Calculate(new(2026, 10, 1), wages, true,
			new() { Membership = "NOT_MEMBER" }, false, false);
		result.Status.ShouldBe(status);
		result.CanSave.ShouldBe(canSave);
	}
	[Fact]
	public void Historical_pf_ceiling_and_membership_are_preserved()
	{
		PfEligibilityCalculator.CoverageCeiling(new(2026, 9, 16)).ShouldBe(15000m);
		PfEligibilityCalculator.CoverageCeiling(new(2026, 9, 17)).ShouldBe(25000m);
		var result = PfEligibilityCalculator.Calculate(new(2026, 10, 1), 60000m, true,
			new() { Membership = "NOT_MEMBER" }, true, false);
		result.Status.ShouldBe("CONTINUING_MEMBER");
		result.IssueCode.ShouldBe("PF_ENROLLMENT_REQUIRED");
	}
	[Fact]
	public void Voluntary_pf_requires_consent_and_unknown_coverage_requires_review()
	{
		var declaration = new PfEnrollmentDeclaration { Membership = "NOT_MEMBER" };
		PfEligibilityCalculator.Calculate(new(2026, 10, 1), 35000m, true, declaration, false, true)
			.IssueCode.ShouldBe("PF_CONSENT_REQUIRED");
		PfEligibilityCalculator.Calculate(new(2026, 10, 1), 35000m, true,
			declaration with { ConsentReference = "Employee declaration" }, false, true).CanSave.ShouldBeTrue();
		PfEligibilityCalculator.Calculate(new(2026, 10, 1), 35000m, null, declaration, false, false)
			.Status.ShouldBe("NEEDS_REVIEW");
	}
	[Theory]
	[InlineData("PF_ENROLLMENT_REQUIRED", true)]
	[InlineData("PF_MEMBERSHIP_REVIEW_REQUIRED", true)]
	[InlineData("PF_CONSENT_REQUIRED", true)]
	[InlineData("PF_WAGE_BASIS_REQUIRED", true)]
	[InlineData("ESI_BASIC_COMPONENT_REQUIRED", true)]
	[InlineData("ESI_RATE_OVERRIDE", false)]
	public void Payroll_lock_matches_new_statutory_readiness(string code, bool blocked) =>
		PayrollLockPreconditionGuard.IsBlockingLockIssue(code).ShouldBe(blocked);

	[Fact]
	public void Basic_basis_changes_coverage_without_discarding_other_required_wages()
	{
		var basic = Guid.NewGuid();
		StatutoryWageLine[] wages = [new(basic, "Custom basic", 18000m, StatutoryWageTreatment.Included, IsBasic: true),
			new(Guid.NewGuid(), "HRA", 12000m, StatutoryWageTreatment.SpecifiedExclusion)];
		Calculate(wages, new() { WageBasis = "GROSS" }).IsCovered.ShouldBeFalse();
		var settings = new EsiCalculationSettings { WageBasis = "BASIC_COMPONENT", BasicComponentId = basic };
		var result = Calculate(wages, settings);
		result.IsCovered.ShouldBeTrue();
		result.StatutoryWages.ShouldBe(18000m);
		result.EmployeeContribution.ShouldBe(135m);
		Calculate([.. wages, new(Guid.NewGuid(), "DA", 4000m, StatutoryWageTreatment.Included)], settings)
			.IsCovered.ShouldBeFalse();
	}
	[Fact]
	public void Basic_percentage_and_contributions_follow_payable_wages_without_changing_eligibility()
	{
		var basic = Guid.NewGuid();
		StatutoryWageLine[] full = [new(basic, "Basic", 9598.2m, StatutoryWageTreatment.Included, IsBasic: true),
			new(Guid.NewGuid(), "HRA", 6398.8m, StatutoryWageTreatment.SpecifiedExclusion)];
		var settings = new EsiCalculationSettings { WageBasis = "BASIC_COMPONENT", BasicComponentId = basic };
		Calculate(full, settings).EmployeeContribution.ShouldBe(72m);
		var result = Calculate(full.Select(x => x with { Amount = x.Amount / 2m }).ToList(), settings, full);
		result.EligibilityWages.ShouldBe(9598.2m);
		result.StatutoryWages.ShouldBe(4799.1m);
		result.EmployeeContribution.ShouldBe(36m);
		Calculate(full.Select(x => x with { Amount = 0m }).ToList(), settings, full).EmployeeContribution.ShouldBe(0m);
	}
	[Fact]
	public void Missing_or_inactive_basic_never_falls_back_to_gross()
	{
		var id = Guid.NewGuid();
		var settings = new EsiCalculationSettings { WageBasis = "BASIC_COMPONENT", BasicComponentId = id };
		Calculate([new(Guid.NewGuid(), "Ordinary", 12000m, StatutoryWageTreatment.Included)], settings).CanCalculate.ShouldBeFalse();
		Calculate([new(id, "Basic", 12000m, StatutoryWageTreatment.Included, IsBasic: true)],
			settings with { BasicComponentIsActive = false }).IssueCodes.ShouldContain(EsiStatutoryCalculator.BasicComponentIssue);
	}
	[Fact]
	public void Rate_override_requires_documentation_and_keeps_shares_separate()
	{
		Should.Throw<BusinessException>(() => new EsiCalculationSettings { EmployeePercentage = 0.5m }.Validate());
		Should.Throw<BusinessException>(() => new EsiCalculationSettings { EmployerPercentage = -1m }.Validate());
		var settings = new EsiCalculationSettings { EmployeePercentage = 0.5m, EmployerPercentage = 4m,
			OverrideReason = "School documented policy", EvidenceReference = "Policy reference" };
		var result = Calculate([new(Guid.NewGuid(), "Basic", 9600m, StatutoryWageTreatment.Included)], settings);
		result.EmployeeContribution.ShouldBe(48m);
		result.EmployerContribution.ShouldBe(384m);
		result.IssueCodes.ShouldContain(EsiStatutoryCalculator.RateOverrideIssue);
		result.CalculationSettings.EvidenceReference.ShouldBe(settings.EvidenceReference);
	}
	private static EsiStatutoryCalculationResult Calculate(IReadOnlyList<StatutoryWageLine> wages,
		EsiCalculationSettings settings, IReadOnlyList<StatutoryWageLine>? full = null)
	{
		var rule = new StatutoryRuleVersion(Guid.NewGuid(), Guid.NewGuid(), "ESI", "IN-GJ", 1,
			new(2025, 11, 21), null, 10, 21000m, 25000m, 0.75m, 3.25m, 50m, 15m,
			StatutoryRoundingPolicy.NextHigherRupee, "https://esic.gov.in/contribution", new(2026, 10, 12));
		var establishment = new StatutoryEstablishment(Guid.NewGuid(), Guid.NewGuid(), "ESI", "IN-GJ");
		establishment.Configure("IN-GJ", "TEST", 20, new(2026, 1, 1), new(2026, 1, 1), "Synthetic test");
		establishment.Activate(Guid.NewGuid(), new(2026, 1, 1));
		return EsiStatutoryCalculator.Calculate(new(2026, 10, 1), rule, establishment, null, wages,
			coverageWageLines: full, calculationSettings: settings);
	}
}
