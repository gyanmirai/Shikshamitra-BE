using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Document uploads for one concession type in a Step-5 PUT request.
/// </summary>
public class ConcessionTypeDocumentBlockDto
{
    /// <summary>Concession type UUID (from Step 3 mapping).</summary>
    [Required]
    public Guid ConcessionTypeId { get; set; }

    /// <summary>
    /// All document uploads for this concession type.
    /// Must include every REQUIRED document; OPTIONAL documents may be omitted.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one document entry is required per concession type.")]
    public List<DocumentUploadEntryDto> Documents { get; set; } = new();
}
