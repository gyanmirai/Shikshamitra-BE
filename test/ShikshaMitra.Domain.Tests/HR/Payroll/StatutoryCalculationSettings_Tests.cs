using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
		Calculate(wages, new() { EligibilityBasis = "GROSS", CalculationBasis = "GROSS" }).IsCovered.ShouldBeFalse();
		var settings = new EsiCalculationSettings { EligibilityBasis = "BASIC_COMPONENT", CalculationBasis = "BASIC_COMPONENT", BasicComponentId = basic };
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
		var settings = new EsiCalculationSettings { EligibilityBasis = "BASIC_COMPONENT", CalculationBasis = "BASIC_COMPONENT", BasicComponentId = basic };
		Calculate(full, settings).EmployeeContribution.ShouldBe(72m);
		var result = Calculate(full.Select(x => x with { Amount = x.Amount / 2m }).ToList(), settings, full);
		result.EligibilityWages.ShouldBe(9598.2m);
		result.StatutoryWages.ShouldBe(4799.1m);
		result.EmployeeContribution.ShouldBe(36m);
		Calculate(full.Select(x => x with { Amount = 0m }).ToList(), settings, full).EmployeeContribution.ShouldBe(0m);
	}
	[Theory]
	[InlineData("BASIC_COMPONENT", "GROSS")]
	[InlineData("GROSS", "BASIC_COMPONENT")]
	[InlineData("BASIC_COMPONENT", "BASIC_COMPONENT")]
	public void Missing_or_inactive_basic_never_falls_back_to_gross(string eligibilityBasis, string calculationBasis)
	{
		var id = Guid.NewGuid();
		var settings = new EsiCalculationSettings { EligibilityBasis = eligibilityBasis, CalculationBasis = calculationBasis, BasicComponentId = id };
		Calculate([new(Guid.NewGuid(), "Ordinary", 12000m, StatutoryWageTreatment.Included)], settings).CanCalculate.ShouldBeFalse();
		Calculate([new(id, "Basic", 12000m, StatutoryWageTreatment.Included, IsBasic: true)],
			settings with { BasicComponentIsActive = false }).IssueCodes.ShouldContain(EsiStatutoryCalculator.BasicComponentIssue);
	}
	[Theory]
	[InlineData("CLASSIFIED_EARNINGS", "CLASSIFIED_EARNINGS", 6000, 3000, 23)]
	[InlineData("CLASSIFIED_EARNINGS", "BASIC_COMPONENT", 6000, 7500, 57)]
	[InlineData("CLASSIFIED_EARNINGS", "GROSS", 6000, 12000, 90)]
	[InlineData("BASIC_COMPONENT", "CLASSIFIED_EARNINGS", 15000, 3000, 23)]
	[InlineData("BASIC_COMPONENT", "BASIC_COMPONENT", 15000, 7500, 57)]
	[InlineData("BASIC_COMPONENT", "GROSS", 15000, 12000, 90)]
	[InlineData("GROSS", "CLASSIFIED_EARNINGS", 24000, 3000, 0)]
	[InlineData("GROSS", "BASIC_COMPONENT", 24000, 7500, 0)]
	[InlineData("GROSS", "GROSS", 24000, 12000, 0)]
	public void Eligibility_and_contribution_bases_are_independent_after_attendance(
		string eligibilityBasis, string calculationBasis, decimal eligibilityWages, decimal payableWages, decimal contribution)
	{
		var basic = Guid.NewGuid();
		StatutoryWageLine[] full = [new(basic, "School basic", 9000m, StatutoryWageTreatment.OtherExclusion, IsBasic: true),
			new(Guid.NewGuid(), "Allowance", 6000m, StatutoryWageTreatment.Included),
			new(Guid.NewGuid(), "HRA", 9000m, StatutoryWageTreatment.SpecifiedExclusion)];
		var settings = new EsiCalculationSettings
		{
			EligibilityBasis = eligibilityBasis,
			CalculationBasis = calculationBasis,
			BasicComponentId = eligibilityBasis == "BASIC_COMPONENT" || calculationBasis == "BASIC_COMPONENT" ? basic : null
		};
		var result = Calculate(full.Select(x => x with { Amount = x.Amount / 2m }).ToList(), settings, full);
		result.EligibilityWages.ShouldBe(eligibilityWages);
		result.StatutoryWages.ShouldBe(payableWages);
		result.EmployeeContribution.ShouldBe(contribution);
		result.CalculationSettings.ShouldBe(settings);
	}
	[Fact]
	public void Employee_daily_wage_exemption_uses_full_month_calculation_basis()
	{
		var basic = Guid.NewGuid();
		StatutoryWageLine[] full = [new(basic, "School basic", 4000m, StatutoryWageTreatment.Included, IsBasic: true),
			new(Guid.NewGuid(), "HRA", 4000m, StatutoryWageTreatment.SpecifiedExclusion)];
		var settings = new EsiCalculationSettings { EligibilityBasis = "BASIC_COMPONENT", CalculationBasis = "GROSS", BasicComponentId = basic };
		Calculate(full, settings).EmployeeContribution.ShouldBe(60m);
		Calculate(full, settings with { EligibilityBasis = "GROSS", CalculationBasis = "BASIC_COMPONENT" }).EmployeeContribution.ShouldBe(0m);
		Calculate(full.Select(x => x with { Amount = x.Amount / 4m }).ToList(), settings, full).EmployeeContribution.ShouldBe(15m);
	}
	[Fact]
	public void Calculation_basic_is_not_required_for_an_employee_outside_gross_eligibility()
	{
		var result = Calculate([new(Guid.NewGuid(), "Salary", 30000m, StatutoryWageTreatment.Included)],
			new() { EligibilityBasis = "GROSS", CalculationBasis = "BASIC_COMPONENT", BasicComponentId = Guid.NewGuid() });
		result.IsCovered.ShouldBeFalse();
		result.CanCalculate.ShouldBeTrue();
		result.EligibilityWages.ShouldBe(30000m);
		result.IssueCodes.ShouldNotContain(EsiStatutoryCalculator.BasicComponentIssue);
	}
	[Fact]
	public void Continuing_coverage_still_requires_the_selected_calculation_component()
	{
		var enrollment = new EmployeeStatutoryEnrollment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ESI");
		enrollment.Update(EmployeeStatutoryEnrollmentStatus.Covered, StatutoryWageCeilingCategory.Standard,
			"SYNTHETIC-TEST", new(2026, 10, 1), null, new(2027, 3, 31), null);
		var result = Calculate([new(Guid.NewGuid(), "Salary", 30000m, StatutoryWageTreatment.Included)],
			new() { EligibilityBasis = "GROSS", CalculationBasis = "BASIC_COMPONENT", BasicComponentId = Guid.NewGuid() }, enrollment: enrollment);
		result.CanCalculate.ShouldBeFalse();
		result.IssueCodes.ShouldContain(EsiStatutoryCalculator.BasicComponentIssue);
	}
	[Fact]
	public void Invalid_basis_values_and_basic_component_requirements_are_rejected()
	{
		Should.Throw<BusinessException>(() => new EsiCalculationSettings { EligibilityBasis = "INVALID" }.Validate());
		Should.Throw<BusinessException>(() => new EsiCalculationSettings { CalculationBasis = "INVALID" }.Validate());
		Should.Throw<BusinessException>(() => new EsiCalculationSettings { EligibilityBasis = "BASIC_COMPONENT" }.Validate());
		Should.Throw<BusinessException>(() => new EsiCalculationSettings { CalculationBasis = "BASIC_COMPONENT" }.Validate());
		Should.Throw<BusinessException>(() => new EsiCalculationSettings { BasicComponentId = Guid.NewGuid() }.Validate());
		Should.Throw<JsonException>(() => JsonSerializer.Deserialize<EsiCalculationSettings>("{\"WageBasis\":\"GROSS\"}"));
	}
	[Fact]
	public void Older_configuration_history_keeps_its_coupled_policy_and_original_json()
	{
		var basic = Guid.NewGuid();
		var legacy = JsonSerializer.Serialize(new
		{
			WageBasis = "BASIC_COMPONENT",
			BasicComponentId = basic,
			EmployeePercentage = 0.5m,
			EmployerPercentage = 4m,
			OverrideReason = "Historic policy",
			EvidenceReference = "Saved reference",
			HasRateOverride = true
		});
		var configuration = new EsiPayrollConfiguration(Guid.NewGuid(), Guid.NewGuid(), new(2026, 10, 1), true, null, null, "TEST");
		typeof(EsiPayrollConfiguration).GetProperty(nameof(EsiPayrollConfiguration.CalculationSettingsJson))!.SetValue(configuration, legacy);
		var read = configuration.GetCalculationSettings();
		read.EligibilityBasis.ShouldBe("BASIC_COMPONENT");
		read.CalculationBasis.ShouldBe("BASIC_COMPONENT");
		read.BasicComponentId.ShouldBe(basic);
		read.EmployeePercentage.ShouldBe(0.5m);
		read.OverrideReason.ShouldBe("Historic policy");
		configuration.CalculationSettingsJson.ShouldBe(legacy);
		Calculate([new(basic, "Basic", 10000m, StatutoryWageTreatment.Included, IsBasic: true)], read).EmployeeContribution.ShouldBe(50m);
	}
	[Fact]
	public void Rate_override_requires_documentation_and_keeps_shares_separate()
	{
		Should.Throw<BusinessException>(() => new EsiCalculationSettings { EmployeePercentage = 0.5m }.Validate());
		Should.Throw<BusinessException>(() => new EsiCalculationSettings { EmployerPercentage = -1m }.Validate());
		var settings = new EsiCalculationSettings
		{
			EmployeePercentage = 0.5m,
			EmployerPercentage = 4m,
			OverrideReason = "School documented policy",
			EvidenceReference = "Policy reference"
		};
		var result = Calculate([new(Guid.NewGuid(), "Basic", 9600m, StatutoryWageTreatment.Included)], settings);
		result.EmployeeContribution.ShouldBe(48m);
		result.EmployerContribution.ShouldBe(384m);
		result.IssueCodes.ShouldContain(EsiStatutoryCalculator.RateOverrideIssue);
		result.CalculationSettings.EvidenceReference.ShouldBe(settings.EvidenceReference);
	}
	private static EsiStatutoryCalculationResult Calculate(IReadOnlyList<StatutoryWageLine> wages,
		EsiCalculationSettings settings, IReadOnlyList<StatutoryWageLine>? full = null, EmployeeStatutoryEnrollment? enrollment = null)
	{
		var rule = new StatutoryRuleVersion(Guid.NewGuid(), Guid.NewGuid(), "ESI", "IN-GJ", 1,
			new(2025, 11, 21), null, 10, 21000m, 25000m, 0.75m, 3.25m, 50m, 15m,
			StatutoryRoundingPolicy.NextHigherRupee, "https://esic.gov.in/contribution", new(2026, 10, 12));
		var establishment = new StatutoryEstablishment(Guid.NewGuid(), Guid.NewGuid(), "ESI", "IN-GJ");
		establishment.Configure("IN-GJ", "TEST", 20, new(2026, 1, 1), new(2026, 1, 1), "Synthetic test");
		establishment.Activate(Guid.NewGuid(), new(2026, 1, 1));
		return EsiStatutoryCalculator.Calculate(new(2026, 10, 1), rule, establishment, enrollment, wages,
			coverageWageLines: full, calculationSettings: settings);
	}
}