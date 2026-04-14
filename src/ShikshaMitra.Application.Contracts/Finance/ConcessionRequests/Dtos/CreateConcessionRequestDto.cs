using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// Payload for creating a new ConcessionRequest (Step 1 + Step 2).
/// Captures the student's enrollment context so the server can verify
/// the student belongs to the specified board / medium / standard / division
/// before persisting any request.
/// </summary>
public class CreateConcessionRequestDto
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

    /// <summary>ID of the student requesting the concession.</summary>
    [Required]
    public Guid StudentId { get; set; }

    /// <summary>
    /// One or more concession-type IDs selected by the requestor (Step 2).
    /// Each ID results in a separate <c>ConcessionRequest</c> record.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one concession type must be selected.")]
    public List<Guid> ConcessionTypeIds { get; set; } = new();
}
