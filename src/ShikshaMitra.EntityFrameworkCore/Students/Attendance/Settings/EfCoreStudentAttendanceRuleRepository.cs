using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.Attendance.settings;
using System;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Students.Attendance.Settings;

public class EfCoreStudentAttendanceRuleRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) : EfCoreRepository<ShikshaMitraDbContext, StudentAttendanceRule, Guid>(dbContextProvider), IStudentAttendanceRuleRepository
{
	
}