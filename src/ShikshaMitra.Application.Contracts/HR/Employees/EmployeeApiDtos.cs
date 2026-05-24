using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Employee list response with pagination (OpenAPI contract)
/// </summary>
public class EmployeeListResponse
{
    [Required]
    public int TotalCount { get; set; }

    [Required]
    public int CurrentPage { get; set; }

    [Required]
    public int PageSize { get; set; }

    [Required]
    public int TotalPages { get; set; }

    [Required]
    public List<EmployeeSummary> Items { get; set; } = new();
}

/// <summary>
/// Employee summary for list view (OpenAPI contract)
/// </summary>
public class EmployeeSummary
{
    /// <summary>
    /// Unique identifier for the employee
    /// </summary>
    [Required]
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string? RoleName { get; set; }

    public string? FullName { get; set; }

    public string? ThumbnailPicture { get; set; }

    public string? EmployeeIdentifier { get; set; }

    public DateTime? Dob { get; set; }

    public string? Gender { get; set; }

    public DateTime? Doj { get; set; }

    public string? Email { get; set; }

    public string? MobileNo { get; set; }

    public string? Department { get; set; }

    public string? Designation { get; set; }

    public string? BloodGroup { get; set; }

    public string? Address { get; set; }

    public bool IsTeaching { get; set; }

    public ShiftInfo? Shift { get; set; }

    public List<EmployeeOrganizationDto> Organizations { get; set; } = new();

    public List<string> OrganizationNames { get; set; } = new();

    public int Status { get; set; }
}

public class EmployeeOrganizationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Employee details (OpenAPI contract)
/// </summary>
public class EmployeeDetails : EmployeeSummary
{
    // Additional fields can be added here if needed
}

/// <summary>
/// Shift information (OpenAPI contract)
/// </summary>
public class ShiftInfo
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string StartTime { get; set; } = string.Empty;

    [Required]
    public string EndTime { get; set; } = string.Empty;
}
