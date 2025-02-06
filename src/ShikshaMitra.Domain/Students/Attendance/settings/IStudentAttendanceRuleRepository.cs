using ShikshaMitra.Students.Attendance.Settings;
using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Attendance.settings;

public interface IStudentAttendanceRuleRepository : IRepository<StudentAttendanceRule, Guid>
{
	
}