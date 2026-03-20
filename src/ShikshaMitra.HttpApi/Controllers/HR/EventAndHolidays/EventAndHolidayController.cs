using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.EventAndHolidays;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.EventAndHolidays;

/// <summary>
/// API Controller for managing events and holidays.
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/events-and-holidays")]
[Authorize]
public class EventAndHolidayController : AbpControllerBase
{
    private readonly IEventAndHolidayAppService _eventAndHolidayAppService;

    public EventAndHolidayController(IEventAndHolidayAppService eventAndHolidayAppService)
    {
        _eventAndHolidayAppService = eventAndHolidayAppService;
    }

    /// <summary>
    /// Get paginated list of events and holidays.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<EventAndHolidayDto>), StatusCodes.Status200OK)]
    public virtual async Task<PagedResultDto<EventAndHolidayDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return await _eventAndHolidayAppService.GetListAsync(input);
    }

    /// <summary>
    /// Get an event or holiday by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EventAndHolidayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<EventAndHolidayDto> GetAsync(Guid id)
    {
        return await _eventAndHolidayAppService.GetAsync(id);
    }

    /// <summary>
    /// Get events and holidays for a specific month.
    /// </summary>
    [HttpGet("by-month")]
    [ProducesResponseType(typeof(PagedResultDto<EventAndHolidayDto>), StatusCodes.Status200OK)]
    public virtual async Task<PagedResultDto<EventAndHolidayDto>> GetByMonthAsync([FromQuery] DateTimeOffset date)
    {
        return await _eventAndHolidayAppService.GetEventAndHolidayByMonth(date);
    }

    /// <summary>
    /// Create a new event or holiday.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EventAndHolidayDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<ActionResult<EventAndHolidayDto>> CreateAsync([FromBody] CreateUpdateEventAndHolidayDto input)
    {
        var result = await _eventAndHolidayAppService.CreateAsync(input);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Update an existing event or holiday.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(EventAndHolidayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<EventAndHolidayDto> UpdateAsync(Guid id, [FromBody] CreateUpdateEventAndHolidayDto input)
    {
        return await _eventAndHolidayAppService.UpdateAsync(id, input);
    }

    /// <summary>
    /// Delete an event or holiday.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> DeleteAsync(Guid id)
    {
        await _eventAndHolidayAppService.DeleteAsync(id);
        return NoContent();
    }
}
