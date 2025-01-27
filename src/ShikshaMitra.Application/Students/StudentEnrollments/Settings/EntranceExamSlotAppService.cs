using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class EntranceExamSlotAppService(IEntranceExamSlotRepository entranceExamSlotRepository) : CrudAppService<EntranceExamSlot, EntranceExamSlotDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEntranceExamSlotDto>(entranceExamSlotRepository),IEntranceExamSlotAppService
{
	
}