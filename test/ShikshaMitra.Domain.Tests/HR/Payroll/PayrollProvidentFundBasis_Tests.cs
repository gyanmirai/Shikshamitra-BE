using System;
using System.Collections.Generic;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollProvidentFundBasis_Tests
{
	[Theory]
	[InlineData(PfContributionBasis.Basic, 26, 12, 1200)]
	[InlineData(PfContributionBasis.Basic, 13, 12, 600)]
	[InlineData(PfContributionBasis.Basic, 0, 12, 0)]
	[InlineData(PfContributionBasis.BasicAndDearnessAllowance, 26, 12, 1440)]
	[InlineData(PfContributionBasis.BasicAndDearnessAllowance, 13, 12, 720)]
	[InlineData(PfContributionBasis.BasicAndDearnessAllowance, 0, 12, 0)]
	[InlineData(PfContributionBasis.Basic, 26, 0, 0)]
	public void Contributions_use_only_earned_recurring_basis(PfContributionBasis basis, decimal payable, decimal employeePercent, decimal expectedEmployee)
	{
		var rule = new ProvidentFund(Guid.NewGuid(), null, "TEST", "PF", employeePercent, 12m, basis);
		var components = new List<EmployeePayrollComponent>
		{
			Component("Basic Pay", 10000m, PayrollComponentSemanticType.Ordinary),
			Component("DA", 2000m, PayrollComponentSemanticType.DearnessAllowance),
			Component("Basic adjustment", 1000m, PayrollComponentSemanticType.Basic, temporary: true),
			Component("DA bonus", 500m, PayrollComponentSemanticType.DearnessAllowance, temporary: true),
			Component("PF", 0m, PayrollComponentSemanticType.EmployeeProvidentFund, earning: false)
		};
		var result = PayrollComputation.Calculate(0, 0, payable, 26m, components, new(rule, []));
		result.TotalDeduction.ShouldBe(expectedEmployee);
		result.EmployerContributionTotal.ShouldBe((basis == PfContributionBasis.Basic ? 10000m : 12000m) * payable / 26m * 0.12m);
		result.ProvidentFundRuleId.ShouldBe(rule.Id);
		result.ProvidentFundRuleVersion.ShouldBe(rule.Version);
		result.StatutoryIssueCodes.ShouldNotContain("PF_WAGE_BASIS_REQUIRED");
	}

	[Theory]
	[InlineData(PfContributionBasis.Basic)]
	[InlineData(PfContributionBasis.BasicAndDearnessAllowance)]
	public void Temporary_basis_and_unclassified_salary_cannot_enable_gross_fallback(PfContributionBasis basis)
	{
		var rule = new ProvidentFund(Guid.NewGuid(), null, "TEST", "PF", 12m, 12m, basis, 15000m);
		var result = PayrollComputation.Calculate(26, 0, 26m, 26m,
			[Component("Consolidated Pay", 30000m, PayrollComponentSemanticType.Ordinary),
			 Component("Basic bonus", 1000m, PayrollComponentSemanticType.Basic, temporary: true),
			 Component("PF", 0m, PayrollComponentSemanticType.EmployeeProvidentFund, earning: false)], new(rule, []));
		result.Deductions.ShouldBeEmpty();
		result.EmployerContributions.ShouldBeEmpty();
		result.StatutoryIssueCodes.ShouldContain("PF_WAGE_BASIS_REQUIRED");
		PayrollLockPreconditionGuard.IsBlockingLockIssue("PF_WAGE_BASIS_REQUIRED").ShouldBeTrue();
	}

	[Fact]
	public void Wage_ceiling_and_money_rounding_apply_after_earned_basis()
	{
		var rule = new ProvidentFund(Guid.NewGuid(), null, "TEST", "PF", 12m, 12m, PfContributionBasis.Basic, 15000m);
		var components = new[] { Component("Basic", 30000m, PayrollComponentSemanticType.Basic),
			Component("PF", 0m, PayrollComponentSemanticType.EmployeeProvidentFund, earning: false) };
		PayrollComputation.Calculate(26, 0, 26m, 26m, components, new(rule, [])).TotalDeduction.ShouldBe(1800m);
		PayrollComputation.Calculate(1, 0, 1m, 26m, components, new(rule, [])).TotalDeduction.ShouldBe(138.46m);
	}

	private static EmployeePayrollComponent Component(string name, decimal amount, PayrollComponentSemanticType semantic, bool temporary = false, bool earning = true) =>
		new(Guid.NewGuid(), Guid.NewGuid(), name, null, null, amount, true, temporary, null,
			SalaryComponentType.Fixed, SalaryComponentDependency.None, earning, semanticType: semantic);
}
