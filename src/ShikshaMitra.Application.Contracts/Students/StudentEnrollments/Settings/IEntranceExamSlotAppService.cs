using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public interface IEntranceExamSlotAppService : ICrudAppService<EntranceExamSlotDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEntranceExamSlotDto>
{
	
}