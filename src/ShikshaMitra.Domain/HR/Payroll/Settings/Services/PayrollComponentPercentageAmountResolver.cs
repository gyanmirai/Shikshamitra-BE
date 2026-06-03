using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees.Payroll;

namespace ShikshaMitra.HR.Payroll.Settings;

internal static class PayrollComponentPercentageAmountResolver
{
	public static List<EmployeePayrollComponent> ResolvePercentageAmounts(
		IReadOnlyList<EmployeePayrollComponent> components,
		IReadOnlyDictionary<Guid, PayrollComponentVersion> versionByComponentId)
	{
		var earningAmountsByComponentId = new Dictionary<Guid, decimal>();
		var allAmountsByComponentId = new Dictionary<Guid, decimal>();
		var resolvedEarnings = new List<EmployeePayrollComponent>();
		foreach (var earning in components.Where(x => x.IsEarning))
		{
			var amount = earning.ComponentType == SalaryComponentType.Percentage
				? CalculatePercentageAmount(
					ResolvePercentageBase(earning, versionByComponentId, resolvedEarnings, earningAmountsByComponentId, allAmountsByComponentId),
					earning.Amount)
				: earning.Amount;
			var resolved = CopyWithAmount(earning, amount);
			resolvedEarnings.Add(resolved);
			if (resolved.ComponentId.HasValue)
			{
				earningAmountsByComponentId[resolved.ComponentId.Value] = amount;
				allAmountsByComponentId[resolved.ComponentId.Value] = amount;
			}
		}

		var grossAmount = resolvedEarnings.Sum(x => x.Amount);
		var resolvedById = resolvedEarnings.ToDictionary(x => x.Id);
		foreach (var deduction in components.Where(x => !x.IsEarning))
		{
			var amount = deduction.ComponentType == SalaryComponentType.Percentage
				? CalculatePercentageAmount(
					ResolvePercentageBase(deduction, versionByComponentId, resolvedEarnings, earningAmountsByComponentId, allAmountsByComponentId, grossAmount),
					deduction.Amount)
				: deduction.Amount;
			var resolved = CopyWithAmount(deduction, amount);
			resolvedById[deduction.Id] = resolved;
			if (resolved.ComponentId.HasValue)
			{
				allAmountsByComponentId[resolved.ComponentId.Value] = amount;
			}
		}

		return components.Select(x => resolvedById.GetValueOrDefault(x.Id) ?? x).ToList();
	}

	private static decimal ResolvePercentageBase(
		EmployeePayrollComponent component,
		IReadOnlyDictionary<Guid, PayrollComponentVersion> versionByComponentId,
		IReadOnlyList<EmployeePayrollComponent> resolvedEarnings,
		IReadOnlyDictionary<Guid, decimal> earningAmountsByComponentId,
		IReadOnlyDictionary<Guid, decimal> allAmountsByComponentId,
		decimal? grossAmount = null)
	{
		if (versionByComponentId.TryGetValue(component.Id, out var version) &&
		    version.DependOn == ComponentDependency.CustomComponent &&
		    version.SelectedComponentId.HasValue &&
		    allAmountsByComponentId.TryGetValue(version.SelectedComponentId.Value, out var selectedAmount))
		{
			return selectedAmount;
		}

		return ResolvePercentageBase(
			component.DependOn,
			resolvedEarnings,
			earningAmountsByComponentId,
			grossAmount);
	}

	private static decimal ResolvePercentageBase(
		SalaryComponentDependency? dependency,
		IReadOnlyList<EmployeePayrollComponent> resolvedEarnings,
		IReadOnlyDictionary<Guid, decimal> earningAmountsByComponentId,
		decimal? grossAmount = null)
	{
		return dependency switch
		{
			SalaryComponentDependency.Basic => ResolveBasicAmount(resolvedEarnings, earningAmountsByComponentId),
			SalaryComponentDependency.Gross => grossAmount ?? resolvedEarnings.Sum(x => x.Amount),
			_ => 0m
		};
	}

	private static decimal ResolveBasicAmount(
		IReadOnlyList<EmployeePayrollComponent> resolvedEarnings,
		IReadOnlyDictionary<Guid, decimal> earningAmountsByComponentId)
	{
		var basic = resolvedEarnings.FirstOrDefault(x =>
			string.Equals(x.Name, "Basic", StringComparison.OrdinalIgnoreCase) ||
			x.Name.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase));
		if (basic != null)
		{
			return basic.Amount;
		}

		return earningAmountsByComponentId.Count == 1 ? earningAmountsByComponentId.Values.Single() : 0m;
	}

	private static decimal CalculatePercentageAmount(decimal baseAmount, decimal percentage) =>
		Math.Round(baseAmount * percentage / 100m, 2, MidpointRounding.AwayFromZero);

	private static EmployeePayrollComponent CopyWithAmount(EmployeePayrollComponent component, decimal amount) =>
		new(
			component.Id,
			component.EmployeeId,
			component.Name,
			component.ComponentId,
			component.Description,
			amount,
			component.IsFixed,
			component.IsTemp,
			component.ApplicableMonth,
			component.ComponentType,
			component.DependOn,
			component.IsEarning,
			component.UsesMasterValue);
}
