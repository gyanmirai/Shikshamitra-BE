using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// One document slot in a Step-5 GET response.
/// Combines the concession-type document template (name, accepted formats) with
/// the previously saved upload state (URL, status, uploaded timestamp).
/// </summary>
public class DocumentSlotDto
{
    /// <summary>
    /// Document template UUID (<c>ConcessionTypeDocument.Id</c>).
    /// Used as the key when submitting uploads in the Step-5 PUT.
    /// </summary>
    public Guid DocumentId { get; set; }

    /// <summary>Display label shown in the upload UI (e.g. "Mark Sheet / Report Card").</summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>
    /// File types the UI should accept. Integer values of the <c>DocumentType</c> enum
    /// (e.g. <c>[0, 1, 2]</c> = PDF, JPG, PNG).
    /// </summary>
    public List<int> AcceptedFormat { get; set; } = new();

    /// <summary>
    /// Previously uploaded file URL. <c>null</c> when the slot has not yet been filled.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Upload state. 0 = Pending (not yet uploaded); 1 = Uploaded.
    /// Defaults to 0 for slots that have never been submitted.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Timestamp of the most recent upload write.
    /// <c>null</c> when the slot has never been submitted.
    /// </summary>
    public DateTime? UploadedAt { get; set; }
}
