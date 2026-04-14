using System;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// Response DTO for <c>POST /api/concession-requests/{studentConcessionTypeRequestId}/reject</c>.
/// </summary>
public class ConcessionRejectionResponseDto
{
    /// <summary>Identifier of the rejected concession request.</summary>
    public Guid StudentConcessionTypeRequestId { get; set; }

    /// <summary>
    /// API-level status after rejection (2 = Rejected, consistent with the
    /// status mapping used across all other concession-request endpoints).
    /// </summary>
    public int Status { get; set; }

    /// <summary>Human-readable status label.</summary>
    public string StatusName { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the rejection was recorded.</summary>
    public DateTime RejectedAt { get; set; }

    /// <summary>User ID of the rejector (current authenticated user).</summary>
    public Guid RejectorId { get; set; }

    /// <summary>Stored rejection reason supplied by the rejector.</summary>
    public string RejectionReason { get; set; } = string.Empty;

    /// <summary>Human-readable success confirmation.</summary>
    public string Message { get; set; } = "Application rejected successfully";
}
