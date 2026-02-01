using System;
using ShikshaMitra.Groups;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.Groups;

/// <summary>
/// DTO for requesting a paginated list of groups
/// </summary>
public class GetGroupListRequestDto : PagedAndSortedResultRequestDto
{
    /// <summary>
    /// Academic Year ID to filter groups
    /// </summary>
    public Guid AcademicYearId { get; set; }

    /// <summary>
    /// Optional filter by name or description
    /// </summary>
    public string? Filter { get; set; }

    /// <summary>
    /// Optional filter by selection mode
    /// </summary>
    public GroupSelectionMode? SelectionMode { get; set; }
}
