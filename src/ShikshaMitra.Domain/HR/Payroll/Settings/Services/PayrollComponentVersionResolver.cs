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

		return PayrollComponentPercentageAmountResolver.ResolvePercentageAmounts(versioned, versionByResolvedComponentId);
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

	private static string NormalizeMonth(string month)
	{
		return DateTime.TryParse($"{month}-01", out var parsed)
			? parsed.ToString("yyyy-MM")
			: month;
	}
}
