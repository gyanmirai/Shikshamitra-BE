using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.SalarySlips;

namespace ShikshaMitra.HR.Payroll.Services;

public static class PayrollComputation
{
	public static PayrollComputationResult Calculate(
		int presentDays,
		int halfWorkingDays,
		decimal basisDays,
		IReadOnlyList<EmployeePayrollComponent> components)
	{
		var payableDays = PayrollProrationCalculator.CalculatePayableDays(presentDays, halfWorkingDays);
		return Calculate(presentDays, halfWorkingDays, payableDays, basisDays, components);
	}

	public static PayrollComputationResult Calculate(
		int presentDays,
		int halfWorkingDays,
		decimal payableDays,
		decimal basisDays,
		IReadOnlyList<EmployeePayrollComponent> components)
	{
		var factor = PayrollProrationCalculator.CalculateProrationFactor(basisDays, payableDays);
		var earnings = new List<PayrollComputedLineItem>();
		var deductions = new List<PayrollComputedLineItem>();
		var fullGrossSalary = components
			.Where(c => c.IsEarning)
			.Sum(c => c.Amount);
		var grossSalary = 0m;
		var totalDeduction = 0m;

		foreach (var component in components)
		{
			var proratedAmount = PayrollProrationCalculator.ProrateAmount(component.Amount, component.IsTemp, factor);
			var lineItem = new PayrollComputedLineItem(component, proratedAmount);
			if (component.IsEarning)
			{
				earnings.Add(lineItem);
				grossSalary += proratedAmount;
			}
			else
			{
				deductions.Add(lineItem);
				totalDeduction += proratedAmount;
			}
		}

		var basicSalaryBase = components
			.Where(c => c.IsEarning && IsBasicSalaryComponentName(c.Name))
			.Sum(c => c.Amount);
		var basicSalary = PayrollProrationCalculator.ProrateAmount(basicSalaryBase, isTemp: false, factor);

		return new PayrollComputationResult(
			presentDays,
			halfWorkingDays,
			payableDays,
			factor,
			basicSalary,
			fullGrossSalary,
			grossSalary,
			totalDeduction,
			grossSalary - totalDeduction,
			earnings,
			deductions);
	}

	public static SalarySlipLineItem ToSalarySlipLineItem(PayrollComputedLineItem item) =>
		new(
			item.Source.Name,
			item.Amount,
			item.Source.Description,
			item.Source.IsFixed,
			item.Source.IsTemp,
			item.Source.ComponentType,
			item.Source.DependOn ?? SalaryComponentDependency.None);

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
