using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// Previously saved discount values for one concession type (Step 4 GET response).
/// </summary>
public class SavedValueEntryByTypeDto
{
    /// <summary>Concession type UUID.</summary>
    public Guid ConcessionTypeId { get; set; }

    /// <summary>Display name of the concession type (e.g. "Staff Discount").</summary>
    public string ConcessionName { get; set; } = string.Empty;

    /// <summary>
    /// Saved discount values for every fee head selected in Step 3.
    /// Hydrates <c>formik.values.valueEntries[concessionTypeId][feeHeadId]</c>.
    /// </summary>
    public List<SavedFeeHeadValueDto> FeeHeadValues { get; set; } = new();
}
