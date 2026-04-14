using System;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// One document slot in the flat document list returned by
/// <c>GET /api/concession-requests/{studentConcessionTypeRequestId}/documents</c>.
/// </summary>
public class ConcessionDocumentItemDto
{
    /// <summary>Unique identifier of the document template slot.</summary>
    public Guid DocumentId { get; set; }

    /// <summary>Display name of the document type (e.g. "Mark Sheet / Report Card").</summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>
    /// Presigned or permanent URL to access the uploaded file.
    /// Null when the document has not yet been uploaded (Status = 0).
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Upload status. 0 = PENDING (not yet uploaded), 1 = UPLOADED (successfully uploaded).
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// ISO 8601 UTC timestamp when the document was uploaded.
    /// Null when <see cref="Status"/> is 0 (PENDING).
    /// </summary>
    public DateTime? UploadedAt { get; set; }
}
