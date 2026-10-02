using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollSalaryProtectionTests
{
	[Theory]
	[InlineData(CompensationProtectionPolicy.ProtectEmployerCost, 26, 20000, 19800)]
	[InlineData(CompensationProtectionPolicy.ProtectTakeHome, 26, 20200, 20000)]
	[InlineData(CompensationProtectionPolicy.ProtectEmployerCost, 13, 10000, 9800)]
	[InlineData(CompensationProtectionPolicy.ProtectTakeHome, 13, 10200, 10000)]
	public void Protects_period_target_without_prorating_PT(CompensationProtectionPolicy policy, int days, decimal gross, decimal net)
	{
		var result = PayrollSalaryProtection.Calculate(days, 0, days, 26, Components(), Rules(), policy, 20000m);
		result.GrossSalary.ShouldBe(gross);
		result.NetPay.ShouldBe(net);
		result.Protection!.Difference.ShouldBe(0m);
		result.Deductions.Single().Amount.ShouldBe(200m);
	}

	[Fact]
	public void Other_deductions_reduce_payout_and_bonus_stays_outside_protected_target()
	{
		var components = Components();
		components.Add(Component("Loan", 500m, false));
		var bonus = Component("Bonus", 1000m, true); bonus.IsTemp = true; components.Add(bonus);
		var result = PayrollSalaryProtection.Calculate(26, 0, 26, 26, components, Rules(), CompensationProtectionPolicy.ProtectTakeHome, 20000m);
		result.NetPay.ShouldBe(20500m);
		result.Protection!.Achieved.ShouldBe(20000m);
		result.Protection.ExcludedDeductions.ShouldBe(500m);
		result.Protection.AdditionalEarnings.ShouldBe(1000m);
	}

	[Theory]
	[InlineData(CompensationProtectionPolicy.ProtectEmployerCost)]
	[InlineData(CompensationProtectionPolicy.ProtectTakeHome)]
	public void Recalculates_both_employee_and_employer_PF(CompensationProtectionPolicy policy)
	{
		var components = Components();
		components.Add(Component("PF", 0m, false, PayrollComponentSemanticType.EmployeeProvidentFund));
		var rules = Rules() with { ProvidentFund = new ProvidentFund(Guid.NewGuid(), null, "PF", "PF", 12m, 12m) };
		var result = PayrollSalaryProtection.Calculate(26, 0, 26, 26, components, rules, policy, 20000m);
		Math.Abs(result.Protection!.Difference).ShouldBeLessThanOrEqualTo(0.01m);
		if (policy == CompensationProtectionPolicy.ProtectEmployerCost)
			(result.GrossSalary + result.EmployerContributionTotal).ShouldBeLessThanOrEqualTo(20000m);
		else result.StatutoryTakeHome.ShouldBeGreaterThanOrEqualTo(20000m);
		result.EmployerContributionTotal.ShouldBeGreaterThan(0m);
	}

	[Fact]
	public void Fixed_earning_that_cannot_restore_target_is_blocked_without_inventing_an_allowance()
	{
		var components = Components();
		components[0].Formula = new PayrollComponentFormula(SalaryTemplateFormulaType.FixedAmount, 20000m);
		var result = PayrollSalaryProtection.Calculate(26, 0, 26, 26, components, Rules(), CompensationProtectionPolicy.ProtectTakeHome, 20000m);
		result.GrossSalary.ShouldBe(20000m);
		result.Protection!.Status.ShouldBe("BLOCKED");
		result.Protection.Difference.ShouldBe(-200m);
	}

	[Fact]
	public void Handles_PT_slab_discontinuity_and_additional_tax_on_bonus()
	{
		var components = Components();
		var bonus = Component("Bonus", 2000m, true); bonus.IsTemp = true; components.Add(bonus);
		var slabs = new[] { Tax(0m, 20999.99m, 200m), Tax(21000m, null, 300m) };
		var result = PayrollSalaryProtection.Calculate(26, 0, 26, 26, components,
			new PayrollStatutoryRuleSet(null, slabs), CompensationProtectionPolicy.ProtectTakeHome, 20000m);
		result.GrossSalary.ShouldBe(20200m);
		result.NetPay.ShouldBe(21900m);
		result.Protection!.AdditionalEmployeeStatutoryDeductions.ShouldBe(100m);
	}

	[Fact]
	public void Zero_payable_days_and_zero_tax_leave_the_protected_target_at_zero()
	{
		var result = PayrollSalaryProtection.Calculate(0, 0, 0m, 26m, Components(),
			new PayrollStatutoryRuleSet(null, [Tax(0m, null, 0m)]), CompensationProtectionPolicy.ProtectTakeHome, 20000m);
		result.NetPay.ShouldBe(0m);
		result.Protection!.PeriodTarget.ShouldBe(0m);
		result.Protection.Difference.ShouldBe(0m);
	}

	[Fact]
	public void Unresolved_target_blocks_payroll_locking()
	{
		RunPayroll.PayrollLockPreconditionGuard.HasBlockingLockIssue("[\"COMPENSATION_TARGET_VARIANCE\"]").ShouldBeTrue();
	}

	[Fact]
	public void Missing_statutory_configuration_blocks_protection()
	{
		var result = PayrollSalaryProtection.Calculate(26, 0, 26, 26, Components(), null, CompensationProtectionPolicy.ProtectEmployerCost, 20000m);
		result.Protection!.Status.ShouldBe("BLOCKED");
	}

	private static List<EmployeePayrollComponent> Components()
	{
		var basic = Component("Basic", 20000m, true, PayrollComponentSemanticType.Basic);
		basic.Formula = new PayrollComponentFormula(SalaryTemplateFormulaType.PercentageOfGross, 100m);
		return [basic, Component("PT", 0m, false, PayrollComponentSemanticType.ProfessionalTax)];
	}
	private static PayrollStatutoryRuleSet Rules() => new(null, [Tax(0m, null, 200m)]);
	private static ProfessionalTax Tax(decimal from, decimal? to, decimal amount) => new(Guid.NewGuid(), null,
		"IN-GJ", new DateTime(2020, 1, 1), null, from, to, amount);
	private static EmployeePayrollComponent Component(string name, decimal amount, bool earning,
		PayrollComponentSemanticType semantic = PayrollComponentSemanticType.Ordinary) => new(
		Guid.NewGuid(), Guid.NewGuid(), name, Guid.NewGuid(), null, amount, true, false, null,
		SalaryComponentType.Fixed, SalaryComponentDependency.None, earning, false, semantic);
}
