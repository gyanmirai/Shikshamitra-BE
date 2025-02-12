using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class EntranceExamSlotAppService(IEntranceExamSlotRepository entranceExamSlotRepository, EntranceExamSlotManager entranceExamSlotManager) :
	CrudAppService<EntranceExamSlot, EntranceExamSlotDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEntranceExamSlotDto>(entranceExamSlotRepository),
	IEntranceExamSlotAppService
{

	public override async Task<EntranceExamSlotDto> CreateAsync(CreateUpdateEntranceExamSlotDto input)
	{
		// Validate overlapping slot before insertion.
		await entranceExamSlotManager.ValidateNoOverlappingSlotAsync(input.StartDate, input.EndDate);

		// Map the input DTO to the entity.
		var entity = ObjectMapper.Map<CreateUpdateEntranceExamSlotDto, EntranceExamSlot>(input);

		// Insert the entity.
		await entranceExamSlotRepository.InsertAsync(entity);

		// Return the created entity as a DTO.
		return ObjectMapper.Map<EntranceExamSlot, EntranceExamSlotDto>(entity);
	}

	public override async Task<EntranceExamSlotDto> UpdateAsync(Guid id, CreateUpdateEntranceExamSlotDto input)
	{
		// Retrieve the existing slot.
		var entity = await entranceExamSlotRepository.GetAsync(id);

		// Validate overlapping slot, excluding the current slot.
		await entranceExamSlotManager.ValidateNoOverlappingSlotAsync(input.StartDate, input.EndDate, id);

		// Map updated values to the entity.
		ObjectMapper.Map(input, entity);

		// Update the entity.
		await entranceExamSlotRepository.UpdateAsync(entity);

		// Return the updated entity as a DTO.
		return ObjectMapper.Map<EntranceExamSlot, EntranceExamSlotDto>(entity);
	}
}