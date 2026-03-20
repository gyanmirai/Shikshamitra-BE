using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.AttendanceAndLeaves;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

/// <summary>
/// API Controller for managing employee leave requests.
/// Provides CRUD operations and leave status management.
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employee-leaves")]
[Authorize]
public class EmployeeLeaveController : AbpControllerBase
{
    private readonly IEmployeeLeaveService _employeeLeaveService;

    public EmployeeLeaveController(IEmployeeLeaveService employeeLeaveService)
    {
        _employeeLeaveService = employeeLeaveService;
    }

    /// <summary>
    /// Get paginated list of employee leaves.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<EmployeeLeaveDto>), StatusCodes.Status200OK)]
    public virtual async Task<PagedResultDto<EmployeeLeaveDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return await _employeeLeaveService.GetListAsync(input);
    }

    /// <summary>
    /// Get employee leave by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EmployeeLeaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<EmployeeLeaveDto> GetAsync(Guid id)
    {
        return await _employeeLeaveService.GetAsync(id);
    }

    /// <summary>
    /// Create a new employee leave request.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeLeaveDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<ActionResult<EmployeeLeaveDto>> CreateAsync([FromBody] CreateUpdateEmployeeLeaveDto input)
    {
        var result = await _employeeLeaveService.CreateAsync(input);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Update an existing employee leave request.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(EmployeeLeaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<EmployeeLeaveDto> UpdateAsync(Guid id, [FromBody] CreateUpdateEmployeeLeaveDto input)
    {
        return await _employeeLeaveService.UpdateAsync(id, input);
    }

    /// <summary>
    /// Delete an employee leave request.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> DeleteAsync(Guid id)
    {
        await _employeeLeaveService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Update leave request status (approve/reject).
    /// </summary>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(EmployeeLeaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<EmployeeLeaveDto> UpdateStatusAsync(Guid id, [FromBody] LeaveApprovalDto input)
    {
        return await _employeeLeaveService.UpdateLeaveRequestStatusAsync(id, input);
    }
}
