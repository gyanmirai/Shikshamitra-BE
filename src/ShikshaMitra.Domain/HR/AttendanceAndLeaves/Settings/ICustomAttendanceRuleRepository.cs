using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;

public interface ICustomAttendanceRuleRepository : IRepository<CustomAttendanceRule, Guid>
{
}