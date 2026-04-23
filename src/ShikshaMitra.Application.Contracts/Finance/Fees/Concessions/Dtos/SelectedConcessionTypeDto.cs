using System;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Represents a single concession-type selection within a concession request batch.
/// </summary>
public class SelectedConcessionTypeDto
{
    /// <summary>
    /// ID of the <c>ConcessionType</c> master record (the "what was requested").
    /// </summary>
    public Guid ConcessionTypeId { get; init; }

    /// <summary>
    /// ID of the <c>ConcessionRequest</c> entity that holds this specific type selection
    /// (the "request record" for this type within the student's batch).
    /// </summary>
    public Guid ConcessionRequestConcessionTypeId { get; init; }
}
