using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Calendar;

/// <summary>
/// Holiday Management Controller
/// Manages holidays with temporal properties (start/end dates) and status tracking
/// </summary>
[Area("app")]
[Route("api/calendar/holidays")]
[Authorize]
public class HolidayController : ShikshaMitraController
{
    private readonly IHolidayAppService _holidayAppService;

    public HolidayController(IHolidayAppService holidayAppService)
    {
        _holidayAppService = holidayAppService;
    }

    /// <summary>
    /// Get paginated list of holidays with filtering and sorting
    /// </summary>
    /// <remarks>
    /// Supports filtering by:
    /// - Name (case-insensitive substring match)
    /// - Holiday Type/EventType
    /// - Status (0=Upcoming, 1=Ongoing, 2=Completed)
    /// - Date range (StartDate/EndDate)
    /// </remarks>
    /// <param name="input">Pagination, filtering, and sorting parameters</param>
    /// <returns>Paginated list of holidays</returns>
    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.View)]
    [ProducesResponseType(typeof(PagedResultDto<HolidayDto>), StatusCodes.Status200OK)]
    public virtual async Task<PagedResultDto<HolidayDto>> GetListAsync(
        [FromQuery] GetHolidayListRequestDto input)
    {
        return await _holidayAppService.GetListAsync(input);
    }

    /// <summary>
    /// Get holiday by ID
    /// </summary>
    /// <param name="id">Holiday ID</param>
    /// <returns>Holiday details</returns>
    [HttpGet("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.View)]
    [ProducesResponseType(typeof(HolidayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<HolidayDto> GetAsync(Guid id)
    {
        return await _holidayAppService.GetAsync(id);
    }

    /// <summary>
    /// Create a new holiday
    /// </summary>
    /// <param name="academicYearId">Academic Year ID from query parameter</param>
    /// <param name="input">Holiday creation data</param>
    /// <returns>Created holiday</returns>
    [HttpPost]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.Add)]
    [ProducesResponseType(typeof(HolidayDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<HolidayDto> CreateAsync(
        [FromQuery] Guid academicYearId,
        [FromBody] CreateUpdateHolidayDto input)
    {
        input.AcademicYearId = academicYearId;
        return await _holidayAppService.CreateAsync(input);
    }

    /// <summary>
    /// Update an existing holiday
    /// </summary>
    /// <param name="id">Holiday ID</param>
    /// <param name="input">Holiday update data</param>
    /// <returns>Updated holiday</returns>
    [HttpPut("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.Add)]
    [ProducesResponseType(typeof(HolidayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<HolidayDto> UpdateAsync(Guid id,
        [FromBody] CreateUpdateHolidayDto input)
    {
        return await _holidayAppService.UpdateAsync(id, input);
    }

    /// <summary>
    /// Delete a holiday
    /// </summary>
    /// <param name="id">Holiday ID</param>
    [HttpDelete("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _holidayAppService.DeleteAsync(id);
    }

    /// <summary>
    /// Get holidays by type (EventType)
    /// </summary>
    /// <param name="typeId">Event type ID</param>
    /// <returns>List of holidays for the given type</returns>
    [HttpGet("by-type/{typeId}")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.View)]
    [ProducesResponseType(typeof(PagedResultDto<HolidayDto>), StatusCodes.Status200OK)]
    public virtual async Task<PagedResultDto<HolidayDto>> GetByTypeAsync(Guid typeId)
    {
        var input = new GetHolidayListRequestDto { TypeId = typeId };
        return await _holidayAppService.GetListAsync(input);
    }

    /// <summary>
    /// Get holidays by status
    /// </summary>
    /// <param name="status">Holiday status (0=Upcoming, 1=Ongoing, 2=Completed)</param>
    /// <returns>List of holidays with the given status</returns>
    [HttpGet("by-status/{status}")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.View)]
    [ProducesResponseType(typeof(PagedResultDto<HolidayDto>), StatusCodes.Status200OK)]
    public virtual async Task<PagedResultDto<HolidayDto>> GetByStatusAsync(int status)
    {
        var input = new GetHolidayListRequestDto { Status = status };
        return await _holidayAppService.GetByStatusAsync(input);
    }
}
