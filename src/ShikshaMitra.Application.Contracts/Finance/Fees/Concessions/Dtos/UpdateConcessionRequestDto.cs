using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Payload for updating an existing ConcessionRequest.
/// Only requests in <b>Pending</b> status may be updated.
///
/// <para>
/// The <c>concessionTypeIds</c> list defines the desired final state for the
/// student's Pending requests.  The server performs a full batch replace:
/// <list type="bullet">
///   <item><description>Pending requests for types no longer in the list are deleted.</description></item>
///   <item><description>Types in the list without an existing Pending request get a new request created.</description></item>
///   <item><description>Types already having a non-rejected (e.g. Approved) request are rejected.</description></item>
/// </list>
/// </para>
/// </summary>
public class UpdateConcessionRequestDto
{
    /// <summary>ID of the board the student is enrolled under.</summary>
    [Required]
    public Guid BoardId { get; set; }

    /// <summary>ID of the education medium the student is enrolled under.</summary>
    [Required]
    public Guid EducationMediumId { get; set; }

    /// <summary>ID of the standard (class) the student is enrolled in.</summary>
    [Required]
    public Guid StandardId { get; set; }

    /// <summary>ID of the division (section) the student is assigned to.</summary>
    [Required]
    public Guid DivisionId { get; set; }

    /// <summary>ID of the student whose requests are being updated.</summary>
    [Required]
    public Guid StudentId { get; set; }

    /// <summary>
    /// Updated list of concession-type IDs — the complete desired selection.
    /// Must contain at least one entry.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one concession type must be selected.")]
    public List<Guid> ConcessionTypeIds { get; set; } = new();
}
