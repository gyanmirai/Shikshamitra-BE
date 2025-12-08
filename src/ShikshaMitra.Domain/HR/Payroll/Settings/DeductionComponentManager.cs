using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Domain service for managing deduction components with business logic
/// </summary>
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
		ComponentDependency dependOn,
		string amount,
		bool visibleInPayslip,
		int displayOrder = 0)
	{
		// Check if name already exists
		await CheckNameAvailabilityAsync(name);

		var deductionComponent = new DeductionComponent(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			name,
			description,
			componentType,
			dependOn,
			amount,
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
		ComponentDependency dependOn,
		string amount,
		bool visibleInPayslip,
		int displayOrder)
	{
		// Check if name already exists (excluding current component)
		if (deductionComponent.Name != name)
		{
			await CheckNameAvailabilityAsync(name, deductionComponent.Id);
		}

		deductionComponent.Update(
			name,
			description,
			componentType,
			dependOn,
			amount,
			visibleInPayslip,
			displayOrder
		);
	}

	private async Task CheckNameAvailabilityAsync(string name, Guid? excludeId = null)
	{
		if (await _deductionComponentRepository.IsNameExistsAsync(name, excludeId))
		{
			throw new BusinessException("ShikshaMitra:DeductionComponentNameAlreadyExists")
				.WithData("name", name);
		}
	}
}
