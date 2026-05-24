using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Calendar;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Controllers.HR.Calendar;

/// <summary>
/// Holiday Type Management Controller
/// Manages master data for different types of holidays (National Holiday, Festival, etc.)
/// </summary>
[ApiController]
[Route("api/calendar/holiday-types")]
[Authorize]
public class HolidayTypeController : ControllerBase
{
    private readonly IHolidayTypeAppService _appService;

    public HolidayTypeController(IHolidayTypeAppService appService)
    {
        _appService = appService;
    }

    /// <summary>
    /// Get paginated list of holiday types
    /// </summary>
    /// <returns>Paginated list of holiday types</returns>
    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.View)]
    public async Task<ActionResult<PagedResultDto<HolidayTypeDto>>> GetListAsync([FromQuery] GetHolidayTypeListRequestDto input)
    {
        var result = await _appService.GetListAsync(input);
        return Ok(result);
    }

    /// <summary>
    /// Get holiday type by ID
    /// </summary>
    /// <param name="id">Holiday type ID</param>
    /// <returns>Holiday type details</returns>
    [HttpGet("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.View)]
    public async Task<ActionResult<HolidayTypeDto>> GetAsync(Guid id)
    {
        var result = await _appService.GetAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Create a new holiday type
    /// </summary>
    /// <param name="input">Holiday type creation data</param>
    /// <returns>Created holiday type details</returns>
    [HttpPost]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.Add)]
    public async Task<ActionResult<HolidayTypeDto>> CreateAsync([FromBody] CreateUpdateHolidayTypeDto input)
    {
        var result = await _appService.CreateAsync(input);
        return CreatedAtAction(nameof(GetAsync), new { id = result.Id }, result);
    }

    /// <summary>
    /// Update an existing holiday type
    /// </summary>
    /// <param name="id">Holiday type ID</param>
    /// <param name="input">Holiday type update data</param>
    /// <returns>Updated holiday type details</returns>
    [HttpPut("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.Add)]
    public async Task<ActionResult<HolidayTypeDto>> UpdateAsync(Guid id, [FromBody] CreateUpdateHolidayTypeDto input)
    {
        var result = await _appService.UpdateAsync(id, input);
        return Ok(result);
    }

    /// <summary>
    /// Delete a holiday type
    /// </summary>
    /// <param name="id">Holiday type ID</param>
    [HttpDelete("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.Delete)]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        await _appService.DeleteAsync(id);
        return NoContent();
    }
}
