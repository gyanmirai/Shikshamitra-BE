using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public interface IStudentDocumentSettingAppService : ICrudAppService<StudentDocumentSettingDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentDocumentSettingDto>
{
	
}