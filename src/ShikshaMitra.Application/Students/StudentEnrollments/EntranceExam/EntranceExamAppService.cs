using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.EntranceExam;

public class EntranceExamAppService(IEntranceExamRepository entranceExamRepository) : CrudAppService<EntranceExam,EntranceExamDto, Guid, PagedAndSortedResultRequestDto,CreateUpdateEntranceExamDto>(entranceExamRepository), IEntranceExamAppService
{
}