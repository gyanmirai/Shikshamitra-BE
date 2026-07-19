using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollProvidentFundComponentBuilder(
	IProvidentFundRepository providentFundRepository)
	: ITransientDependency
{
	public async Task<List<EmployeePayrollComponent>> AddProvidentFundDeductionAsync(
		EmployeePayrollDetails? payrollDetails,
		List<EmployeePayrollComponent> components,
		int year,
		int month)
	{
		var result = components.ToList();
		if (payrollDetails == null ||
		    string.IsNullOrWhiteSpace(payrollDetails.PfRule) ||
		    !Guid.TryParse(payrollDetails.PfRule, out var pfRuleId))
		{
			return result;
		}

		var rule = await providentFundRepository.FindAsync(pfRuleId);
		if (rule == null || rule.EmployeePercentage <= 0)
		{
			return result;
		}

		var baseAmount = ResolveProvidentFundBaseAmount(result);
		if (baseAmount <= 0m)
		{
			return result;
		}

		var amount = Math.Round(baseAmount * (decimal)rule.EmployeePercentage / 100m, 2, MidpointRounding.AwayFromZero);
		if (amount <= 0m)
		{
			return result;
		}

		result.Add(new EmployeePayrollComponent(
			Guid.NewGuid(),
			payrollDetails.EmployeeId,
			$"Provident Fund - {rule.Name}",
			componentId: null,
			description: $"{rule.EmployeePercentage:0.##}% employee PF contribution",
			amount,
			isFixed: true,
			isTemp: false,
			applicableMonth: new DateTime(year, month, 1),
			SalaryComponentType.Fixed,
			SalaryComponentDependency.None,
			isEarning: false,
			usesMasterValue: false));

		return result;
	}

	private static decimal ResolveProvidentFundBaseAmount(IReadOnlyList<EmployeePayrollComponent> components)
	{
		var basic = components
			.Where(x => x.IsEarning && IsBasicSalaryComponentName(x.Name))
			.Sum(x => x.Amount);

		return basic > 0m
			? basic
			: components.Where(x => x.IsEarning).Sum(x => x.Amount);
	}

	private static bool IsBasicSalaryComponentName(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return false;
		}

		return string.Equals(name, "Basic", StringComparison.OrdinalIgnoreCase)
			|| name.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase);
	}
}
