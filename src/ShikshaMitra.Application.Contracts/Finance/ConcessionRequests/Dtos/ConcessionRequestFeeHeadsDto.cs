using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// Response body for GET /api/concession-requests/{concessionRequestId}/fee-heads.
///
/// <para>
/// Step 3 — Edit / Resume. Returns only the previously saved fee-head selections
/// so the UI can pre-populate the checkboxes when resuming or editing an existing request.
/// The available fee-head pool is fetched independently via the standard fee-structure query.
/// </para>
/// </summary>
public class ConcessionRequestFeeHeadsDto
{
    /// <summary>ID of the anchor <c>ConcessionRequest</c> used to locate this batch.</summary>
    public Guid ConcessionRequestId { get; init; }

    /// <summary>
    /// ID of the student. Confirms the student context for the
    /// fee-structure query that loads the available fee-head pool in the UI.
    /// </summary>
    public Guid StudentId { get; init; }

    /// <summary>
    /// One entry per concession type selected in Step 2.
    /// <c>FeeHeadIds</c> is empty when no fee heads have been saved for that type yet.
    /// </summary>
    public List<FeeHeadSelectionByTypeDto> ConcessionTypes { get; init; } = new();
}
