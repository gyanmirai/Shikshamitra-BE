using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;
using System;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class EfCoreCustomAttendanceRuleRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, CustomAttendanceRule, Guid>(dbContextProvider),
		ICustomAttendanceRuleRepository
{
}