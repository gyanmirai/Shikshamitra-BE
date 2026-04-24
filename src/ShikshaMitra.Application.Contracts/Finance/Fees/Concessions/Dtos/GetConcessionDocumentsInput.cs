using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Query parameters for
/// <c>GET /api/concession-requests/{studentConcessionTypeRequestId}/documents</c>.
/// </summary>
public class GetConcessionDocumentsInput
{
    /// <summary>
    /// Optional filter by document upload status.
    /// 0 = PENDING (not yet uploaded), 1 = UPLOADED.
    /// When omitted all documents are returned regardless of status.
    /// </summary>
    [Range(0, 1, ErrorMessage = "Status must be 0 (PENDING) or 1 (UPLOADED).")]
    public int? Status { get; set; }

    /// <summary>Page number (1-based). Defaults to 1.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page must be >= 1.")]
    public int Page { get; set; } = 1;

    /// <summary>Records per page. Minimum 1, Maximum 100. Defaults to 20.</summary>
    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")]
    public int PageSize { get; set; } = 20;
}
