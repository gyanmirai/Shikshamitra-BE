using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// Request body for <c>POST /api/concession-requests/{studentConcessionTypeRequestId}/approve</c>.
/// </summary>
public class ApproveConcessionRequestDto
{
    /// <summary>
    /// Optional approval message or conditions from the approver.
    /// </summary>
    [MaxLength(1000)]
    public string? Message { get; set; }
}
