using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class SalaryTemplateCalculator_Tests
{
	[Fact]
	public void Should_calculate_gross_template_and_employer_pf()
	{
		var fixture = BuildFixture(employeePfPercentage: 5m, employerPfPercentage: 5m, wageCeiling: null);

		var result = SalaryTemplateCalculator.Calculate(
			SalaryInputBasis.Gross,
			30_000m,
			fixture.Rules,
			fixture.Components,
			fixture.PfRule,
			fixture.ProfessionalTaxSlabs);

		result.Gross.ShouldBe(30_000m);
		result.Basic.ShouldBe(18_000m);
		result.TotalDeductions.ShouldBe(1_100m);
		result.Net.ShouldBe(28_900m);
		result.EmployerContributionTotal.ShouldBe(900m);
	}

	[Fact]
	public void Should_recalculate_pf_and_non_prorated_pt_for_payroll_month()
	{
		var fixture = BuildFixture(employeePfPercentage: 5m, employerPfPercentage: 5m, wageCeiling: null);
		var employeeId = Guid.NewGuid();
		var payrollComponents = new List<EmployeePayrollComponent>
		{
			BuildComponent(employeeId, "Basic Salary", 18_000m, true, PayrollComponentSemanticType.Basic),
			BuildComponent(employeeId, "Special Allowance", 12_000m, true, PayrollComponentSemanticType.BalancingEarning),
			BuildComponent(employeeId, "Provident Fund", 900m, false, PayrollComponentSemanticType.EmployeeProvidentFund),
			BuildComponent(employeeId, "Professional Tax", 200m, false, PayrollComponentSemanticType.ProfessionalTax)
		};

		var result = PayrollComputation.Calculate(
			presentDays: 27,
			halfWorkingDays: 0,
			payableDays: 27m,
			basisDays: 30m,
			payrollComponents,
			new PayrollStatutoryRuleSet(fixture.PfRule, fixture.ProfessionalTaxSlabs));

		result.GrossSalary.ShouldBe(27_000m);
		result.BasicSalary.ShouldBe(16_200m);
		result.Deductions.ShouldContain(x => x.Source.SemanticType == PayrollComponentSemanticType.EmployeeProvidentFund && x.Amount == 810m);
		result.Deductions.ShouldContain(x => x.Source.SemanticType == PayrollComponentSemanticType.ProfessionalTax && x.Amount == 200m);
		result.TotalDeduction.ShouldBe(1_010m);
		result.NetPay.ShouldBe(25_990m);
		result.EmployerContributionTotal.ShouldBe(810m);
	}

	[Theory]
	[InlineData(true, 200)]
	[InlineData(false, 0)]
	public void Should_follow_configured_professional_tax_treatment_in_cessation_month(
		bool deductInCessationMonth,
		decimal expectedTax)
	{
		var fixture = BuildFixture(employeePfPercentage: 0m, employerPfPercentage: 0m, wageCeiling: null);
		var employeeId = Guid.NewGuid();
		var payrollComponents = new List<EmployeePayrollComponent>
		{
			BuildComponent(employeeId, "Basic Salary", 18_000m, true, PayrollComponentSemanticType.Basic),
			BuildComponent(employeeId, "Special Allowance", 12_000m, true, PayrollComponentSemanticType.BalancingEarning),
			BuildComponent(employeeId, "Professional Tax", 200m, false, PayrollComponentSemanticType.ProfessionalTax)
		};
		var professionalTaxSlabs = new List<ProfessionalTax>
		{
			new(Guid.NewGuid(), Guid.NewGuid(), "IN-GJ", new DateTime(2022, 4, 1), null,
				0m, 12_000m, 0m, deductInCessationMonth: deductInCessationMonth),
			new(Guid.NewGuid(), Guid.NewGuid(), "IN-GJ", new DateTime(2022, 4, 1), null,
				12_000.01m, null, 200m, deductInCessationMonth: deductInCessationMonth)
		};

		var result = PayrollComputation.Calculate(
			presentDays: 30,
			halfWorkingDays: 0,
			payableDays: 30m,
			basisDays: 30m,
			payrollComponents,
			new PayrollStatutoryRuleSet(null, professionalTaxSlabs, IsEmploymentCessationMonth: true));

		result.Deductions
			.Where(x => x.Source.SemanticType == PayrollComponentSemanticType.ProfessionalTax)
			.Sum(x => x.Amount)
			.ShouldBe(expectedTax);
	}

	[Fact]
	public void Should_solve_take_home_across_professional_tax_boundary()
	{
		var fixture = BuildFixture(employeePfPercentage: 5m, employerPfPercentage: 5m, wageCeiling: null);

		var result = SalaryTemplateCalculator.Calculate(
			SalaryInputBasis.TakeHome,
			25_990m,
			fixture.Rules,
			fixture.Components,
			fixture.PfRule,
			fixture.ProfessionalTaxSlabs);

		result.Gross.ShouldBe(27_000m);
		result.Net.ShouldBe(25_990m);
	}

	[Fact]
	public void Should_choose_the_lowest_gross_when_two_tax_intervals_produce_the_target_net()
	{
		var fixture = BuildFixture(employeePfPercentage: 0m, employerPfPercentage: 0m, wageCeiling: null);

		var result = SalaryTemplateCalculator.Calculate(
			SalaryInputBasis.TakeHome,
			12_000m,
			fixture.Rules,
			fixture.Components,
			pfRule: null,
			professionalTaxSlabs: fixture.ProfessionalTaxSlabs);

		result.Gross.ShouldBe(12_000m);
		result.Net.ShouldBe(12_000m);
	}

	[Fact]
	public void Should_apply_pf_wage_ceiling_to_employee_and_employer_contributions()
	{
		var fixture = BuildFixture(employeePfPercentage: 12m, employerPfPercentage: 12m, wageCeiling: 15_000m);

		var result = SalaryTemplateCalculator.Calculate(
			SalaryInputBasis.Gross,
			30_000m,
			fixture.Rules,
			fixture.Components,
			fixture.PfRule,
			fixture.ProfessionalTaxSlabs);

		result.Deductions.Single(x => x.Code == "EMPLOYEE_PF").Amount.ShouldBe(1_800m);
		result.EmployerContributionTotal.ShouldBe(1_800m);
		result.Net.ShouldBe(28_000m);
	}

	[Fact]
	public void Should_keep_employer_only_pf_out_of_employee_deductions()
	{
		var fixture = BuildFixture(employeePfPercentage: 0m, employerPfPercentage: 12m, wageCeiling: null);

		var result = SalaryTemplateCalculator.Calculate(
			SalaryInputBasis.Gross,
			30_000m,
			fixture.Rules,
			fixture.Components,
			fixture.PfRule,
			fixture.ProfessionalTaxSlabs);

		result.Deductions.Single(x => x.Code == "EMPLOYEE_PF").Amount.ShouldBe(0m);
		result.EmployerContributionTotal.ShouldBe(2_160m);
		result.Net.ShouldBe(29_800m);
	}

	[Theory]
	[InlineData(12000, 0)]
	[InlineData(12000.01, 200)]
	public void Should_apply_gujarat_professional_tax_boundaries(decimal gross, decimal expectedTax)
	{
		var fixture = BuildFixture(employeePfPercentage: 0m, employerPfPercentage: 0m, wageCeiling: null);

		var result = SalaryTemplateCalculator.Calculate(
			SalaryInputBasis.Gross,
			gross,
			fixture.Rules,
			fixture.Components,
			pfRule: null,
			professionalTaxSlabs: fixture.ProfessionalTaxSlabs);

		result.Deductions.Single(x => x.Code == "PROFESSIONAL_TAX").Amount.ShouldBe(expectedTax);
	}

	[Fact]
	public void Should_round_each_line_midpoint_away_from_zero()
	{
		var fixture = BuildFixture(employeePfPercentage: 0m, employerPfPercentage: 0m, wageCeiling: null);
		var result = SalaryTemplateCalculator.Calculate(
			SalaryInputBasis.Gross,
			200.01m,
			fixture.Rules,
			fixture.Components,
			pfRule: null,
			professionalTaxSlabs: []);

		result.Basic.ShouldBe(120.01m);
		result.Earnings.Sum(x => x.Amount).ShouldBe(200.01m);
	}

	[Fact]
	public void Should_reject_component_dependency_cycles()
	{
		var fixture = BuildFixture(employeePfPercentage: 0m, employerPfPercentage: 0m, wageCeiling: null);
		fixture.Rules.Add(new SalaryTemplateRule(
			"FIRST", Guid.NewGuid(), SalaryTemplateLineKind.Deduction,
			SalaryTemplateFormulaType.PercentageOfComponent, PayrollComponentSemanticType.Ordinary,
			10m, "SECOND", 5));
		fixture.Rules.Add(new SalaryTemplateRule(
			"SECOND", Guid.NewGuid(), SalaryTemplateLineKind.Deduction,
			SalaryTemplateFormulaType.PercentageOfComponent, PayrollComponentSemanticType.Ordinary,
			10m, "FIRST", 6));

		Should.Throw<BusinessException>(() => SalaryTemplateCalculator.ValidateRules(fixture.Rules));
	}

	[Fact]
	public void Should_reject_statutory_formula_on_an_earning_line()
	{
		var fixture = BuildFixture(employeePfPercentage: 5m, employerPfPercentage: 5m, wageCeiling: null);
		var invalidRule = fixture.Rules.Single(x => x.Code == "EMPLOYEE_PF");
		fixture.Rules[fixture.Rules.IndexOf(invalidRule)] = invalidRule with
		{
			LineKind = SalaryTemplateLineKind.Earning
		};

		var exception = Should.Throw<BusinessException>(() => SalaryTemplateCalculator.ValidateRules(fixture.Rules));

		exception.Code.ShouldBe("HR:Payroll:SalaryTemplateFormulaLineKindMismatch");
	}

	private static EmployeePayrollComponent BuildComponent(
		Guid employeeId,
		string name,
		decimal amount,
		bool isEarning,
		PayrollComponentSemanticType semanticType) =>
		new(
			Guid.NewGuid(), employeeId, name, Guid.NewGuid(), null, amount, true, false, null,
			SalaryComponentType.Fixed, SalaryComponentDependency.None, isEarning, false, semanticType);

	private static SalaryFixture BuildFixture(decimal employeePfPercentage, decimal employerPfPercentage, decimal? wageCeiling)
	{
		var basicId = Guid.NewGuid();
		var allowanceId = Guid.NewGuid();
		var pfId = Guid.NewGuid();
		var ptId = Guid.NewGuid();
		var rules = new List<SalaryTemplateRule>
		{
			new("BASIC", basicId, SalaryTemplateLineKind.Earning, SalaryTemplateFormulaType.PercentageOfGross, PayrollComponentSemanticType.Basic, 60m, null, 1),
			new("SPECIAL_ALLOWANCE", allowanceId, SalaryTemplateLineKind.Earning, SalaryTemplateFormulaType.RemainderOfGross, PayrollComponentSemanticType.BalancingEarning, null, null, 2),
			new("EMPLOYEE_PF", pfId, SalaryTemplateLineKind.Deduction, SalaryTemplateFormulaType.EmployeeProvidentFund, PayrollComponentSemanticType.EmployeeProvidentFund, null, "BASIC", 3),
			new("PROFESSIONAL_TAX", ptId, SalaryTemplateLineKind.Deduction, SalaryTemplateFormulaType.ProfessionalTax, PayrollComponentSemanticType.ProfessionalTax, null, null, 4)
		};
		var components = new Dictionary<Guid, SalaryTemplateComponent>
		{
			[basicId] = new(basicId, "Basic Salary"),
			[allowanceId] = new(allowanceId, "Special Allowance"),
			[pfId] = new(pfId, "Provident Fund"),
			[ptId] = new(ptId, "Professional Tax")
		};
		var pfRule = new ProvidentFund(
			Guid.NewGuid(), Guid.NewGuid(), "TEST-PF", "Test PF", employeePfPercentage,
			employerPfPercentage, PfContributionBasis.Basic, wageCeiling, new DateTime(2022, 4, 1));
		var ptSlabs = new List<ProfessionalTax>
		{
			new(Guid.NewGuid(), Guid.NewGuid(), "IN-GJ", new DateTime(2022, 4, 1), null, 0m, 12_000m, 0m),
			new(Guid.NewGuid(), Guid.NewGuid(), "IN-GJ", new DateTime(2022, 4, 1), null, 12_000.01m, null, 200m)
		};

		return new SalaryFixture(rules, components, pfRule, ptSlabs);
	}

	private sealed record SalaryFixture(
		List<SalaryTemplateRule> Rules,
		Dictionary<Guid, SalaryTemplateComponent> Components,
		ProvidentFund PfRule,
		List<ProfessionalTax> ProfessionalTaxSlabs);
}
