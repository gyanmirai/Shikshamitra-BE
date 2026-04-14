using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// Paged response for <c>GET /api/concession-requests/{studentId}</c>.
/// Extends <see cref="PagedResultDto{T}"/> (which carries <c>TotalCount</c> and <c>Items</c>)
/// and adds student context and concession summary statistics.
/// </summary>
public class StudentConcessionRequestsDto : PagedResultDto<StudentConcessionRequestItemDto>
{
    /// <summary>Basic profile of the requested student.</summary>
    public StudentBasicDetailsDto StudentDetails { get; set; } = new();

    /// <summary>
    /// Unfiltered aggregate statistics for all of the student's concession applications.
    /// Counts are always computed across all statuses, regardless of the active filter.
    /// </summary>
    public ApplicationConcessionBasicDetailDto ConcessionBasicDetail { get; set; } = new();

    /// <summary>Current page number (1-based).</summary>
    public int CurrentPage { get; set; }

    /// <summary>Number of items per page.</summary>
    public int PageSize { get; set; }

    /// <summary>Total number of pages available for the active filter.</summary>
    public int TotalPages { get; set; }
}
