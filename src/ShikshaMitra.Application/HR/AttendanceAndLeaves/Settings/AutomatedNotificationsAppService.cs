using ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AutomatedNotificationsAppService(IRepository<AutomatedNotificationsSettings, Guid> repository) :
	CrudAppService<AutomatedNotificationsSettings, AutomatedNotificationsSettingsDto, Guid,
		PagedAndSortedResultRequestDto, AutomatedNotificationsSettingsDto>(repository)
{
}