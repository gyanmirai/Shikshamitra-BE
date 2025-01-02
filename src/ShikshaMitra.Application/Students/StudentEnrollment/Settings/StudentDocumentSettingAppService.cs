using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollment.Settings;

public class StudentDocumentSettingAppService(IStudentDocumentSettingRepository studentDocumentSettingRepository) : CrudAppService<StudentDocumentSetting, StudentDocumentSettingDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentDocumentSettingDto>(studentDocumentSettingRepository), IStudentDocumentSettingAppService
{
	
}