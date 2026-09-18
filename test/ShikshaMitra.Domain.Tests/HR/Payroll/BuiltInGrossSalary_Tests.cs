using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class BuiltInGrossSalary_Tests
{
	[Fact]
	public void Remainder_does_not_hide_a_genuine_earning_overallocation()
	{
		var components = Structure();
		components[1].Formula = new PayrollComponentFormula(SalaryTemplateFormulaType.PercentageOfGross, 50m);
		var remainder = Component("Remainder", 0m, true);
		remainder.Formula = new PayrollComponentFormula(SalaryTemplateFormulaType.RemainderOfGross, null);
		components.Add(remainder);
		Should.Throw<BusinessException>(() => PayrollComputation.Calculate(15, 0, 15m, 30m, components))
			.Code.ShouldBe("HR:Payroll:SalaryTemplateEarningsExceedGross");
	}

	[Fact]
	public void Rounded_earnings_with_zero_remainder_reconcile_after_proration()
	{
		var components = new List<EmployeePayrollComponent>
		{
			Component("Basic", 10000.01m, true, 50m, PayrollComponentSemanticType.Basic),
			Component("Allowance", 10000.01m, true, 50m),
			Component("Remainder", 0m, true)
		};
		components[2].Formula = new PayrollComponentFormula(SalaryTemplateFormulaType.RemainderOfGross, null);
		var result = PayrollComputation.Calculate(1, 0, 1m, 26m, components);
		result.GrossSalary.ShouldBe(769.23m);
		result.Earnings.Sum(x => x.Amount).ShouldBe(769.23m);
		result.Earnings.ShouldAllBe(x => x.Amount >= 0m);
		result.Earnings.Single(x => x.Source.Name == "Remainder").Amount.ShouldBe(0m);
	}

	[Theory]
	[InlineData(30, 20000, 12000, 1000, 19000)]
	[InlineData(15, 10000, 6000, 500, 9500)]
	[InlineData(0, 0, 0, 0, 0)]
	public void Gross_percentages_use_earned_salary_once(int days, decimal gross, decimal basic, decimal deduction, decimal net)
	{
		var result = PayrollComputation.Calculate(days, 0, days, 30m, Structure());
		result.FullGrossSalary.ShouldBe(20000m);
		result.GrossSalary.ShouldBe(gross);
		result.BasicSalary.ShouldBe(basic);
		result.TotalDeduction.ShouldBe(deduction);
		result.NetPay.ShouldBe(net);
		result.Earnings.Sum(x => x.Amount).ShouldBe(gross);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(15)]
	[InlineData(30)]
	public void Bonus_does_not_increase_gross_or_gross_based_deductions(int days)
	{
		var components = Structure();
		var bonus = Component("Bonus", 2000m, true);
		bonus.IsTemp = true;
		components.Add(bonus);
		var result = PayrollComputation.Calculate(days, 0, days, 30m, components);
		result.FullGrossSalary.ShouldBe(20000m);
		result.GrossSalary.ShouldBe(20000m * days / 30m);
		result.TotalDeduction.ShouldBe(1000m * days / 30m);
		result.AdditionalEarnings.ShouldBe(2000m);
		result.TotalEarnings.ShouldBe(result.GrossSalary + 2000m);
		result.NetPay.ShouldBe(result.TotalEarnings - result.TotalDeduction);
	}

	[Theory]
	[InlineData(28)]
	[InlineData(29)]
	[InlineData(30)]
	[InlineData(31)]
	public void Basis_and_fractional_payable_days_reconcile_to_rounded_gross(int basis)
	{
		var result = PayrollComputation.Calculate(10, 1, 10.5m, basis, Structure());
		result.GrossSalary.ShouldBe(Math.Round(20000m * 10.5m / basis, 2, MidpointRounding.AwayFromZero));
		result.Earnings.Sum(x => x.Amount).ShouldBe(result.GrossSalary);
	}

	[Fact]
	public void Remainder_and_component_dependencies_resolve_independent_of_display_order()
	{
		var components = Structure();
		components[1].Formula = new PayrollComponentFormula(SalaryTemplateFormulaType.RemainderOfGross, null);
		components[2].Formula = new PayrollComponentFormula(SalaryTemplateFormulaType.PercentageOfComponent, 5m, components[0].ComponentId);
		components.Reverse();
		var result = PayrollComputation.Calculate(15, 0, 15m, 30m, components);
		result.Earnings.Single(x => x.Source.Name == "Allowance").Amount.ShouldBe(4000m);
		result.TotalDeduction.ShouldBe(300m);
	}

	[Fact]
	public void Missing_and_circular_bases_are_rejected()
	{
		var components = Structure();
		components[0].Formula = new PayrollComponentFormula(SalaryTemplateFormulaType.PercentageOfComponent, 60m, Guid.NewGuid());
		Should.Throw<BusinessException>(() => PayrollComputation.Calculate(30, 0, 30m, 30m, components))
			.Code.ShouldBe(HRErrorCodes.PayrollComponentDependencyTargetInvalid);
		components[0].Formula = new PayrollComponentFormula(SalaryTemplateFormulaType.PercentageOfComponent, 60m, components[1].ComponentId);
		components[1].Formula = new PayrollComponentFormula(SalaryTemplateFormulaType.PercentageOfComponent, 40m, components[0].ComponentId);
		Should.Throw<BusinessException>(() => PayrollComputation.Calculate(30, 0, 30m, 30m, components))
			.Code.ShouldBe(HRErrorCodes.PayrollComponentDependencyCycle);
	}

	[Fact]
	public void Incomplete_allocation_is_not_hidden_as_rounding()
	{
		var components = Structure();
		components[1].Formula = new PayrollComponentFormula(SalaryTemplateFormulaType.PercentageOfGross, 30m);
		Should.Throw<BusinessException>(() => PayrollComputation.Calculate(30, 0, 30m, 30m, components))
			.Code.ShouldBe(HRErrorCodes.SalaryTemplateAllocationMismatch);
	}

	[Theory]
	[InlineData(EarningComponentType.Fixed, DeductionComponentType.Fixed)]
	[InlineData(EarningComponentType.Variable, DeductionComponentType.Variable)]
	public void Component_settings_accept_system_gross_without_a_component_id(EarningComponentType earningType, DeductionComponentType deductionType)
	{
		var earning = new EarningComponent(Guid.NewGuid(), null, "Basic", null, earningType,
			CalculationType.Percentage, null, 60m, null, ComponentDependency.Gross, true);
		var deduction = new DeductionComponent(Guid.NewGuid(), null, "Recovery", null, deductionType,
			CalculationType.Percentage, null, 5m, null, ComponentDependency.Gross, true);
		earning.DependOn.ShouldBe(ComponentDependency.Gross);
		deduction.DependOn.ShouldBe(ComponentDependency.Gross);
		earning.SelectedEarningComponentId.ShouldBeNull();
		deduction.SelectedDeductionComponentId.ShouldBeNull();
	}

	private static List<EmployeePayrollComponent> Structure() =>
	[
		Component("Basic", 12000m, true, 60m, PayrollComponentSemanticType.Basic),
		Component("Allowance", 8000m, true, 40m),
		Component("Recovery", 1000m, false, 5m)
	];

	private static EmployeePayrollComponent Component(string name, decimal amount, bool earning, decimal? rate = null,
		PayrollComponentSemanticType semantic = PayrollComponentSemanticType.Ordinary) => new(
		Guid.NewGuid(), Guid.NewGuid(), name, Guid.NewGuid(), null, amount, true, false, null,
		SalaryComponentType.Fixed, SalaryComponentDependency.None, earning, false, semantic)
	{
		Formula = rate.HasValue ? new PayrollComponentFormula(SalaryTemplateFormulaType.PercentageOfGross, rate) : null
	};
}
