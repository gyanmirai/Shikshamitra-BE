using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Calendar.EventTypes;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Calendar;

/// <summary>
/// Event Type Management Controller
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
    /// <returns>Paginated list of holiday types</returns>
    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.View)]
    [ProducesResponseType(typeof(PagedResultDto<EventTypeDto>), StatusCodes.Status200OK)]
    public virtual async Task<PagedResultDto<EventTypeDto>> GetListAsync(
        [FromQuery] GetEventTypeListRequestDto input)
    {
        return await _eventTypeAppService.GetListAsync(input);
    }

    /// <summary>
    /// Get event type by ID
    /// </summary>
    /// <param name="id">Holiday type ID</param>
    /// <returns>Holiday type details</returns>
    [HttpGet("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.View)]
    [ProducesResponseType(typeof(EventTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<EventTypeDto> GetAsync(Guid id)
    {
        return await _eventTypeAppService.GetAsync(id);
    }

    /// <summary>
    /// Create a new event type
    /// </summary>
    /// <param name="input">Holiday type creation data</param>
    /// <returns>Created holiday type</returns>
    [HttpPost]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.Add)]
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
    /// <param name="id">Holiday type ID</param>
    /// <param name="input">Holiday type update data</param>
    /// <returns>Updated holiday type</returns>
    [HttpPut("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.Add)]
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
    /// <param name="id">Holiday type ID</param>
    [HttpDelete("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _eventTypeAppService.DeleteAsync(id);
    }
}
