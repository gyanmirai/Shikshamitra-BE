using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// Response body for GET /api/concession-request/{concessionRequestId}/value-entries.
///
/// <para>
/// Step 4 — Value Entry (Edit / Resume). Returns the previously saved discount
/// values per concession type per fee head so that the UI can pre-populate
/// the input fields when resuming or editing an existing Draft request.
/// </para>
/// </summary>
public class ConcessionRequestValueEntriesDto
{
    /// <summary>ID of the anchor concession request used to identify the batch.</summary>
    public Guid ConcessionRequestId { get; set; }

    /// <summary>
    /// ID of the student. Tells the UI which student's fee structure to load for
    /// fee-head names and base amounts via <c>useGetStandardFeeStructureQuery(studentId)</c>.
    /// </summary>
    public Guid StudentId { get; set; }

    /// <summary>
    /// Previously saved discount values, one entry per concession type.
    /// Directly hydrates <c>formik.values.valueEntries[concessionTypeId][feeHeadId]</c>.
    /// </summary>
    public List<SavedValueEntryByTypeDto> ConcessionTypeValues { get; set; } = new();
}
