using System;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// A submitted document entry within a student concession-request list item.
/// </summary>
public class ConcessionRequestDocumentItemDto
{
    /// <summary>References <c>ConcessionTypeDocument.Id</c> (the template slot).</summary>
    public Guid DocumentId { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    /// <summary>Presigned / permanent download URL of the uploaded file.</summary>
    public string? Url { get; set; }

    /// <summary>UTC timestamp when the document was uploaded (null if not yet uploaded).</summary>
    public DateTime? UploadedAt { get; set; }

    /// <summary>Upload status: 0 = Pending, 1 = Uploaded.</summary>
    public int Status { get; set; }
}
