using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.HR.Calendar.CalendarView;
using ShikshaMitra.Controllers;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HttpApi.Controllers.HR.Calendar;

/// <summary>
/// REST API Controller for Calendar View operations
/// Provides unified view of events and holidays
/// </summary>
[ApiController]
[Route("api/calendar")]
[Produces("application/json")]
public class CalendarViewController : ShikshaMitraController
{
    private readonly ICalendarViewAppService _calendarViewAppService;

    public CalendarViewController(ICalendarViewAppService calendarViewAppService)
    {
        _calendarViewAppService = calendarViewAppService;
    }

    /// <summary>
    /// Get calendar view data (unified events and holidays)
    /// Returns calendar items with visibility filtering based on user's group membership
    /// </summary>
    /// <remarks>
    /// Sample request:
    /// 
    ///     GET /api/calendar/view?academicYearId=550e8400-e29b-41d4-a716-446655440000&amp;page=1&amp;search=holiday
    /// 
    /// Supports filtering by:
    /// - academicYearId (required): Filter by academic year
    /// - eventTypeIds: Filter by specific event types
    /// - holidayTypeIds: Filter by specific holiday types
    /// - status: Filter by status (0=Upcoming, 1=Active/Ongoing, 2=Completed)
    /// - search: Search by title
    /// </remarks>
    /// <param name="input">Filter and pagination parameters</param>
    /// <returns>Paginated list of calendar items</returns>
    [HttpGet("view")]
    [ProducesResponseType(typeof(PagedResultDto<CalendarViewItemDto>), 200)]
    public async Task<ActionResult<PagedResultDto<CalendarViewItemDto>>> GetViewAsync(
        [FromQuery] GetCalendarViewRequestDto input)
    {
        var result = await _calendarViewAppService.GetViewAsync(input);
        return Ok(result);
    }

    /// <summary>
    /// Get event or holiday popup details
    /// Returns detailed information about a specific calendar item (event or holiday)
    /// </summary>
    /// <remarks>
    /// Sample request:
    /// 
    ///     GET /api/calendar/items/550e8400-e29b-41d4-a716-446655440000
    /// 
    /// Returns:
    /// - For events: id, calendarItemType, status, title, date, time, audienceCount, eventType, mode, venue, description
    /// - For holidays: id, calendarItemType, status, title, date, audienceCount, holidayType, description
    /// </remarks>
    /// <param name="itemId">Event or Holiday identifier (GUID)</param>
    /// <returns>Calendar item details</returns>
    [HttpGet("items/{itemId}")]
    [ProducesResponseType(typeof(CalendarItemDetailsDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<CalendarItemDetailsDto>> GetItemDetailsAsync(Guid itemId)
    {
        try
        {
            var result = await _calendarViewAppService.GetItemDetailsAsync(itemId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
