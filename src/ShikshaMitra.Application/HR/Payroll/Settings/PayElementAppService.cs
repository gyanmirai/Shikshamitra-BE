using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Settings;

public class PayElementAppService(IPayElementRepository payElementRepository, PayElementManager payElementManager)
	: CrudAppService<PayElement, PayElementDto, Guid, PagedAndSortedResultRequestDto, CreateUpdatePayElementDto>(
			payElementRepository),
		IPayElementAppService
{
	public override async Task<PayElementDto> CreateAsync(CreateUpdatePayElementDto input)
	{
		// Use the manager to handle business rules
		var payElement = await payElementManager.CreateAsync(
			input.Code,
			input.Name,
			input.Type,
			input.IsEmployeeSpecific,
			input.IsDependent,
			input.Dependent == null
				? null
				: new PayElement.DependentProperties
				{
					ElementId = input.Dependent.ElementId,
					Percentage = input.Dependent.Percentage,
					Amount = input.Dependent.Amount
				},
			input.IsVisibleInPaySlip);

		// Save the entity
		await payElementRepository.InsertAsync(payElement);

		return ObjectMapper.Map<PayElement, PayElementDto>(payElement);
	}

	public override async Task<PayElementDto> UpdateAsync(Guid id, CreateUpdatePayElementDto input)
	{
		var payElement = await payElementRepository.GetAsync(id);

		await payElementManager.UpdateAsync(
			payElement,
			input.Code,
			input.Name,
			input.IsEmployeeSpecific,
			input.IsDependent,
			input.Dependent == null
				? null
				: new PayElement.DependentProperties
				{
					ElementId = input.Dependent.ElementId,
					Percentage = input.Dependent.Percentage,
					Amount = input.Dependent.Amount
				},
			input.IsVisibleInPaySlip);

		// Save changes
		await payElementRepository.UpdateAsync(payElement);

		return ObjectMapper.Map<PayElement, PayElementDto>(payElement);
	}
}