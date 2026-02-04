using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.Communication.AnnouncementTypes;

/// <summary>
/// Request DTO for getting paginated list of announcement types
/// </summary>
public class GetAnnouncementTypeListRequestDto : PagedAndSortedResultRequestDto
{
    /// <summary>
    /// Academic Medium ID filter (required)
    /// </summary>
    public Guid? AcademicMediumId { get; set; }

    /// <summary>
    /// Search by announcement type name (case-insensitive)
    /// </summary>
    public string? Search { get; set; }
}
