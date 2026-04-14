using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// Request body for PUT /api/concession-request/{concessionRequestId}/description.
///
/// <para>
/// Step 6 — Save Description (optional). The <c>description</c> field may be omitted,
/// null, or empty — all are treated as "clear the description".
/// </para>
/// </summary>
public class SaveDescriptionDto
{
    /// <summary>
    /// Optional free-text description for the concession request.
    /// May be null or omitted to clear an existing description.
    /// </summary>
    [MaxLength(2000)]
    public string? Description { get; set; }
}
