using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

/// <summary>
/// Employees Controller (OpenAPI contract compliant)
/// Provides read-only access to employee data
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees")]
[Authorize]
public class EmployeesController : AbpControllerBase
{
    private readonly IEmployeeAppService _employeeAppService;

    public EmployeesController(IEmployeeAppService employeeAppService)
    {
        _employeeAppService = employeeAppService;
    }

    /// <summary>
    /// Get all employees with pagination and filtering (OpenAPI contract)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(EmployeeListResponse), StatusCodes.Status200OK)]
    public virtual async Task<EmployeeListResponse> GetAllEmployeesAsync(
        [FromQuery] Guid? mediumStageId = null,
        [FromQuery] string? status = null,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(10, 100)] int pageSize = 10,
        [FromQuery] bool? isTeaching = null,
        [FromQuery] Guid? roleId = null)
    {
        // Create input for existing service
        var input = new GetEmployeeListInput
        {
            SkipCount = (page - 1) * pageSize,
            MaxResultCount = pageSize,
            MediumStageId = mediumStageId,
            Status = status,
            IsTeaching = isTeaching,
            RoleId = roleId
        };

        // Call the filtered list method
        return await _employeeAppService.GetFilteredListAsync(input);
    }

    /// <summary>
    /// Get employee by ID (OpenAPI contract)
    /// </summary>
    [HttpGet("{employeeId}")]
    [ProducesResponseType(typeof(EmployeeDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<EmployeeDetails> GetEmployeeByIdAsync(Guid employeeId)
    {
        var employee = await _employeeAppService.GetAsync(employeeId);
        return MapToEmployeeDetails(employee);
    }

    /// <summary>
    /// Search employees by name, identifier, email, or mobile (OpenAPI contract)
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(EmployeeSearchResponse), StatusCodes.Status200OK)]
    public virtual async Task<EmployeeSearchResponse> SearchEmployeesAsync(
        [FromQuery, MinLength(2)] string? search = null)
    {
        var input = new SearchEmployeeInput
        {
            SearchTerm = search,
            MaxResultCount = 50
        };

        // SearchAsync already returns EmployeeSearchResponse with properly mapped items
        return await _employeeAppService.SearchAsync(input);
    }

    private static EmployeeDetails MapToEmployeeDetails(EmployeeDto employee)
    {
        return new EmployeeDetails
        {
            Id = employee.Id,
            RoleName = employee.Role?.Name,
            FullName = employee.EmployeeName,
            ThumbnailPicture = employee.ProfilePictureBlobName,
            EmployeeIdentifier = employee.EmployeeCode,
            Dob = employee.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
            Gender = employee.GenderType?.ToString(),
            Doj = employee.DateOfJoining?.ToDateTime(TimeOnly.MinValue),
            Email = employee.Email,
            MobileNo = employee.MobileNo,
            IsTeaching = employee.EmployeeGroup == Common.EmployeeGroupType.Teaching,
            Shift = employee.Shift != null ? new ShiftInfo
            {
                Id = employee.Shift.Id,
                Name = employee.Shift.ShiftName ?? string.Empty,
                StartTime = employee.Shift.StartTime.ToString("HH:mm:ss"),
                EndTime = employee.Shift.EndTime.ToString("HH:mm:ss")
            } : null,
            Status = (int)(employee.EmployeeStatusType ?? Common.EmployeeStatusType.Active)
        };
    }
}
