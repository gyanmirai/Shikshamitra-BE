using System;
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
    /// Includes rule, member, and resolved selection details in list results.
    /// Keep this false for lookup/dropdown use cases.
    /// </summary>
    public bool IncludeDetails { get; set; }
}
