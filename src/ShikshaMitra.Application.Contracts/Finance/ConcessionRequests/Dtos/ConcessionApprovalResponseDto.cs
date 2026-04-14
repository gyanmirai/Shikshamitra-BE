using System;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// Response DTO for <c>POST /api/concession-requests/{studentConcessionTypeRequestId}/approve</c>.
/// </summary>
public class ConcessionApprovalResponseDto
{
    /// <summary>Identifier of the approved concession request.</summary>
    public Guid StudentConcessionTypeRequestId { get; set; }

    /// <summary>
    /// API-level status after approval (1 = Approved, consistent with the existing
    /// status mapping used across all other concession-request endpoints).
    /// </summary>
    public int Status { get; set; }

    /// <summary>Human-readable status label.</summary>
    public string StatusName { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the approval was recorded.</summary>
    public DateTime ApprovedAt { get; set; }

    /// <summary>User ID of the approver (current authenticated user).</summary>
    public Guid ApproverId { get; set; }

    /// <summary>Approver message stored on the request; null when none was supplied.</summary>
    public string? ApproverMessage { get; set; }

    /// <summary>Human-readable success confirmation.</summary>
    public string Message { get; set; } = "Application approved successfully";
}
