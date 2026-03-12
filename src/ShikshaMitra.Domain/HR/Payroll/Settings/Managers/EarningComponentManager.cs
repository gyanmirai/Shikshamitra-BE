using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Domain service for managing earning components with business logic
/// </summary>
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
		ComponentDependency dependOn,
		Guid? customComponentId,
		string amount,
		bool visibleInPayslip,
		int displayOrder = 0)
	{
		// Check if name already exists
		await CheckNameAvailabilityAsync(name);

		var earningComponent = new EarningComponent(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			name,
			description,
			componentType,
			dependOn,
			customComponentId,
			amount,
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
		ComponentDependency dependOn,
		Guid? customComponentId,
		string amount,
		bool visibleInPayslip,
		int displayOrder)
	{
		// Check if name already exists (excluding current component)
		if (earningComponent.Name != name)
		{
			await CheckNameAvailabilityAsync(name, earningComponent.Id);
		}

		earningComponent.Update(
			name,
			description,
			componentType,
			dependOn,
			customComponentId,
			amount,
			visibleInPayslip,
			displayOrder
		);
	}

	private async Task CheckNameAvailabilityAsync(string name, Guid? excludeId = null)
	{
		if (await _earningComponentRepository.IsNameExistsAsync(name, excludeId))
		{
			throw new BusinessException("ShikshaMitra:EarningComponentNameAlreadyExists")
				.WithData("name", name);
		}
	}
}
