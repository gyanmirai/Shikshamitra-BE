using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public interface IAutomatedNotificationsAppService : ICrudAppService<
    AutomatedNotificationsSettingsDto,
    Guid,
    PagedAndSortedResultRequestDto,
    AutomatedNotificationsSettingsDto>
{
}
