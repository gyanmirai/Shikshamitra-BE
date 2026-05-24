using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Payroll;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

public class PayrollComponentVersionResolver : DomainService
{
	private readonly IRepository<PayrollComponentVersion, Guid> _componentVersionRepository;

	public PayrollComponentVersionResolver(IRepository<PayrollComponentVersion, Guid> componentVersionRepository)
	{
		_componentVersionRepository = componentVersionRepository;
	}

	public async Task<List<EmployeePayrollComponent>> ResolveAsync(
		IReadOnlyList<EmployeePayrollComponent> components,
		string monthYear)
	{
		var latestByComponent = await LoadLatestVersionsAsync(components, monthYear);
		return ResolveWithVersions(components, latestByComponent);
	}

	public async Task<ILookup<Guid, EmployeePayrollComponent>> ResolveByEmployeeAsync(
		ILookup<Guid, EmployeePayrollComponent> componentsByEmployee,
		string monthYear)
	{
		var allComponents = componentsByEmployee.SelectMany(x => x).ToList();
		var latestByComponent = await LoadLatestVersionsAsync(allComponents, monthYear);
		return componentsByEmployee
			.SelectMany(group => ResolveWithVersions(group.ToList(), latestByComponent))
			.ToLookup(x => x.EmployeeId);
	}

	private async Task<Dictionary<(Guid ComponentId, bool IsEarning), PayrollComponentVersion>> LoadLatestVersionsAsync(
		IReadOnlyList<EmployeePayrollComponent> components,
		string monthYear)
	{
		var syncedComponentIds = components
			.Where(x => x.UsesMasterValue && x.ComponentId.HasValue)
			.Select(x => x.ComponentId!.Value)
			.Distinct()
			.ToList();

		if (syncedComponentIds.Count == 0)
		{
			return new Dictionary<(Guid ComponentId, bool IsEarning), PayrollComponentVersion>();
		}

		var normalizedMonth = NormalizeMonth(monthYear);
		var query = await _componentVersionRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(x => x.TenantId == CurrentTenant.Id);
		}
		else
		{
			query = query.Where(x => x.TenantId == null);
		}

		var versions = await AsyncExecuter.ToListAsync(query
			.Where(x => syncedComponentIds.Contains(x.ComponentId) && string.Compare(x.EffectiveMonth, normalizedMonth) <= 0));

		return versions
			.GroupBy(x => new { x.ComponentId, x.IsEarning })
			.ToDictionary(
				x => (x.Key.ComponentId, x.Key.IsEarning),
				x => x.OrderByDescending(v => v.EffectiveMonth).ThenByDescending(v => v.CreationTime).First());
	}

	private static List<EmployeePayrollComponent> ResolveWithVersions(
		IReadOnlyList<EmployeePayrollComponent> components,
		IReadOnlyDictionary<(Guid ComponentId, bool IsEarning), PayrollComponentVersion> latestByComponent)
	{
		var versionByResolvedComponentId = new Dictionary<Guid, PayrollComponentVersion>();
		var versioned = components.Select(component =>
		{
			if (!component.UsesMasterValue ||
			    !component.ComponentId.HasValue ||
			    !latestByComponent.TryGetValue((component.ComponentId.Value, component.IsEarning), out var version))
			{
				return component;
			}

			var resolved = new EmployeePayrollComponent(
				component.Id,
				component.EmployeeId,
				version.Name,
				component.ComponentId,
				version.Description,
				ResolveComponentAmount(version, component.Amount),
				version.CalculationType == CalculationType.Amount,
				component.IsTemp,
				component.ApplicableMonth,
				version.CalculationType == CalculationType.Amount
					? SalaryComponentType.Fixed
					: SalaryComponentType.Percentage,
				MapDependency(version.DependOn),
				component.IsEarning,
				usesMasterValue: true);
			versionByResolvedComponentId[resolved.Id] = version;
			return resolved;
		}).ToList();

		return ResolvePercentageAmounts(versioned, versionByResolvedComponentId);
	}

	private static decimal ResolveComponentAmount(PayrollComponentVersion version, decimal fallbackAmount)
	{
		return version.CalculationType == CalculationType.Amount
			? version.Amount ?? fallbackAmount
			: version.Percentage ?? fallbackAmount;
	}

	private static SalaryComponentDependency? MapDependency(ComponentDependency dependOn)
	{
		return dependOn switch
		{
			ComponentDependency.None => SalaryComponentDependency.None,
			ComponentDependency.Basic => SalaryComponentDependency.Basic,
			ComponentDependency.Gross => SalaryComponentDependency.Gross,
			_ => null
		};
	}

	private static List<EmployeePayrollComponent> ResolvePercentageAmounts(
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

	private static string NormalizeMonth(string month)
	{
		return DateTime.TryParse($"{month}-01", out var parsed)
			? parsed.ToString("yyyy-MM")
			: month;
	}
}
