using ShikshaMitra.Finance.ConcessionRequests;
using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Response DTO for <c>GET /api/concession-request/{id}</c>.
///
/// <para>
/// Aggregates the anchor request's identity, the student's enrollment context
/// (board / medium / standard / division), and <em>all</em> concession-request
/// records that belong to the same student and share the same lifecycle status
/// as the anchor.
/// </para>
/// </summary>
public class ConcessionRequestDetailDto
{
    /// <summary>ID of the anchor <c>ConcessionRequest</c>.</summary>
    public Guid ConcessionRequestId { get; init; }

    /// <summary>Board the student is enrolled under (sourced from the Student record).</summary>
    public Guid? BoardId { get; init; }

    /// <summary>Education medium the student is enrolled under (sourced from the Student record).</summary>
    public Guid EducationMediumId { get; init; }

    /// <summary>Standard (class) the student is enrolled in (sourced from the Student record).</summary>
    public Guid? StandardId { get; init; }

    /// <summary>Division (section) the student is assigned to (sourced from the Student record).</summary>
    public Guid? DivisionId { get; init; }

    /// <summary>Student who made the concession request.</summary>
    public Guid StudentId { get; init; }

    /// <summary>
    /// Maps the domain <see cref="ConcessionRequestStatus"/> enum to the integer
    /// values defined by the API specification.
    /// <list type="bullet">
    ///   <item><description>0 – DRAFT</description></item>
    ///   <item><description>1 – PENDING</description></item>
    ///   <item><description>2 – APPROVED</description></item>
    ///   <item><description>3 – REJECTED</description></item>
    ///   <item><description>4 – CANCELLED</description></item>
    /// </list>
    /// </summary>
    public int Status { get; init; }

    /// <summary>
    /// All concession-type selections for this student that share the same status
    /// as the anchor request — one item per <c>ConcessionRequest</c> entity.
    /// </summary>
    public List<SelectedConcessionTypeDto> SelectedConcessionTypes { get; init; } = new();

    /// <summary>UTC timestamp of the anchor request's creation.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>UTC timestamp of the anchor request's last modification.</summary>
    public DateTime UpdatedAt { get; init; }

    // -------------------------------------------------------------------------
    // Status mapping helper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Maps the domain <see cref="ConcessionRequestStatus"/> enum to the integer
    /// values defined by the API specification (mirrors the enum integer values).
    /// </summary>
    public static int MapStatus(ConcessionRequestStatus status) => (int)status;
}
