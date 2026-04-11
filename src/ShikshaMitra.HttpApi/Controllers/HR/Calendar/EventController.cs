using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.HR.Calendar.Events;
using ShikshaMitra.Controllers;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HttpApi.Controllers.HR.Calendar;

/// <summary>
/// REST API Controller for Event management
/// </summary>
[ApiController]
[Route("api/calendar/events")]
[Produces("application/json")]
public class EventController : ShikshaMitraController
{
    private readonly IEventAppService _eventAppService;

    public EventController(IEventAppService eventAppService)
    {
        _eventAppService = eventAppService;
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
    [ProducesResponseType(typeof(EventDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<EventDto>> GetAsync(Guid id)
    {
        try
        {
            var result = await _eventAppService.GetAsync(id);
            return Ok(result);
        }
        catch (Exception)
        {
            return NotFound();
        }
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
    [ProducesResponseType(typeof(List<EventDto>), 200)]
    public async Task<ActionResult<List<EventDto>>> GetByTypeAsync(Guid typeId)
    {
        var result = await _eventAppService.GetByTypeAsync(typeId);
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
    [ProducesResponseType(typeof(PagedResultDto<EventDto>), 200)]
    public async Task<ActionResult<PagedResultDto<EventDto>>> GetByStatusAsync([FromRoute] int status, [FromQuery] GetEventListRequestDto input)
    {
        input.Status = status;
        var result = await _eventAppService.GetByStatusAsync(input);
        return Ok(result);
    }
}
