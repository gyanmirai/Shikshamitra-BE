using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Response body for
/// <c>GET /api/concession-requests/{studentConcessionTypeRequestId}/documents</c>.
///
/// <para>
/// Returns a paginated flat list of all document slots for the specified concession request,
/// each merged with its upload state. Slots that have never been submitted carry
/// <c>url=null</c> and <c>status=0</c>.
/// </para>
/// </summary>
public class ConcessionDocumentsDto
{
    /// <summary>Always <c>true</c> for a successful response.</summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// The concession-request identifier echoed back from the path parameter.
    /// </summary>
    public Guid StudentConcessionTypeRequestId { get; set; }

    /// <summary>Total number of matching document slots across all pages.</summary>
    public long TotalCount { get; set; }

    /// <summary>Current page number returned (1-based).</summary>
    public int CurrentPage { get; set; }

    /// <summary>Number of records per page used in this response.</summary>
    public int PageSize { get; set; }

    /// <summary>Total number of pages available.</summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Flat list of document items for the current page.
    /// Empty when no documents exist or no slots match the applied filters.
    /// </summary>
    public List<ConcessionDocumentItemDto> Items { get; set; } = new();
}
