using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Paged response for <c>GET /api/concession-request/concession-applications</c>.
/// Extends <see cref="PagedResultDto{T}"/> (which carries <c>TotalCount</c> and <c>Items</c>)
/// and adds per-status counts and explicit pagination metadata.
/// </summary>
public class ConcessionApplicationsDto : PagedResultDto<ConcessionApplicationItemDto>
{
    /// <summary>
    /// Number of Pending concession applications matching the board, medium, and search filters
    /// (independent of the status filter applied to <c>Items</c>).
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// Number of Approved concession applications matching the board, medium, and search filters.
    /// </summary>
    public int ApprovedCount { get; set; }

    /// <summary>
    /// Number of Rejected concession applications matching the board, medium, and search filters.
    /// </summary>
    public int RejectedCount { get; set; }

    /// <summary>Current page number (1-based).</summary>
    public int CurrentPage { get; set; }

    /// <summary>Number of items per page.</summary>
    public int PageSize { get; set; }

    /// <summary>Total number of pages available for the active filter.</summary>
    public int TotalPages { get; set; }
}
