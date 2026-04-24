using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Query parameters for <c>GET /api/concession-requests/{studentId}</c>.
/// </summary>
public class GetStudentConcessionRequestsInput
{
    /// <summary>
    /// Filter by API-level status: 0 = Pending, 1 = Approved, 2 = Rejected.
    /// When omitted, all statuses are returned.
    /// </summary>
    public int? Status { get; set; }

    /// <summary>Page number (1-based). Defaults to 1.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>Items per page. Defaults to 20, maximum 100.</summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Filters by concession type name (case-insensitive contains).
    /// Must be at least 2 characters when provided.
    /// </summary>
    [MinLength(2)]
    public string? Search { get; set; }
}
