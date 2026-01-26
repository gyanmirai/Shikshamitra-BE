using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Calendar.EventTypes;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Calendar;

/// <summary>
/// EventType Management Controller
/// Manages event type configurations for calendar events and holidays
/// </summary>
[Area("app")]
[Route("api/calendar/event-types")]
[Authorize]
public class EventTypeController : ShikshaMitraController
{
    private readonly IEventTypeAppService _eventTypeAppService;

    public EventTypeController(IEventTypeAppService eventTypeAppService)
    {
        _eventTypeAppService = eventTypeAppService;
    }

    /// <summary>
    /// Get paginated list of event types with filtering and sorting
    /// </summary>
    /// <param name="input">Pagination, filtering, and sorting parameters</param>
    /// <returns>Paginated list of event types</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<EventTypeDto>), StatusCodes.Status200OK)]
    public virtual async Task<PagedResultDto<EventTypeDto>> GetListAsync(
        [FromQuery] GetEventTypeListRequestDto input)
    {
        return await _eventTypeAppService.GetListAsync(input);
    }

    /// <summary>
    /// Get event type by ID
    /// </summary>
    /// <param name="id">Event type ID</param>
    /// <returns>Event type details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EventTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<EventTypeDto> GetAsync(Guid id)
    {
        return await _eventTypeAppService.GetAsync(id);
    }

    /// <summary>
    /// Create a new event type
    /// </summary>
    /// <param name="input">Event type creation data</param>
    /// <returns>Created event type</returns>
    [HttpPost]
    [ProducesResponseType(typeof(EventTypeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<EventTypeDto> CreateAsync(
        [FromBody] CreateUpdateEventTypeDto input)
    {
        return await _eventTypeAppService.CreateAsync(input);
    }

    /// <summary>
    /// Update an existing event type
    /// </summary>
    /// <param name="id">Event type ID</param>
    /// <param name="input">Event type update data</param>
    /// <returns>Updated event type</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(EventTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<EventTypeDto> UpdateAsync(Guid id,
        [FromBody] CreateUpdateEventTypeDto input)
    {
        return await _eventTypeAppService.UpdateAsync(id, input);
    }

    /// <summary>
    /// Delete an event type
    /// </summary>
    /// <param name="id">Event type ID</param>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _eventTypeAppService.DeleteAsync(id);
    }
}
