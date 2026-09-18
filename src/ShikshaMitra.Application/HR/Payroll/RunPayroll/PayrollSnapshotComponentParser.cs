using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

internal static class PayrollSnapshotComponentParser
{
	public static List<SalarySlipLineItem> BuildSalarySlipLineItems(string componentsJson)
	{
		var components = JsonSerializer.Deserialize<List<RunPayrollComponentItem>>(componentsJson)
			?? new List<RunPayrollComponentItem>();

		return components
			.Select(x => new SalarySlipLineItem(
				x.Name,
				Math.Round((decimal)x.Amount, 2, MidpointRounding.AwayFromZero),
				x.Description,
				x.IsFixed,
				x.IsTemp,
				ParseSalaryComponentType(x.ComponentType),
				ParseSalaryComponentDependency(x.DependOn))
			{
				FullMonthAmount = x.FullMonthAmount,
				FormulaType = x.FormulaType,
				FormulaValue = x.FormulaValue,
				BasisComponentId = x.BasisComponentId
			})
			.ToList();
	}

	private static SalaryComponentType ParseSalaryComponentType(string? value) =>
		Enum.TryParse<SalaryComponentType>(value, ignoreCase: true, out var componentType)
			? componentType
			: SalaryComponentType.Fixed;

	private static SalaryComponentDependency ParseSalaryComponentDependency(string? value) =>
		Enum.TryParse<SalaryComponentDependency>(value, ignoreCase: true, out var dependency)
			? dependency
			: SalaryComponentDependency.None;
}
