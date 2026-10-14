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
	public void Pf_coverage_boundary_uses_gross_salary(decimal grossSalary, string status, bool canSave)
	{
		var result = PfEligibilityCalculator.Calculate(grossSalary, grossSalary * 0.6m, true,
			new() { Membership = "NOT_MEMBER" }, false, false);
		result.Status.ShouldBe(status);
		result.CanSave.ShouldBe(canSave);
		result.GrossSalary.ShouldBe(grossSalary);
		result.Wages.ShouldBe(grossSalary * 0.6m);
	}
	[Fact]
	public void Existing_pf_membership_is_preserved_above_the_gross_ceiling()
	{
		var result = PfEligibilityCalculator.Calculate(60000m, 36000m, true,
			new() { Membership = "NOT_MEMBER" }, true, false);
		result.Status.ShouldBe("CONTINUING_MEMBER");
		result.IssueCode.ShouldBe("PF_ENROLLMENT_REQUIRED");
	}
	[Fact]
	public void Voluntary_pf_requires_consent_and_unknown_coverage_requires_review()
	{
		var declaration = new PfEnrollmentDeclaration { Membership = "NOT_MEMBER" };
		PfEligibilityCalculator.Calculate(35000m, 21000m, true, declaration, false, true)
			.IssueCode.ShouldBe("PF_CONSENT_REQUIRED");
		PfEligibilityCalculator.Calculate(35000m, 21000m, true,
			declaration with { ConsentReference = "Employee declaration" }, false, true).CanSave.ShouldBeTrue();
		PfEligibilityCalculator.Calculate(35000m, 21000m, null, declaration, false, false)
			.Status.ShouldBe("NEEDS_REVIEW");
	}
	[Fact]
	public void Gross_salary_above_the_ceiling_is_optional_even_when_basic_is_below_it()
	{
		var result = PfEligibilityCalculator.Calculate(30000m, 18000m, true,
			new() { Membership = "NOT_MEMBER" }, false, false);
		result.Status.ShouldBe("VOLUNTARY");
		result.CanSave.ShouldBeTrue();
		result.EnrollmentRequired.ShouldBeFalse();
	}
	[Fact]
	public void Opting_in_still_requires_a_positive_pf_contribution_basis()
	{
		var result = PfEligibilityCalculator.Calculate(30000m, 0m, true,
			new() { Membership = "NOT_MEMBER", ConsentReference = "Employee consent" }, false, true);
		result.IssueCode.ShouldBe("PF_WAGE_BASIS_REQUIRED");
	}
	[Fact]
	public void Legacy_decisions_can_be_read_without_rewriting_saved_wages()
	{
		var original = "{\"Status\":\"MANDATORY\",\"Wages\":18000,\"CoverageCeiling\":25000,\"EnrollmentRequired\":true,\"CanSave\":true,\"IssueCode\":null}";
		var decision = JsonSerializer.Deserialize<PfEligibilityDecision>(original)!;
		decision.GrossSalary.ShouldBeNull();
		decision.Wages.ShouldBe(18000m);
		decision.Status.ShouldBe("MANDATORY");
	}
	[Theory]
	[InlineData("PF_ENROLLMENT_REQUIRED", true)]
	[InlineData("PF_MEMBERSHIP_REVIEW_REQUIRED", true)]
	[InlineData("PF_CONSENT_REQUIRED", true)]
	[InlineData("PF_WAGE_BASIS_REQUIRED", true)]
	[InlineData("ESI_COMPONENT_REQUIRED", true)]
	[InlineData("ESI_RATE_OVERRIDE", false)]
	public void Payroll_lock_matches_new_statutory_readiness(string code, bool blocked) =>
		PayrollLockPreconditionGuard.IsBlockingLockIssue(code).ShouldBe(blocked);

	[Fact]
	public void Selected_component_alone_controls_deductions_and_independent_eligibility()
	{
		var basic = Guid.NewGuid(); var allowance = Guid.NewGuid();
		StatutoryWageLine[] full = [new(basic, "School basic", 9000m, StatutoryWageTreatment.OtherExclusion),
			new(allowance, "Allowance", 6000m, StatutoryWageTreatment.Included), new(Guid.NewGuid(), "HRA", 9000m, StatutoryWageTreatment.SpecifiedExclusion)];
		var settings = new EsiCalculationSettings { EligibilityBasis = "COMPONENT", EligibilityComponentId = allowance,
			CalculationBasis = "COMPONENT", CalculationComponentId = basic };
		var result = Calculate(full.Select(x => x with { Amount = x.Amount / 2m }).ToList(), settings, full);
		result.EligibilityWages.ShouldBe(6000m); result.StatutoryWages.ShouldBe(4500m);
		result.EmployeeContribution.ShouldBe(34m); result.EmployerContribution.ShouldBe(147m);
		result.ReferenceStatutoryWages.ShouldBe(3000m);
		result.IssueCodes.ShouldContain("ESI_BASIS_RECONCILIATION_DIFFERENCE");
		Calculate(full, settings with { EligibilityBasis = "GROSS", EligibilityComponentId = null }).IsCovered.ShouldBeFalse();
		Calculate([.. full, new(Guid.NewGuid(), "Extra earning", 3000m, StatutoryWageTreatment.Included, IsAdditional: true)], settings)
			.StatutoryWages.ShouldBe(9000m);
	}
	[Theory]
	[InlineData("COMPONENT", "GROSS")]
	[InlineData("GROSS", "COMPONENT")]
	[InlineData("COMPONENT", "COMPONENT")]
	public void Missing_or_inactive_component_never_falls_back(string eligibility, string calculation)
	{
		var id = Guid.NewGuid();
		var settings = new EsiCalculationSettings { EligibilityBasis = eligibility, CalculationBasis = calculation,
			EligibilityComponentId = eligibility == "COMPONENT" ? id : null, CalculationComponentId = calculation == "COMPONENT" ? id : null };
		Calculate([new(Guid.NewGuid(), "Ordinary", 12000m, StatutoryWageTreatment.Included)], settings).CanCalculate.ShouldBeFalse();
		Calculate([new(id, "Custom component", 12000m, StatutoryWageTreatment.Included)],
			settings with { EligibilityComponentIsActive = false, CalculationComponentIsActive = false }).IssueCodes.ShouldContain(EsiStatutoryCalculator.BasicComponentIssue);
	}
	[Fact]
	public void Attendance_uses_payable_component_once_and_keeps_full_month_eligibility()
	{
		var basic = Guid.NewGuid();
		StatutoryWageLine[] full = [new(basic, "Basic", 9538.2m, StatutoryWageTreatment.Included),
			new(Guid.NewGuid(), "Allowance", 6358.8m, StatutoryWageTreatment.Included)];
		var settings = new EsiCalculationSettings { EligibilityBasis = "GROSS", CalculationBasis = "COMPONENT", CalculationComponentId = basic };
		Calculate(full, settings).EmployeeContribution.ShouldBe(72m);
		var prorated = Calculate(full.Select(x => x with { Amount = x.Amount / 2m }).ToList(), settings, full);
		prorated.EligibilityWages.ShouldBe(15897m); prorated.StatutoryWages.ShouldBe(4769.1m); prorated.EmployeeContribution.ShouldBe(36m);
		Calculate(full.Select(x => x with { Amount = 0m }).ToList(), settings, full).EmployeeContribution.ShouldBe(0m);
	}
	[Fact]
	public void Configuration_validates_independent_ids_and_reads_historical_coupled_json()
	{
		Should.Throw<BusinessException>(() => new EsiCalculationSettings { EligibilityBasis = "COMPONENT" }.Validate());
		Should.Throw<BusinessException>(() => new EsiCalculationSettings { CalculationComponentId = Guid.NewGuid() }.Validate());
		Should.Throw<BusinessException>(() => new EsiCalculationSettings { EligibilityBasis = "INVALID" }.Validate());
		var basic = Guid.NewGuid();
		var legacy = JsonSerializer.Serialize(new { WageBasis = "BASIC_COMPONENT", BasicComponentId = basic });
		var configuration = new EsiPayrollConfiguration(Guid.NewGuid(), Guid.NewGuid(), new(2026, 10, 1), true, null, null, "TEST");
		typeof(EsiPayrollConfiguration).GetProperty(nameof(EsiPayrollConfiguration.CalculationSettingsJson))!.SetValue(configuration, legacy);
		var read = configuration.GetCalculationSettings(); read.EligibilityBasis.ShouldBe("COMPONENT"); read.CalculationBasis.ShouldBe("COMPONENT");
		read.EligibilityComponentId.ShouldBe(basic); read.CalculationComponentId.ShouldBe(basic); read.IsLegacyRead.ShouldBeTrue();
		configuration.CalculationSettingsJson.ShouldBe(legacy);
	}
	[Fact]
	public void Rate_override_keeps_employee_and_employer_shares_separate()
	{
		Should.Throw<BusinessException>(() => new EsiCalculationSettings { EmployeePercentage = 0.5m }.Validate());
		var settings = new EsiCalculationSettings { EmployeePercentage = 0.5m, EmployerPercentage = 4m,
			OverrideReason = "School documented policy", EvidenceReference = "Policy reference" };
		var result = Calculate([new(Guid.NewGuid(), "Basic", 9600m, StatutoryWageTreatment.Included)], settings);
		result.EmployeeContribution.ShouldBe(48m); result.EmployerContribution.ShouldBe(384m);
		result.IssueCodes.ShouldContain(EsiStatutoryCalculator.RateOverrideIssue);
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