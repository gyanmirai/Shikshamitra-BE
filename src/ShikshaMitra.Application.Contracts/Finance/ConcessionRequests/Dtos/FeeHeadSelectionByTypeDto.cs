using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// One entry in the Step 3 GET response — represents a single concession type
/// together with the fee heads that have been saved against it.
/// </summary>
public class FeeHeadSelectionByTypeDto
{
    /// <summary>ID of the concession type (Step 2 selection).</summary>
    public Guid ConcessionTypeId { get; init; }

    /// <summary>Display name of the concession type.</summary>
    public string ConcessionName { get; init; } = string.Empty;

    /// <summary>
    /// Whether this concession type applies as a percentage or flat amount.
    /// Returns <c>"PERCENTAGE"</c>, <c>"FLAT"</c>, or <c>null</c> when not yet configured.
    /// </summary>
    public string? DiscountType { get; init; }

    /// <summary>
    /// IDs of the fee heads previously saved for this concession type.
    /// Returns an empty list when no fee heads have been saved yet —
    /// this allows the UI to pre-populate checkboxes correctly on resume.
    /// </summary>
    public List<Guid> FeeHeadIds { get; init; } = new();
}
