using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// Request body for <c>POST /api/concession-requests/{studentConcessionTypeRequestId}/reject</c>.
/// The rejection reason is mandatory and must be meaningful (10 – 1 000 characters).
/// </summary>
public class RejectConcessionRequestDto
{
    [Required]
    [StringLength(1000, MinimumLength = 10,
        ErrorMessage = "Rejection reason must be between 10 and 1000 characters.")]
    public string Message { get; set; } = string.Empty;
}
