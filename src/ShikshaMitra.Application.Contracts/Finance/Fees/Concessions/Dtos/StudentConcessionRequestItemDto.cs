using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// A single concession-request row in the student table-view response.
/// </summary>
public class StudentConcessionRequestItemDto
{
    public Guid StudentConcessionTypeRequestId { get; set; }

    public Guid ConcessionTypeId { get; set; }

    public string ConcessionTypeName { get; set; } = string.Empty;

    /// <summary>API status: 0 = Pending, 1 = Approved, 2 = Rejected, 3 = Draft.</summary>
    public int Status { get; set; }

    /// <summary>Total concession amount granted (sum of per-fee-head discount values).</summary>
    public decimal TotalAmount { get; set; }

    public List<ConcessionRequestFeeHeadItemDto> FeeHeads { get; set; } = new();

    public List<ConcessionRequestDocumentItemDto> SubmittedDocuments { get; set; } = new();

    /// <summary>True when all required document slots for this concession type have been uploaded.</summary>
    public bool IsDocumentUploaded { get; set; }

    /// <summary>UTC timestamp of request creation.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of last modification.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Rejection reason (set when Status = Rejected); null otherwise.</summary>
    public string? Message { get; set; }

    /// <summary>UTC timestamp of approval; null when not approved.</summary>
    public DateTime? ApprovedAt { get; set; }
}
