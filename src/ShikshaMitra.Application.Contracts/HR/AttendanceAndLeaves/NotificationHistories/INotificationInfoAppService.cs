using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;

public interface INotificationInfoAppService : IReadOnlyAppService<
    NotificationInfoDto,
    Guid,
    PagedAndSortedResultRequestDto>
{
}
