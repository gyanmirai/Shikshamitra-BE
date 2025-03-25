using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentRecords.StudentGroups;

public interface IStudentGroupAppService : ICrudAppService<StudentGroupDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentGroupDto>
{ 

}
