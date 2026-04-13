using System;
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

		var earningComponent = new EarningComponent(
			GuidGenerator.Create(),
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

	private async Task CheckNameAvailabilityAsync(string name, Guid? excludeId = null)
	{
		if (await _earningComponentRepository.IsNameExistsAsync(name, excludeId))
		{
			throw new BusinessException("ShikshaMitra:EarningComponentNameAlreadyExists")
				.WithData("name", name);
		}
	}
}
