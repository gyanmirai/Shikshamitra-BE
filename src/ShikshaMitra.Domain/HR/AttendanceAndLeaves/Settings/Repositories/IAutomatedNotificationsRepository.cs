using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public interface IAutomatedNotificationsRepository : IRepository<AutomatedNotificationsSettings, Guid>
{
}