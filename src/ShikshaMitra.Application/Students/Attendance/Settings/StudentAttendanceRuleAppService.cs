using ShikshaMitra.Students.Attendance.settings;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Attendance.Settings;

public class StudentAttendanceRuleAppService(IStudentAttendanceRuleRepository studentAttendanceRuleRepository) : CrudAppService<StudentAttendanceRule, StudentAttendanceRuleDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentAttendanceRuleDto>(studentAttendanceRuleRepository),IStudentAttendanceRuleAppService
{
	
}