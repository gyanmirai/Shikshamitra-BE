using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Input for getting employee list with filtering
/// </summary>
public class GetEmployeeListInput : PagedAndSortedResultRequestDto
{
    public Guid? MediumStageId { get; set; }

    public string? Status { get; set; }

    public bool? IsTeaching { get; set; }

    public Guid? RoleId { get; set; }
}

/// <summary>
/// Input for searching employees
/// </summary>
public class SearchEmployeeInput
{
    public string? SearchTerm { get; set; }

    public int MaxResultCount { get; set; } = 50;
}
