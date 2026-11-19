using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

public class EarningComponentManager : DomainService
{
	private readonly IEarningComponentRepository _earningComponentRepository;

	public EarningComponentManager(IEarningComponentRepository earningComponentRepository)
	{
		_earningComponentRepository = earningComponentRepository;
	}

	public async Task<EarningComponent> CreateAsync(
		string name,
		string? description,
		EarningComponentType componentType,
		CalculationType calculationType,
		decimal? amount,
		decimal? percentage,
		Guid? selectedEarningComponentId,
		ComponentDependency dependOn,
		bool visibleInPayslip,
		int displayOrder = 0)
	{
		name = (name ?? string.Empty).Trim();
		await CheckNameAvailabilityAsync(name);
		var id = GuidGenerator.Create();
		await ValidateDependencyAsync(id, calculationType, selectedEarningComponentId, dependOn);

		var earningComponent = new EarningComponent(
			id,
			CurrentTenant.Id,
			name,
			description,
			componentType,
			calculationType,
			amount,
			percentage,
			selectedEarningComponentId,
			dependOn,
			visibleInPayslip,
			displayOrder
		);

		return earningComponent;
	}

	public async Task UpdateAsync(
		EarningComponent earningComponent,
		string name,
		string? description,
		EarningComponentType componentType,
		CalculationType calculationType,
		decimal? amount,
		decimal? percentage,
		Guid? selectedEarningComponentId,
		ComponentDependency dependOn,
		bool visibleInPayslip,
		int displayOrder)
	{
		name = (name ?? string.Empty).Trim();
		if (earningComponent.Name != name)
		{
			await CheckNameAvailabilityAsync(name, earningComponent.Id);
		}

		await ValidateDependencyAsync(
			earningComponent.Id,
			calculationType,
			selectedEarningComponentId,
			dependOn);

		earningComponent.Update(
			name,
			description,
			componentType,
			calculationType,
			amount,
			percentage,
			selectedEarningComponentId,
			dependOn,
			visibleInPayslip,
			displayOrder
		);
	}

	public async Task ValidateDependencyAsync(
		Guid componentId,
		CalculationType calculationType,
		Guid? selectedEarningComponentId,
		ComponentDependency dependOn)
	{
		if (calculationType != CalculationType.Percentage ||
		    dependOn != ComponentDependency.CustomComponent ||
		    !selectedEarningComponentId.HasValue)
		{
			return;
		}

		if (selectedEarningComponentId.Value == componentId)
		{
			throw new BusinessException(HRErrorCodes.PayrollComponentSelfReference)
				.WithData("message", "An earning component cannot use itself as its percentage base.");
		}

		var components = (await _earningComponentRepository.GetListAsync()).ToDictionary(component => component.Id);
		ValidateDependency(componentId, calculationType, selectedEarningComponentId, dependOn, components);
	}

	public static void ValidateDependency(Guid componentId, CalculationType calculationType,
		Guid? selectedEarningComponentId, ComponentDependency dependOn,
		IReadOnlyDictionary<Guid, EarningComponent> components)
	{
		if (calculationType != CalculationType.Percentage || dependOn != ComponentDependency.CustomComponent || !selectedEarningComponentId.HasValue) return;
		if (!components.TryGetValue(selectedEarningComponentId.Value, out var selected) ||
		    !selected.IsActive)
		{
			throw InvalidDependencyTarget();
		}

		var visited = new HashSet<Guid> { componentId };
		var current = selected;
		while (true)
		{
			if (!visited.Add(current.Id))
			{
				throw new BusinessException(HRErrorCodes.PayrollComponentDependencyCycle)
					.WithData(
						"message",
						"This base component would create a circular earning-component dependency.");
			}

			if (current.CalculationType != CalculationType.Percentage ||
			    current.DependOn != ComponentDependency.CustomComponent ||
			    !current.SelectedEarningComponentId.HasValue)
			{
				return;
			}

			if (!components.TryGetValue(current.SelectedEarningComponentId.Value, out var next) ||
			    !next.IsActive)
			{
				throw InvalidDependencyTarget();
			}

			current = next;
		}
	}

	public async Task EnsureCanRetireAsync(Guid componentId)
	{
		var activeDependent = (await _earningComponentRepository.GetListAsync())
			.Where(component =>
				component.IsActive &&
				component.CalculationType == CalculationType.Percentage &&
				component.DependOn == ComponentDependency.CustomComponent &&
				component.SelectedEarningComponentId == componentId)
			.OrderBy(component => component.Name)
			.ThenBy(component => component.Id)
			.FirstOrDefault();
		if (activeDependent is null)
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.PayrollComponentDependencyInUse)
			.WithData(
				"message",
				"This earning component is used as the percentage base by an active earning component and cannot be deactivated or deleted.")
			.WithData("dependentComponentId", activeDependent.Id)
			.WithData("dependentComponentName", activeDependent.Name);
	}

	private async Task CheckNameAvailabilityAsync(string name, Guid? excludeId = null)
	{
		if (await _earningComponentRepository.IsNameExistsAsync(name, excludeId))
		{
			throw new BusinessException(HRErrorCodes.EarningComponentNameAlreadyExists)
				.WithData("name", name);
		}
	}

	private static BusinessException InvalidDependencyTarget() =>
		new BusinessException(HRErrorCodes.PayrollComponentDependencyTargetInvalid)
			.WithData(
				"message",
				"Select an active earning component from the current school as the percentage base.");
}
