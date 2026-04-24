using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Associates one concession type with the fee heads selected for it (Step 3).
/// </summary>
public class ConcessionTypeFeeHeadsDto
{
    /// <summary>ID of the concession type whose fee heads are being specified.</summary>
    [Required]
    public Guid ConcessionTypeId { get; set; }

    /// <summary>
    /// IDs of the fee heads selected for this concession type.
    /// Must contain at least one entry.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one fee head must be selected per concession type.")]
    public List<Guid> SelectedFeeHeadIds { get; set; } = new();
}
