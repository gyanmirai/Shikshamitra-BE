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

namespace ShikshaMitra.HR.Employees;

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

        // Call existing app service
        var result = await _employeeAppService.GetListAsync(input);

        // Calculate total pages
        var totalPages = (int)Math.Ceiling((double)result.TotalCount / pageSize);

        // Map to OpenAPI contract
        return new EmployeeListResponse
        {
            TotalCount = (int)result.TotalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            Items = result.Items.Select(MapToEmployeeSummary).ToList()
        };
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
        var summary = MapToEmployeeSummary(employee);

        return new EmployeeDetails
        {
            RoleName = summary.RoleName,
            FullName = summary.FullName,
            ThumbnailPicture = summary.ThumbnailPicture,
            EmployeeIdentifier = summary.EmployeeIdentifier,
            Dob = summary.Dob,
            Gender = summary.Gender,
            Doj = summary.Doj,
            Email = summary.Email,
            MobileNo = summary.MobileNo,
            IsTeaching = summary.IsTeaching,
            Shift = summary.Shift,
            Status = summary.Status
        };
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

        var result = await _employeeAppService.SearchAsync(input);

        return new EmployeeSearchResponse
        {
            Items = result.Select(e => new EmployeeSearchItem
            {
                RoleName = e.RoleName ?? "N/A",
                FullName = e.FullName ?? "N/A",
                ThumbnailPicture = e.ThumbnailPicture ?? string.Empty,
                EmployeeIdentifier = e.EmployeeCode ?? "N/A",
                Dob = e.DateOfBirth ?? DateTime.MinValue,
                Gender = e.Gender ?? "N/A",
                Doj = e.DateOfJoining ?? DateTime.MinValue,
                Email = e.Email ?? "N/A",
                MobileNo = e.MobileNo ?? "N/A",
                IsTeaching = e.IsTeaching,
                Shift = e.Shift != null ? new ShiftInfo
                {
                    Id = e.Shift.Id,
                    Name = e.Shift.Name,
                    StartTime = e.Shift.StartTime,
                    EndTime = e.Shift.EndTime
                } : new ShiftInfo { Id = Guid.Empty, Name = "N/A", StartTime = "00:00:00+05:30", EndTime = "00:00:00+05:30" },
                Status = e.Status
            }).ToList()
        };
    }

    private EmployeeSummary MapToEmployeeSummary(EmployeeDto employee)
    {
        return new EmployeeSummary
        {
            RoleName = employee.RoleName,
            FullName = employee.FullName,
            ThumbnailPicture = employee.ThumbnailPicture,
            EmployeeIdentifier = employee.EmployeeCode,
            Dob = employee.DateOfBirth,
            Gender = employee.Gender,
            Doj = employee.DateOfJoining,
            Email = employee.Email,
            MobileNo = employee.MobileNo,
            IsTeaching = employee.IsTeaching,
            Shift = employee.Shift != null ? new ShiftInfo
            {
                Id = employee.Shift.Id,
                Name = employee.Shift.Name,
                StartTime = employee.Shift.StartTime,
                EndTime = employee.Shift.EndTime
            } : null,
            Status = employee.Status
        };
    }
}
