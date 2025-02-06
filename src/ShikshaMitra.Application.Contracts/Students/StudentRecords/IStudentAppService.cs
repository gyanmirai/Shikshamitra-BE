using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentRecords;

public interface IStudentAppService : ICrudAppService<StudentDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentDto>
{
}
