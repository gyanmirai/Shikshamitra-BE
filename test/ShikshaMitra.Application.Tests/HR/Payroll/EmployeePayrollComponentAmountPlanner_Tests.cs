using System;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class EmployeePayrollComponentAmountPlanner_Tests
{
	[Fact]
	public void Should_Preserve_Generated_Amount_When_Master_Is_Percentage_Based()
	{
		var tenantId = Guid.NewGuid();
		var basicId = Guid.NewGuid();
		var allowanceId = Guid.NewGuid();
		var basic = new EarningComponent(
			basicId, tenantId, "Basic Salary", null, EarningComponentType.Fixed,
			CalculationType.Amount, 1000m, null, null, ComponentDependency.None, true);
		var allowance = new EarningComponent(
			allowanceId, tenantId, "Special Allowance", null, EarningComponentType.Variable,
			CalculationType.Percentage, null, 10m, basicId, ComponentDependency.Basic, true);
		var lines = new[]
		{
			new EmployeePayrollResolvedLine<EarningComponent>(
				new SalaryComponentContract
				{
					ComponentId = basicId,
					Name = "Basic Salary",
					Amount = 18000m,
					IsTemp = false,
					UsesMasterValue = false
				}, basic, null),
			new EmployeePayrollResolvedLine<EarningComponent>(
				new SalaryComponentContract
				{
					ComponentId = allowanceId,
					Name = "Special Allowance",
					Amount = 12000m,
					IsTemp = false,
					UsesMasterValue = false
				}, allowance, null)
		};

		var plan = EmployeePayrollComponentAmountPlanner.Create(lines, []);

		plan.EarningAmountsByComponentId[basicId].ShouldBe(18000m);
		plan.EarningAmountsByComponentId[allowanceId].ShouldBe(12000m);
	}

	[Fact]
	public void Should_Allow_Zero_Amount_For_A_Statutory_Marker_Line()
	{
		var deduction = new EmployeePayrollResolvedLine<DeductionComponent>(
			new SalaryComponentContract
			{
				Name = "Professional Tax",
				Amount = 0m,
				IsTemp = false,
				UsesMasterValue = false,
				SemanticType = PayrollComponentSemanticType.ProfessionalTax
			}, null, null);

		Should.NotThrow(() => EmployeePayrollComponentAmountPlanner.Create([], [deduction]));
	}
}
