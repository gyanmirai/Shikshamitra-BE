using System;
using ShikshaMitra.HR.AttendanceAndLeaves;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;

[RemoteService(false)]
public class NotificationInfoAppService(IRepository<NotificationInfo, Guid> repository)
	: ReadOnlyAppService<NotificationInfo, NotificationInfoDto, Guid, PagedAndSortedResultRequestDto>(repository),
		INotificationInfoAppService
{
}