using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;

public interface IAutomatedNotificationsRepository : IRepository<AutomatedNotificationsSettings, Guid>
{
}