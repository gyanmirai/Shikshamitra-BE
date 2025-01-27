using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.EntranceExam;

public interface IEntranceExamAppService : ICrudAppService<EntranceExamDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEntranceExamDto>
{
	
}