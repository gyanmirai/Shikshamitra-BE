using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.HR.Calendar.Events;
using ShikshaMitra.Communication;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Controllers;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HttpApi.Controllers.HR.Calendar;

/// <summary>
/// REST API Controller for Event management
/// </summary>
[ApiController]
[Route("api/calendar/events")]
[Produces("application/json")]
[Authorize]
public class EventController : ShikshaMitraController
{
    private readonly IEventAppService _eventAppService;
    private readonly ICalendarCommunicationAppService _calendarCommunicationAppService;

    public EventController(
        IEventAppService eventAppService,
        ICalendarCommunicationAppService calendarCommunicationAppService)
    {
        _eventAppService = eventAppService;
        _calendarCommunicationAppService = calendarCommunicationAppService;
    }

    /// <summary>
    /// Get paginated list of events with optional filtering and sorting
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/calendar/events?search=Team%20Meeting&pageNumber=1&pageSize=10&sorting=title
    /// </remarks>
    /// <param name="input">Filter and pagination parameters</param>
    /// <returns>Paginated list of events</returns>
    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.View)]
    [ProducesResponseType(typeof(PagedResultDto<EventDto>), 200)]
    public async Task<ActionResult<PagedResultDto<EventDto>>> GetListAsync([FromQuery] GetEventListRequestDto input)
    {
        var result = await _eventAppService.GetListAsync(input);
        return Ok(result);
    }

    /// <summary>
    /// Get a specific event by ID
    /// </summary>
    /// <param name="id">Event ID (GUID)</param>
    /// <returns>Event details</returns>
    [HttpGet("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.View)]
    [ProducesResponseType(typeof(EventDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<EventDto>> GetAsync(Guid id)
    {
        var result = await _eventAppService.GetAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Create and optionally publish a Communication announcement for this event.
    /// </summary>
    /// <param name="id">Event ID</param>
    /// <param name="input">Communication campaign options</param>
    /// <returns>Created Communication campaign and publish state</returns>
    [HttpPost("{id}/communication/announcement")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.View)]
    [ProducesResponseType(typeof(CalendarCommunicationResultDto), 200)]
    public async Task<ActionResult<CalendarCommunicationResultDto>> AnnounceAsync(
        Guid id,
        [FromBody] CalendarCommunicationRequestDto input)
    {
        var result = await _calendarCommunicationAppService.AnnounceEventAsync(id, input);
        return Ok(result);
    }

    /// <summary>
    /// Create a new event
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/calendar/events?academicYearId=3fa85f64-5717-4562-b3fc-2c963f66afa6
    ///     {
    ///         "title": "Team Meeting",
    ///         "meetingDate": "2025-02-15",
    ///         "startTime": "10:00",
    ///         "endTime": "11:00",
    ///         "mode": 1,
    ///         "venue": "Conference Room A",
    ///         "description": "Quarterly planning meeting"
    ///     }
    /// </remarks>
    /// <param name="academicYearId">Academic Year ID from query parameter</param>
    /// <param name="input">Event creation data</param>
    /// <returns>Created event with ID</returns>
    [HttpPost]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.Add)]
    [ProducesResponseType(typeof(EventDto), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<EventDto>> CreateAsync([FromQuery] Guid academicYearId, [FromBody] CreateUpdateEventDto input)
    {
        input.AcademicYearId = academicYearId;
        var result = await _eventAppService.CreateAsync(input);
        return CreatedAtAction(nameof(GetAsync), new { id = result.Id }, result);
    }

    /// <summary>
    /// Update an existing event
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT /api/calendar/events/3fa85f64-5717-4562-b3fc-2c963f66afa6
    ///     {
    ///         "title": "Team Meeting - Updated",
    ///         "eventTypeId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///         "meetingDate": "2025-02-15",
    ///         "startTime": "14:00",
    ///         "endTime": "15:00"
    ///     }
    /// </remarks>
    /// <param name="id">Event ID to update</param>
    /// <param name="input">Updated event data</param>
    /// <returns>Updated event</returns>
    [HttpPut("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.Add)]
    [ProducesResponseType(typeof(EventDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<EventDto>> UpdateAsync(Guid id, [FromBody] CreateUpdateEventDto input)
    {
        var result = await _eventAppService.UpdateAsync(id, input);
        return Ok(result);
    }

    /// <summary>
    /// Delete an event
    /// </summary>
    /// <param name="id">Event ID to delete</param>
    /// <returns>No content</returns>
    [HttpDelete("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.Delete)]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<ActionResult> DeleteAsync(Guid id)
    {
        await _eventAppService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Get all events by event type
    /// </summary>
    /// <param name="typeId">Event Type ID (GUID)</param>
    /// <returns>List of events of specified type</returns>
    [HttpGet("by-type/{typeId}")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.View)]
    [ProducesResponseType(typeof(PagedResultDto<EventDto>), 200)]
    public async Task<ActionResult<PagedResultDto<EventDto>>> GetByTypeAsync(
        Guid typeId,
        [FromQuery] GetEventListRequestDto input)
    {
        input.TypeId = typeId;
        var result = await _eventAppService.GetListAsync(input);
        return Ok(result);
    }

    /// <summary>
    /// Get events filtered by status
    /// </summary>
    /// <remarks>
    /// Status values: 0=Upcoming, 1=Active (today), 2=Completed (past)
    /// </remarks>
    /// <param name="input">Filter and pagination parameters</param>
    /// <returns>Paginated list of events with specified status</returns>
    [HttpGet("by-status/{status}")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.View)]
    [ProducesResponseType(typeof(PagedResultDto<EventDto>), 200)]
    public async Task<ActionResult<PagedResultDto<EventDto>>> GetByStatusAsync([FromRoute] int status, [FromQuery] GetEventListRequestDto input)
    {
        input.Status = status;
        var result = await _eventAppService.GetByStatusAsync(input);
        return Ok(result);
    }
}
