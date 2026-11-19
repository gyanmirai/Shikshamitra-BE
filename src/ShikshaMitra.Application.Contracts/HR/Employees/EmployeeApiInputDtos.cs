using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Input for getting employee list with filtering
/// </summary>
public class GetEmployeeListInput : PagedAndSortedResultRequestDto
{
    public string? Status { get; set; }

    public bool? IsTeaching { get; set; }

    public Guid? RoleId { get; set; }

    public Guid? MediumStageId { get; set; }

    public Guid? StandardId { get; set; }

    public string? Designation { get; set; }

    public string? ReportingManagerStatus { get; set; }

    public string? Search { get; set; }
}
