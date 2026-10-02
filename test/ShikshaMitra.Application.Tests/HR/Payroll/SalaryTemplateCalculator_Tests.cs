using System.Text.Json;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
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
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Ctc_boundaries_reconcile_with_payroll_and_never_exceed_monthly_budget(bool includePf)
	{
		var fixture = BuildFixture(12m, 12m, 15000m);
		var rules = fixture.Rules.Where(x => x.LineKind == SalaryTemplateLineKind.Earning ||
			(includePf && x.SemanticType == PayrollComponentSemanticType.EmployeeProvidentFund)).ToList();
		var esi = BuildEsiContext(fixture.Components.Keys);
		foreach (var budget in new[] { 1.01m, 4576m, 4725m, 10000m, 10000.01m, 10325.01m, 20999.99m, 21000m, 21000.01m, 21683m, 25000m, 100000m })
		{
			var quote = SalaryTemplateCalculator.Calculate(SalaryInputBasis.EmployerCost, budget,
				rules, fixture.Components, includePf ? fixture.PfRule : null, [], esi);
			quote.EmployerCost.ShouldBeLessThanOrEqualTo(budget);
			quote.Earnings.Sum(x => x.Amount).ShouldBe(quote.Gross);
			quote.Net.ShouldBe(quote.Gross - quote.Deductions.Sum(x => x.Amount));
			var id = Guid.NewGuid();
			var components = quote.Earnings.Concat(quote.Deductions).Select(line =>
			{
				PayrollComponentSemanticTypeCodes.TryParse(line.SemanticType, out var semantic);
				return new EmployeePayrollComponent(Guid.NewGuid(), id, line.Name, line.ComponentId, null,
					line.Amount, true, false, null, SalaryComponentType.Fixed, SalaryComponentDependency.None,
					quote.Earnings.Contains(line), false, semantic);
			}).ToList();
			var deduction = BuildComponent(id, "System ESI", 0m, false, PayrollComponentSemanticType.EmployeeStateInsurance);
			var payrollEsi = new PayrollEsiStatutoryContext(esi.EffectiveMonth, esi.Rule, esi.Establishment,
				esi.Enrollment, esi.ComponentTreatments, [], deduction);
			foreach (var days in new[] { 0m, 1m, 15.5m, 31m })
			{
				var payroll = PayrollComputation.Calculate(0, 0, days, 31m, components,
					new(includePf ? fixture.PfRule : null, [], EmployeeStateInsurance: payrollEsi));
				payroll.GrossSalary.ShouldBe(Math.Round(quote.Gross * days / 31m, 2, MidpointRounding.AwayFromZero));
				(payroll.TotalEarnings + payroll.EmployerContributionTotal).ShouldBeLessThanOrEqualTo(budget);
				payroll.NetPay.ShouldBe(payroll.TotalEarnings - payroll.TotalDeduction);
				if (days == 31m)
				{
					payroll.NetPay.ShouldBe(quote.Net);
					payroll.EmployerContributionTotal.ShouldBe(quote.EmployerContributionTotal);
				}
			}
		}
	}

    [Theory]
    [InlineData(SalaryInputBasis.Gross, 10000, 75, 325, 9925, 10325)]
    [InlineData(SalaryInputBasis.EmployerCost, 9685, 73, 315, 9612, 10000)]
    public void Ten_thousand_examples_reconcile_without_an_employer_deduction(
        SalaryInputBasis basis, decimal gross, decimal employee, decimal employer, decimal net, decimal cost)
    {
        var fixture = BuildFixture(0m, 0m, null);
        var rules = fixture.Rules.Where(x => x.LineKind == SalaryTemplateLineKind.Earning).ToList();
        var result = SalaryTemplateCalculator.Calculate(basis, 10000m, rules, fixture.Components, null, [],
            BuildEsiContext(fixture.Components.Keys));
        result.Gross.ShouldBe(gross);
        result.Net.ShouldBe(net);
        result.EmployerCost.ShouldBe(cost);
        result.Deductions.Single().Amount.ShouldBe(employee);
        result.EmployerContributions.Single().Amount.ShouldBe(employer);
        result.Deductions.ShouldNotContain(x => x.Code == "EMPLOYER_ESI");
    }
	[Theory]
	[InlineData(SalaryInputBasis.Gross)]
	[InlineData(SalaryInputBasis.TakeHome)]
	[InlineData(SalaryInputBasis.EmployerCost)]
	public void Odd_paise_split_keeps_gross_and_awards_tie_to_ordinary_earning(SalaryInputBasis basis)
	{
		var basicId = Guid.NewGuid();
		var otherId = Guid.NewGuid();
		SalaryTemplateRule[] rules = [
			new("BASIC", basicId, SalaryTemplateLineKind.Earning, SalaryTemplateFormulaType.PercentageOfGross, PayrollComponentSemanticType.Basic, 50m, null, 0),
			new("OTHER", otherId, SalaryTemplateLineKind.Earning, SalaryTemplateFormulaType.PercentageOfGross, PayrollComponentSemanticType.Ordinary, 50m, null, 1)];
		var result = SalaryTemplateCalculator.Calculate(basis, 20000.01m, rules,
			new Dictionary<Guid, SalaryTemplateComponent> { [basicId] = new(basicId, "Basic"), [otherId] = new(otherId, "Other") }, null, []);
		result.Gross.ShouldBe(20000.01m);
		result.Basic.ShouldBe(10000m);
		result.Earnings.Single(x => x.ComponentId == otherId).Amount.ShouldBe(10000.01m);
		result.Earnings.Sum(x => x.Amount).ShouldBe(result.Gross);
	}

	[Fact]
	public void Should_reject_an_earning_that_depends_on_the_gross_remainder()
	{
		var basicId = Guid.NewGuid();
		var remainderId = Guid.NewGuid();
		var dependentId = Guid.NewGuid();
		var rules = new List<SalaryTemplateRule>
		{
			new("BASIC", basicId, SalaryTemplateLineKind.Earning, SalaryTemplateFormulaType.PercentageOfGross, PayrollComponentSemanticType.Basic, 60m, null, 0),
			new("REMAINDER", remainderId, SalaryTemplateLineKind.Earning, SalaryTemplateFormulaType.RemainderOfGross, PayrollComponentSemanticType.BalancingEarning, null, null, 1),
			new("DEPENDENT", dependentId, SalaryTemplateLineKind.Earning, SalaryTemplateFormulaType.PercentageOfComponent, PayrollComponentSemanticType.Ordinary, 10m, "REMAINDER", 2)
		};
		Should.Throw<BusinessException>(() => SalaryTemplateCalculator.ValidateDraftRules(rules))
			.Code.ShouldBe("HR:Payroll:SalaryTemplateDependencyCycle");
		// A deduction does not allocate gross, so it may legitimately reference the remainder.
		rules[2] = rules[2] with { LineKind = SalaryTemplateLineKind.Deduction };
		var result = SalaryTemplateCalculator.Calculate(SalaryInputBasis.Gross, 20000m, rules,
			new Dictionary<Guid, SalaryTemplateComponent>
			{
				[basicId] = new(basicId, "Basic"), [remainderId] = new(remainderId, "Remainder"),
				[dependentId] = new(dependentId, "Deduction")
			}, null, []);
		result.Deductions.Single().Amount.ShouldBe(800m);
	}

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

	[Theory]
	[InlineData(SalaryInputBasis.EmployerCost, 20000, 19370, 146, 630, 19224, 20000)]
	[InlineData(SalaryInputBasis.Gross, 20000, 20000, 150, 650, 19850, 20650)]
	[InlineData(SalaryInputBasis.TakeHome, 20000, 20152, 152, 655, 20000, 20807)]
	public void Should_solve_all_quote_bases_with_employee_and_employer_esi(
		SalaryInputBasis salaryBasis,
		decimal target,
		decimal expectedGross,
		decimal expectedEmployeeEsi,
		decimal expectedEmployerEsi,
		decimal expectedNet,
		decimal expectedEmployerCost)
	{
		var fixture = BuildFixture(employeePfPercentage: 0m, employerPfPercentage: 0m, wageCeiling: null);
		var esiContext = BuildEsiContext(fixture.Components.Keys);

		var result = SalaryTemplateCalculator.Calculate(
			salaryBasis,
			target,
			fixture.Rules,
			fixture.Components,
			pfRule: null,
			professionalTaxSlabs: [],
			esiContext);

		result.Gross.ShouldBe(expectedGross);
		result.Deductions.Single(x => x.Code == "EMPLOYEE_ESI").Amount.ShouldBe(expectedEmployeeEsi);
		result.EmployerContributions.Single(x => x.Code == "EMPLOYER_ESI").Amount.ShouldBe(expectedEmployerEsi);
		result.Net.ShouldBe(expectedNet);
		result.EmployerCost.ShouldBe(expectedEmployerCost);
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
	public void Should_allow_incomplete_drafts_but_reject_them_for_publishing()
	{
		SalaryTemplateCalculator.ValidateDraftRules([]);

		var incompleteRules = new List<SalaryTemplateRule>
		{
			new("BASIC", Guid.NewGuid(), SalaryTemplateLineKind.Earning,
				SalaryTemplateFormulaType.PercentageOfGross, PayrollComponentSemanticType.Basic,
				60m, null, 0)
		};

		SalaryTemplateCalculator.ValidateDraftRules(incompleteRules);
		SalaryTemplateCalculator.ValidateRules(incompleteRules);
		var error = Should.Throw<BusinessException>(() => SalaryTemplateCalculator.Calculate(
			SalaryInputBasis.Gross, 18000m, incompleteRules, new Dictionary<Guid, SalaryTemplateComponent>(), null, []));
		error.Code.ShouldBe(HRErrorCodes.SalaryTemplateAllocationMismatch);
		error.Data["gross"].ShouldBe(18000m);
		error.Data["allocated"].ShouldBe(10800m);
		error.Data["difference"].ShouldBe(7200m);
	}

	[Theory]
	[InlineData(SalaryInputBasis.Gross, 18000)]
	[InlineData(SalaryInputBasis.TakeHome, 16065)]
	[InlineData(SalaryInputBasis.EmployerCost, 20385)]
	public void Should_calculate_basic_pf_and_system_esi_without_balancing_earning(SalaryInputBasis basis, decimal target)
	{
		var fixture = BuildFixture(12m, 12m, 15000m);
		var rules = fixture.Rules.Where(x => x.Code is "BASIC" or "EMPLOYEE_PF")
			.Select(x => x.Code == "BASIC" ? x with { Value = 100m } : x).ToList();
		var result = SalaryTemplateCalculator.Calculate(basis, target, rules, fixture.Components, fixture.PfRule, [], BuildEsiContext(fixture.Components.Keys));
		result.Earnings.Count.ShouldBe(1);
		result.Gross.ShouldBe(18000m);
		result.TotalDeductions.ShouldBe(1935m);
		result.Net.ShouldBe(16065m);
		result.EmployerCost.ShouldBe(20385m);
	}

	[Theory]
	[InlineData(SalaryInputBasis.Gross, 18000)]
	[InlineData(SalaryInputBasis.TakeHome, 18000)]
	[InlineData(SalaryInputBasis.EmployerCost, 18000)]
	public void Should_support_fixed_only_allocations(SalaryInputBasis basis, decimal target)
	{
		var rules = new List<SalaryTemplateRule> { new("BASIC", Guid.NewGuid(), SalaryTemplateLineKind.Earning,
			SalaryTemplateFormulaType.FixedAmount, PayrollComponentSemanticType.Basic, 18000m, null, 0) };
		var result = SalaryTemplateCalculator.Calculate(basis, target, rules, new Dictionary<Guid, SalaryTemplateComponent>(), null, []);
		result.Gross.ShouldBe(18000m);
		result.Earnings.Count.ShouldBe(1);
	}

	[Theory]
	[InlineData(SalaryInputBasis.TakeHome, 20000)]
	[InlineData(SalaryInputBasis.TakeHome, 15000)]
	[InlineData(SalaryInputBasis.EmployerCost, 20000)]
	[InlineData(SalaryInputBasis.EmployerCost, 15000)]
	public void Should_reject_unreachable_fixed_target(SalaryInputBasis basis, decimal target)
	{
		var rules = new List<SalaryTemplateRule> { new("BASIC", Guid.NewGuid(), SalaryTemplateLineKind.Earning,
			SalaryTemplateFormulaType.FixedAmount, PayrollComponentSemanticType.Basic, 18000m, null, 0) };
		Should.Throw<BusinessException>(() => SalaryTemplateCalculator.Calculate(basis, target,
			rules, new Dictionary<Guid, SalaryTemplateComponent>(), null, [])).Code.ShouldBe("HR:Payroll:CompensationTargetNotSolvable");
	}

	[Fact]
	public void Should_calculate_arbitrary_template_lines_and_balance_to_gross()
	{
		var fixture = BuildFixture(employeePfPercentage: 12m, employerPfPercentage: 12m, wageCeiling: 15_000m);
		var hraId = Guid.NewGuid();
		var transportId = Guid.NewGuid();
		var loanId = Guid.NewGuid();
		fixture.Rules.Insert(1, new SalaryTemplateRule(
			"HRA", hraId, SalaryTemplateLineKind.Earning,
			SalaryTemplateFormulaType.PercentageOfGross, PayrollComponentSemanticType.Ordinary,
			20m, null, 2));
		fixture.Rules.Insert(2, new SalaryTemplateRule(
			"TRANSPORT", transportId, SalaryTemplateLineKind.Earning,
			SalaryTemplateFormulaType.FixedAmount, PayrollComponentSemanticType.Ordinary,
			1_500m, null, 3));
		fixture.Rules.Add(new SalaryTemplateRule(
			"LOAN", loanId, SalaryTemplateLineKind.Deduction,
			SalaryTemplateFormulaType.FixedAmount, PayrollComponentSemanticType.Ordinary,
			1_000m, null, 20));
		for (var index = 0; index < fixture.Rules.Count; index++)
		{
			fixture.Rules[index] = fixture.Rules[index] with { DisplayOrder = index };
		}
		fixture.Components[hraId] = new(hraId, "House Rent Allowance");
		fixture.Components[transportId] = new(transportId, "Transport Allowance");
		fixture.Components[loanId] = new(loanId, "Loan Recovery");

		var result = SalaryTemplateCalculator.Calculate(
			SalaryInputBasis.Gross,
			30_000m,
			fixture.Rules,
			fixture.Components,
			fixture.PfRule,
			fixture.ProfessionalTaxSlabs);

		result.Earnings.Single(x => x.Code == "HRA").Amount.ShouldBe(6_000m);
		result.Earnings.Single(x => x.Code == "TRANSPORT").Amount.ShouldBe(1_500m);
		result.Earnings.Single(x => x.Code == "SPECIAL_ALLOWANCE").Amount.ShouldBe(4_500m);
		result.Earnings.Sum(x => x.Amount).ShouldBe(30_000m);
		result.Deductions.Single(x => x.Code == "LOAN").Amount.ShouldBe(1_000m);
		result.Net.ShouldBe(27_000m);
		result.EmployerContributionTotal.ShouldBe(1_800m);
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

	[Theory]
	[InlineData(SalaryInputBasis.Gross, 20000, 30, 20000, 12000, 19500)]
	[InlineData(SalaryInputBasis.TakeHome, 19000, 15, 10000, 6000, 10000)]
	[InlineData(SalaryInputBasis.EmployerCost, 20000, 0, 0, 0, 500)]
	public void Should_rehydrate_gross_formulas_from_saved_compensation_and_copy_payslip_inputs(
		SalaryInputBasis basis, decimal input, decimal days, decimal gross, decimal basic, decimal net)
	{
		var fixture = BuildFixture(0m, 0m, null);
		fixture.Rules.RemoveAll(x => x.LineKind == SalaryTemplateLineKind.Deduction);
		var deductionId = Guid.NewGuid();
		fixture.Components[deductionId] = new(deductionId, "Gross deduction");
		fixture.Rules.Add(new("GROSS_DEDUCTION", deductionId, SalaryTemplateLineKind.Deduction,
			SalaryTemplateFormulaType.PercentageOfGross, PayrollComponentSemanticType.Ordinary, 5m, null, 3));
		var quote = SalaryTemplateCalculator.Calculate(basis, input, fixture.Rules, fixture.Components, null, []);
		quote.Gross.ShouldBe(20000m);
		var employeeId = Guid.NewGuid();
		var revision = new EmployeeCompensationRevision(Guid.NewGuid(), null, employeeId, 1, new DateTime(2026, 1, 1));
		revision.ReplaceDraft(new DateTime(2026, 1, 1), basis, CompensationProtectionPolicy.ProtectGross,
			input, quote.Gross, quote.Gross, quote.Net, quote.EmployerCost, null, null, null, null, "IN-GJ", JsonSerializer.Serialize(quote));
		var components = quote.Earnings.Concat(quote.Deductions).Select(line =>
		{
			PayrollComponentSemanticTypeCodes.TryParse(line.SemanticType, out var semantic);
			return new EmployeePayrollComponent(Guid.NewGuid(), employeeId, line.Name, line.ComponentId, null,
				line.Amount, true, false, null, SalaryComponentType.Fixed, SalaryComponentDependency.None,
				quote.Earnings.Contains(line), false, semantic);
		}).ToList();
		PayrollCompensationRevisionResolver.ApplySnapshotFormulas(revision, components);
		components.Add(new EmployeePayrollComponent(Guid.NewGuid(), employeeId, "Bonus", Guid.NewGuid(), null,
			500m, true, true, new DateTime(2026, 1, 1), SalaryComponentType.Fixed, SalaryComponentDependency.None, true));
		var result = PayrollComputation.Calculate(0, 0, days, 30m, components);
		result.FullGrossSalary.ShouldBe(20000m);
		result.GrossSalary.ShouldBe(gross);
		result.BasicSalary.ShouldBe(basic);
		result.AdditionalEarnings.ShouldBe(500m);
		result.NetPay.ShouldBe(net);
		var basicSlipLine = PayrollComputation.ToSalarySlipLineItem(result.Earnings.Single(x => x.Source.SemanticType == PayrollComponentSemanticType.Basic));
		var restored = JsonSerializer.Deserialize<ShikshaMitra.HR.Payroll.SalarySlips.SalarySlipLineItem>(JsonSerializer.Serialize(basicSlipLine))!;
		restored.FullMonthAmount.ShouldBe(12000m);
		restored.FormulaValue.ShouldBe(60m);
		restored.Amount.ShouldBe(basic);
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

	private static SalaryTemplateEsiEvaluationContext BuildEsiContext(IEnumerable<Guid> componentIds)
	{
		var tenantId = Guid.NewGuid();
		var rule = new StatutoryRuleVersion(
			Guid.NewGuid(), tenantId, StatutorySchemeCodes.EmployeeStateInsurance, "IN-GJ", 1,
			new DateTime(2025, 11, 21), null, 10, 21_000m, 25_000m, 0.75m, 3.25m,
			50m, 15m, StatutoryRoundingPolicy.NextHigherRupee,
			"https://www.labour.gov.in/", new DateTime(2026, 8, 18));
		var establishment = new StatutoryEstablishment(
			Guid.NewGuid(), tenantId, StatutorySchemeCodes.EmployeeStateInsurance, "IN-GJ");
		establishment.Configure("IN-GJ", "ESIC-GJ-001", 18, new DateTime(2026, 1, 1),
			new DateTime(2026, 1, 1), "Registration certificate");
		establishment.Activate(Guid.NewGuid(), new DateTime(2026, 1, 1));
		var enrollment = new EmployeeStatutoryEnrollment(
			Guid.NewGuid(), tenantId, Guid.NewGuid(), StatutorySchemeCodes.EmployeeStateInsurance);
		enrollment.Update(
			EmployeeStatutoryEnrollmentStatus.Covered,
			StatutoryWageCeilingCategory.Standard,
			"ESI-1001",
			new DateTime(2026, 4, 1),
			null,
			null,
			null);

		return new SalaryTemplateEsiEvaluationContext(
			new DateTime(2026, 8, 1),
			rule,
			establishment,
			enrollment,
			componentIds.ToDictionary(x => x, _ => StatutoryWageTreatment.Included),
			[],
			Guid.NewGuid(),
			"Employee State Insurance (ESI)");
	}

	private sealed record SalaryFixture(
		List<SalaryTemplateRule> Rules,
		Dictionary<Guid, SalaryTemplateComponent> Components,
		ProvidentFund PfRule,
		List<ProfessionalTax> ProfessionalTaxSlabs);
}
