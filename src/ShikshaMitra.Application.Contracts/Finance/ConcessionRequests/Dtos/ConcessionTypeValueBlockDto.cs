using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// Groups all fee-head value entries for one concession type (Step 4).
/// </summary>
public class ConcessionTypeValueBlockDto
{
    /// <summary>Concession type UUID from Step 2 / Step 3.</summary>
    [Required]
    public Guid ConcessionTypeId { get; set; }

    /// <summary>
    /// Discount entries for every fee head mapped to this concession type.
    /// All fee heads from Step 3 must be present.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one fee head value entry is required per concession type.")]
    public List<FeeHeadValueEntryDto> FeeHeadValues { get; set; } = new();
}
