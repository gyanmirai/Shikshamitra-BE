using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// A single document upload entry within a Step-5 PUT request.
/// </summary>
public class DocumentUploadEntryDto
{
    /// <summary>
    /// UUID of the document template slot being filled.
    /// References <c>ConcessionTypeDocument.Id</c> — defined on the concession type.
    /// </summary>
    [Required]
    public Guid DocumentId { get; set; }

    /// <summary>Presigned S3 or permanent URL of the uploaded file.</summary>
    [Required]
    [MaxLength(2048)]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Upload state. 0 = Pending (queued but not yet accessible); 1 = Uploaded (accessible).
    /// </summary>
    [Required]
    [Range(0, 1, ErrorMessage = "status must be 0 (Pending) or 1 (Uploaded).")]
    public int Status { get; set; }
}
