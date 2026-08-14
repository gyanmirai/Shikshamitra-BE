using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

internal static class SalaryStructureComponentRules
{
	public static PayrollComponentSemanticType ParseSemanticType(string value)
	{
		if (PayrollComponentSemanticTypeCodes.TryParse(value, out var semanticType))
		{
			return semanticType;
		}

		throw new BusinessException("HR:Payroll:SalaryStructureInvalidSemanticType")
			.WithData("semanticType", value);
	}

	public static EmployeePayrollComponent? FindExistingComponent(
		SalaryComponentDto component,
		IReadOnlyDictionary<Guid, EmployeePayrollComponent> existingById)
	{
		if (component.Id.HasValue && existingById.TryGetValue(component.Id.Value, out var byId))
		{
			return byId;
		}

		if (component.ComponentId.HasValue && existingById.TryGetValue(component.ComponentId.Value, out var byComponentId))
		{
			return byComponentId;
		}

		return null;
	}

	public static bool ShouldResolveMasterComponent(SalaryComponentDto component)
	{
		return component.ComponentId.HasValue
			&& component.ComponentId.Value != Guid.Empty
			&& !component.IsTemp
			&& component.ComponentId != component.Id;
	}

	public static void EnsureCustomComponentIdsAreValid(
		IEnumerable<SalaryComponentDto> components,
		IReadOnlyDictionary<Guid, EmployeePayrollComponent> existingById,
		string errorCode)
	{
		foreach (var component in components)
		{
			if (!component.ComponentId.HasValue || component.ComponentId.Value == Guid.Empty ||
			    ShouldResolveMasterComponent(component))
			{
				continue;
			}

			var matchesExistingCustomRow = existingById.TryGetValue(component.ComponentId.Value, out var existing) &&
				(existing.IsTemp || existing.ComponentId is null || existing.ComponentId == Guid.Empty);
			var matchesSubmittedExistingCustomRow = component.Id.HasValue &&
				component.ComponentId.Value == component.Id.Value &&
				existingById.TryGetValue(component.Id.Value, out existing) &&
				(existing.IsTemp || existing.ComponentId is null || existing.ComponentId == Guid.Empty);

			if (!matchesExistingCustomRow && !matchesSubmittedExistingCustomRow)
			{
				throw new BusinessException(errorCode)
					.WithData("componentId", component.ComponentId.Value);
			}
		}
	}

	public static void EnsureAllMasterComponentsExist(
		IEnumerable<Guid> requestedIds,
		IEnumerable<Guid> foundIds,
		string errorCode)
	{
		var requested = requestedIds.Distinct().ToList();
		if (requested.Count == 0)
		{
			return;
		}

		var found = foundIds.ToHashSet();
		var missing = requested.Where(id => !found.Contains(id)).ToList();
		if (missing.Count == 0)
		{
			return;
		}

		throw new BusinessException(errorCode)
			.WithData("missingComponentIds", string.Join(",", missing));
	}

	public static SalaryComponentType MapCalculationType(CalculationType calculationType)
	{
		return calculationType == CalculationType.Amount
			? SalaryComponentType.Fixed
			: SalaryComponentType.Percentage;
	}

	public static decimal ResolveAmountComponentFallback(SalaryComponentDto line, decimal? masterAmount) =>
		PayrollComponentAmountResolver.ResolveAmountComponentFallback(line.Amount, masterAmount);

	public static PayrollComponentAmountLine<EarningComponent> ToAmountLine(
		ResolvedSalaryComponent<EarningComponent> resolved)
	{
		var line = resolved.Line;
		return new PayrollComponentAmountLine<EarningComponent>(
			line.Id,
			line.ComponentId,
			resolved.Master?.Name ?? line.Name,
			line.Amount,
			line.IsTemp,
			line.UsesMasterValue,
			resolved.Master);
	}

	public static PayrollComponentAmountLine<DeductionComponent> ToAmountLine(
		ResolvedSalaryComponent<DeductionComponent> resolved)
	{
		var line = resolved.Line;
		return new PayrollComponentAmountLine<DeductionComponent>(
			line.Id,
			line.ComponentId,
			resolved.Master?.Name ?? line.Name,
			line.Amount,
			line.IsTemp,
			line.UsesMasterValue,
			resolved.Master);
	}

	public static bool ShouldUseMasterValue(SalaryComponentDto line, EarningComponent? master)
	{
		if (master is null || line.IsTemp)
		{
			return false;
		}
		if (line.UsesMasterValue == false)
		{
			return false;
		}

		return master.CalculationType == CalculationType.Percentage ||
		       (line.Amount ?? 0m) == (master.Amount ?? 0m);
	}

	public static bool ShouldUseMasterValue(SalaryComponentDto line, DeductionComponent? master)
	{
		if (master is null || line.IsTemp)
		{
			return false;
		}
		if (line.UsesMasterValue == false)
		{
			return false;
		}

		return master.CalculationType == CalculationType.Percentage ||
		       (line.Amount ?? 0m) == (master.Amount ?? 0m);
	}
}
