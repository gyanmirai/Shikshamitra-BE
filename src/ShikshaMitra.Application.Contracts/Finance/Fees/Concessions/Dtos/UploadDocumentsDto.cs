using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Request body for PUT /api/concession-request/{concessionRequestId}/document-upload.
///
/// <para>
/// Step 5 — Document Upload (Save &amp; Continue). Full replacement — all concession types
/// from Step 3 must be included. Each block lists the document uploads (URL + status)
/// for one concession type.
/// </para>
/// </summary>
public class UploadDocumentsDto
{
    /// <summary>
    /// Document uploads grouped by concession type.
    /// All concession types from Step 3 must be included.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one concession type document block is required.")]
    public List<ConcessionTypeDocumentBlockDto> ConcessionTypeDocuments { get; set; } = new();
}
