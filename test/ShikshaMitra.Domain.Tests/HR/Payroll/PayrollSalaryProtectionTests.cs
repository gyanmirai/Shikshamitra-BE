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
	[InlineData(26, 20000, 17857, 2143)]
	[InlineData(13, 10000, 8929, 1071)]
	public void CTC_is_prorated_once_with_whole_rupee_PF(int payableDays, decimal target, decimal gross, decimal pf)
	{
		var components = Components();
		components.Add(Component("PF", 0m, false, PayrollComponentSemanticType.EmployeeProvidentFund));
		var rules = Rules() with { ProvidentFund = new ProvidentFund(Guid.NewGuid(), null, "PF", "PF", 12m, 12m) };

		var result = PayrollSalaryProtection.Calculate(payableDays, 0, payableDays, 26m, components, rules, 20000m);

		result.GrossSalary.ShouldBe(gross);
		result.EmployerContributionTotal.ShouldBe(pf);
		result.Deductions.Single(x => x.Source.SemanticType == PayrollComponentSemanticType.EmployeeProvidentFund).Amount.ShouldBe(pf);
		result.Deductions.Single(x => x.Source.SemanticType == PayrollComponentSemanticType.ProfessionalTax).Amount.ShouldBe(200m);
		result.Protection!.PeriodTarget.ShouldBe(target);
		result.Protection.Achieved.ShouldBe(target);
		result.Protection.Difference.ShouldBe(0m);
		result.Protection.Status.ShouldBe("ADJUSTED");
	}

	[Fact]
	public void Bonus_and_its_tax_are_excluded_from_recurring_CTC()
	{
		var components = Components();
		components.Add(Component("Loan", 500m, false));
		var bonus = Component("Bonus", 2000m, true);
		bonus.IsTemp = true;
		components.Add(bonus);
		var slabs = new[] { Tax(0m, 20999.99m, 200m), Tax(21000m, null, 300m) };

		var result = PayrollSalaryProtection.Calculate(26, 0, 26, 26, components, new(null, slabs), 20000m);

		result.NetPay.ShouldBe(21200m);
		result.Protection!.Achieved.ShouldBe(20000m);
		result.Protection.ExcludedDeductions.ShouldBe(500m);
		result.Protection.AdditionalEarnings.ShouldBe(2000m);
		result.Protection.AdditionalEmployeeStatutoryDeductions.ShouldBe(100m);
	}

	[Fact]
	public void Fixed_earning_that_cannot_restore_CTC_is_blocked_without_inventing_an_allowance()
	{
		var components = Components();
		components[0].Formula = new PayrollComponentFormula(SalaryTemplateFormulaType.FixedAmount, 20000m);
		components.Add(Component("PF", 0m, false, PayrollComponentSemanticType.EmployeeProvidentFund));
		var rules = Rules() with { ProvidentFund = new ProvidentFund(Guid.NewGuid(), null, "PF", "PF", 12m, 12m) };

		var result = PayrollSalaryProtection.Calculate(26, 0, 26, 26, components, rules, 20000m);

		result.GrossSalary.ShouldBe(20000m);
		result.Protection!.Status.ShouldBe("BLOCKED");
		result.Protection.Difference.ShouldBe(2400m);
	}

	[Fact]
	public void Zero_payable_days_leave_the_protected_target_at_zero()
	{
		var result = PayrollSalaryProtection.Calculate(0, 0, 0m, 26m, Components(),
			new PayrollStatutoryRuleSet(null, [Tax(0m, null, 0m)]), 20000m);
		result.NetPay.ShouldBe(0m);
		result.Protection!.PeriodTarget.ShouldBe(0m);
		result.Protection.Difference.ShouldBe(0m);
	}

	[Fact]
	public void Missing_statutory_configuration_blocks_protection()
	{
		var components = Components();
		components.Add(Component("PF", 0m, false, PayrollComponentSemanticType.EmployeeProvidentFund));
		var result = PayrollSalaryProtection.Calculate(26, 0, 26, 26, components, null, 20000m);
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
