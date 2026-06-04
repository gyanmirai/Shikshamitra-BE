using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DeductionComponentManager : DomainService
{
	private readonly IDeductionComponentRepository _deductionComponentRepository;

	public DeductionComponentManager(IDeductionComponentRepository deductionComponentRepository)
	{
		_deductionComponentRepository = deductionComponentRepository;
	}

	public async Task<DeductionComponent> CreateAsync(
		string name,
		string? description,
		DeductionComponentType componentType,
		CalculationType calculationType,
		decimal? amount,
		decimal? percentage,
		Guid? selectedDeductionComponentId,
		ComponentDependency dependOn,
		bool visibleInPayslip,
		int displayOrder = 0)
	{
		name = (name ?? string.Empty).Trim();
		await CheckNameAvailabilityAsync(name);

		var deductionComponent = new DeductionComponent(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			name,
			description,
			componentType,
			calculationType,
			amount,
			percentage,
			selectedDeductionComponentId,
			dependOn,
			visibleInPayslip,
			displayOrder
		);

		return deductionComponent;
	}

	public async Task UpdateAsync(
		DeductionComponent deductionComponent,
		string name,
		string? description,
		DeductionComponentType componentType,
		CalculationType calculationType,
		decimal? amount,
		decimal? percentage,
		Guid? selectedDeductionComponentId,
		ComponentDependency dependOn,
		bool visibleInPayslip,
		int displayOrder)
	{
		name = (name ?? string.Empty).Trim();
		if (deductionComponent.Name != name)
		{
			await CheckNameAvailabilityAsync(name, deductionComponent.Id);
		}

		deductionComponent.Update(
			name,
			description,
			componentType,
			calculationType,
			amount,
			percentage,
			selectedDeductionComponentId,
			dependOn,
			visibleInPayslip,
			displayOrder
		);
	}

	private async Task CheckNameAvailabilityAsync(string name, Guid? excludeId = null)
	{
		if (await _deductionComponentRepository.IsNameExistsAsync(name, excludeId))
		{
			throw new BusinessException(HRErrorCodes.DeductionComponentNameAlreadyExists)
				.WithData("name", name);
		}
	}
}
