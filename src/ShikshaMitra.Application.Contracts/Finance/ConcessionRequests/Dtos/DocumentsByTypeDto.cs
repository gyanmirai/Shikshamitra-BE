using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// All document slots for one concession type, merged with saved upload state
/// (Step 5 GET response).
/// </summary>
public class DocumentsByTypeDto
{
    /// <summary>Concession type UUID.</summary>
    public Guid ConcessionTypeId { get; set; }

    /// <summary>Display name of the concession type (e.g. "Merit Scholarship").</summary>
    public string ConcessionTypeName { get; set; } = string.Empty;

    /// <summary>
    /// All document slots defined on the concession type (required + optional).
    /// Each slot is enriched with the previously uploaded URL and status when available.
    /// </summary>
    public List<DocumentSlotDto> Documents { get; set; } = new();
}
