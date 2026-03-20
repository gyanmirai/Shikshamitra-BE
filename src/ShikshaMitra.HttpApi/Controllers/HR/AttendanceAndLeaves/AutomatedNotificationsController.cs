using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

/// <summary>
/// API Controller for managing automated notification settings for attendance.
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/attendance/automated-notifications")]
[Authorize]
public class AutomatedNotificationsController : AbpControllerBase
{
    private readonly IAutomatedNotificationsAppService _automatedNotificationsAppService;

    public AutomatedNotificationsController(IAutomatedNotificationsAppService automatedNotificationsAppService)
    {
        _automatedNotificationsAppService = automatedNotificationsAppService;
    }

    /// <summary>
    /// Get paginated list of automated notification settings.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<AutomatedNotificationsSettingsDto>), StatusCodes.Status200OK)]
    public virtual async Task<PagedResultDto<AutomatedNotificationsSettingsDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return await _automatedNotificationsAppService.GetListAsync(input);
    }

    /// <summary>
    /// Get an automated notification setting by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AutomatedNotificationsSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<AutomatedNotificationsSettingsDto> GetAsync(Guid id)
    {
        return await _automatedNotificationsAppService.GetAsync(id);
    }

    /// <summary>
    /// Create a new automated notification setting.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AutomatedNotificationsSettingsDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<ActionResult<AutomatedNotificationsSettingsDto>> CreateAsync([FromBody] AutomatedNotificationsSettingsDto input)
    {
        var result = await _automatedNotificationsAppService.CreateAsync(input);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Update an existing automated notification setting.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(AutomatedNotificationsSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<AutomatedNotificationsSettingsDto> UpdateAsync(Guid id, [FromBody] AutomatedNotificationsSettingsDto input)
    {
        return await _automatedNotificationsAppService.UpdateAsync(id, input);
    }

    /// <summary>
    /// Delete an automated notification setting.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> DeleteAsync(Guid id)
    {
        await _automatedNotificationsAppService.DeleteAsync(id);
        return NoContent();
    }
}
