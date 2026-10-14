using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class StatutoryEnrollment_Tests
{
	[Theory]
	[InlineData(false, false, false)] [InlineData(false, false, true)]
	[InlineData(false, true, false)] [InlineData(false, true, true)]
	[InlineData(true, false, false)] [InlineData(true, false, true)]
	[InlineData(true, true, false)] [InlineData(true, true, true)]
	public void Application_matrix_preserves_calculated_evidence(bool apply, bool number, bool eligible)
	{
		foreach (var scheme in new[] { "PF", "ESI" })
		{
			var result = PayrollSchemeApplication.Decide(scheme, apply, number ? "7" : "0", eligible, 1145m, 312m, "Recorded reason");
			result.CalculatedEmployeeAmount.ShouldBe(1145m); result.CalculatedEmployerAmount.ShouldBe(312m);
			result.AppliedEmployeeAmount.ShouldBe(apply && number && eligible ? 1145m : 0m);
			result.AppliedEmployerAmount.ShouldBe(apply && number && eligible ? 312m : 0m);
			result.WarningCode.ShouldBe(!apply ? scheme + "_APPLY_DISABLED" : eligible && !number ? scheme + "_NUMBER_MISSING" : null);
			if (result.WarningCode != null) PayrollLockPreconditionGuard.IsBlockingLockIssue(result.WarningCode).ShouldBeFalse();
		}
	}
	[Theory]
	[InlineData(null, null)] [InlineData("", null)] [InlineData("   ", null)] [InlineData(" 0 ", null)]
	[InlineData(" 7 ", "7")] [InlineData("164", "164")] [InlineData("GJ/ABC-123", "GJ/ABC-123")]
	public void School_pf_references_and_missing_numbers_are_normalized(string? raw, string? normalized)
	{
		var version = new EmployeeStatutoryProfileVersion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new(2026, 10, 15),
			new() { PfNumber = raw, EsicInsuranceNumber = raw }, Guid.NewGuid(), "test");
		version.PfNumber.ShouldBe(normalized); version.EsicInsuranceNumber.ShouldBe(normalized);
		version.EffectiveMonth.ShouldBe(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
	}
	[Fact]
	public void Intentional_disabled_flags_require_reasons_and_uan_remains_optional()
	{
		Should.Throw<BusinessException>(() => EmployeeStatutoryProfileVersion.Validate(new() { ApplyPf = false }));
		Should.Throw<BusinessException>(() => EmployeeStatutoryProfileVersion.Validate(new() { ApplyEsic = false }));
		Should.Throw<BusinessException>(() => EmployeeStatutoryProfileVersion.Validate(new() { UanNumber = "123" }));
		EmployeeStatutoryProfileVersion.Validate(new() { ApplyPf = false, PfDisabledReason = "Registration dispute", UanNumber = "123456789012" });
		EmployeeStatutoryProfileVersion.Validate(new() { PfNumber = "244", UanNumber = null });
	}
	[Theory]
	[InlineData(15897, 12, 1145, 72, 14480)]
	[InlineData(13670, 12, 984, 62, 12424)]
	[InlineData(11300, 24, 1627, 51, 9622)]
	[InlineData(11200, 24, 1613, 51, 9536)]
	[InlineData(9300, 24, 1339, 42, 7919)]
	public void Anonymized_school_payroll_uses_basic_at_sixty_percent(decimal gross, decimal pfRate, decimal pfAmount, decimal esiAmount, decimal net)
	{
		var fixture = Build(gross, pfRate);
		var result = PayrollComputation.Calculate(30, 0, 30m, 30m, fixture.Components, fixture.Rules);
		result.GrossSalary.ShouldBe(gross); result.BasicSalary.ShouldBe(gross * .6m);
		result.StatutoryApplication!.ProvidentFund.AppliedEmployeeAmount.ShouldBe(pfAmount);
		result.StatutoryApplication.EmployeeStateInsurance.AppliedEmployeeAmount.ShouldBe(esiAmount);
		result.NetPay.ShouldBe(net); result.EmployerContributionTotal.ShouldBeGreaterThan(0);
		var frozen = JsonSerializer.Serialize(result.StatutoryApplication);
		EsiPayrollReadiness.MissingInsuranceNumber(result.EmployeeStateInsuranceRuleId, result.EmployeeStateInsuranceCoverageDecision, null,
			frozen).ShouldBeTrue();
	}
	[Theory]
	[InlineData(false, true)] [InlineData(true, false)]
	public void Independent_suppression_preserves_other_scheme_and_gross(bool pf, bool esi)
	{
		var fixture = Build(15897m, 12m);
		var profile = fixture.Rules.StatutoryProfile! with { ApplyPf = pf, PfDisabledReason = pf ? null : "School exception", ApplyEsic = esi, EsicDisabledReason = esi ? null : "Registration pending" };
		var result = PayrollComputation.Calculate(30, 0, 30m, 30m, fixture.Components, fixture.Rules with { StatutoryProfile = profile });
		result.GrossSalary.ShouldBe(15897m);
		result.StatutoryApplication!.ProvidentFund.AppliedEmployeeAmount.ShouldBe(pf ? 1145m : 0m);
		result.StatutoryApplication.EmployeeStateInsurance.AppliedEmployeeAmount.ShouldBe(esi ? 72m : 0m);
		if (!esi) EsiPayrollReadiness.MissingInsuranceNumber(result.EmployeeStateInsuranceRuleId, result.EmployeeStateInsuranceCoverageDecision, null,
			JsonSerializer.Serialize(result.StatutoryApplication)).ShouldBeFalse();
	}
	[Theory]
	[InlineData(8500, 27, 7650)] [InlineData(10000, 26, 8666.67)] [InlineData(8500, 13.5, 3825)] [InlineData(8500, 31, 8500)]
	public void Regular_gross_proration_is_capped_and_missing_numbers_skip_contributions(decimal gross, decimal days, decimal earned)
	{
		var fixture = Build(gross, 12m);
		var result = PayrollComputation.Calculate(0, 0, days, 30m, fixture.Components, fixture.Rules with
		{ StatutoryProfile = fixture.Rules.StatutoryProfile! with { PfNumber = "0", EsicInsuranceNumber = " " } });
		result.GrossSalary.ShouldBe(earned);
		result.StatutoryApplication!.ProvidentFund.AppliedEmployeeAmount.ShouldBe(0);
		result.StatutoryApplication.EmployeeStateInsurance.AppliedEmployeeAmount.ShouldBe(0);
		result.EmployerContributionTotal.ShouldBe(0);
	}
	private static (List<EmployeePayrollComponent> Components, PayrollStatutoryRuleSet Rules) Build(decimal gross, decimal pfRate)
	{
		var tenant = Guid.NewGuid(); var employee = Guid.NewGuid(); var basicId = Guid.NewGuid();
		EmployeePayrollComponent Component(string name, decimal amount, bool earning, PayrollComponentSemanticType semantic, Guid? id = null) =>
			new(Guid.NewGuid(), employee, name, id ?? Guid.NewGuid(), null, amount, true, false, null, SalaryComponentType.Fixed, SalaryComponentDependency.None, earning, false, semantic);
		var basic = Component("School basic", gross * .6m, true, PayrollComponentSemanticType.Basic, basicId);
		var pf = new ProvidentFund(Guid.NewGuid(), tenant, "School PF", "Test policy", pfRate, 12m, PfContributionBasis.Basic, null, new(2022, 4, 1));
		var esiRule = new StatutoryRuleVersion(Guid.NewGuid(), tenant, "ESI", "IN-GJ", 1, new(2025, 11, 21), null, 10, 21000, 25000, .75m, 3.25m, 50, 15, StatutoryRoundingPolicy.NextHigherRupee, "https://esic.gov.in/contribution", new(2026, 10, 1));
		var establishment = new StatutoryEstablishment(Guid.NewGuid(), tenant, "ESI", "IN-GJ");
		establishment.Configure("IN-GJ", "TEST", 18, new(2026, 1, 1), new(2026, 1, 1), "Synthetic test"); establishment.Activate(Guid.NewGuid(), new(2026, 1, 1));
		var esi = new PayrollEsiStatutoryContext(new(2026, 10, 1), esiRule, establishment, null,
			new Dictionary<Guid, StatutoryWageTreatment> { [basicId] = StatutoryWageTreatment.Included }, [], Component("ESIC", 0, false, PayrollComponentSemanticType.EmployeeStateInsurance))
		{ CalculationSettings = new() { EligibilityBasis = "GROSS", CalculationBasis = "COMPONENT", CalculationComponentId = basicId } };
		return ([basic, Component("Allowance", gross * .4m, true, PayrollComponentSemanticType.Ordinary), Component("PF", 0, false, PayrollComponentSemanticType.EmployeeProvidentFund), Component("PT", 0, false, PayrollComponentSemanticType.ProfessionalTax)],
			new(pf, [new ProfessionalTax(Guid.NewGuid(), tenant, "IN-GJ", new(2022, 4, 1), null, 0, 12000, 0), new ProfessionalTax(Guid.NewGuid(), tenant, "IN-GJ", new(2022, 4, 1), null, 12000.01m, null, 200)], EmployeeStateInsurance: esi)
			{ StatutoryProfile = new() { PfNumber = "7", EsicInsuranceNumber = "TEST-INSURANCE" } });
	}
}
