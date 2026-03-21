using ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;
using System;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class AutomatedNotificationsAppService(IAutomatedNotificationsRepository repository) :
	CrudAppService<AutomatedNotificationsSettings, AutomatedNotificationsSettingsDto, Guid,
		PagedAndSortedResultRequestDto, AutomatedNotificationsSettingsDto>(repository),
	IAutomatedNotificationsAppService
{
}