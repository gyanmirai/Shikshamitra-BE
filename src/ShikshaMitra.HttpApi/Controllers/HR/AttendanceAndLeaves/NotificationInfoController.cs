using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

/// <summary>
/// API Controller for viewing attendance notification history (read-only).
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/attendance/notifications")]
[Authorize]
public class NotificationInfoController : AbpControllerBase
{
    private readonly INotificationInfoAppService _notificationInfoAppService;

    public NotificationInfoController(INotificationInfoAppService notificationInfoAppService)
    {
        _notificationInfoAppService = notificationInfoAppService;
    }

    /// <summary>
    /// Get paginated list of notification records.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<NotificationInfoDto>), StatusCodes.Status200OK)]
    public virtual async Task<PagedResultDto<NotificationInfoDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return await _notificationInfoAppService.GetListAsync(input);
    }

    /// <summary>
    /// Get a notification record by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(NotificationInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<NotificationInfoDto> GetAsync(Guid id)
    {
        return await _notificationInfoAppService.GetAsync(id);
    }
}
