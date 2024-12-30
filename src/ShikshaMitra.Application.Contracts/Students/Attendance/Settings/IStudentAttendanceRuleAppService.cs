using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Attendance.Settings;

public interface IStudentAttendanceRuleAppService : ICrudAppService<StudentAttendanceRuleDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentAttendanceRuleDto>
{
	
}