using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// Payload for rejecting a Pending ConcessionRequest.
/// A non-empty, meaningful reason is mandatory.
/// </summary>
public class RejectConcessionRequestDto
{
    [Required]
    [StringLength(2000, MinimumLength = 10,
        ErrorMessage = "Rejection reason must be between 10 and 2000 characters.")]
    public string RejectionReason { get; set; } = string.Empty;
}
