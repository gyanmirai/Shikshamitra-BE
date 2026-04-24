using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Request body for PUT /api/concession-request/{concessionRequestId}/value-entry.
///
/// <para>
/// Step 4 — Value Entry (Save &amp; Continue). Full replacement — all concession types from
/// Step 3 must be included. The server computes and stores <c>discountAmount</c> and
/// <c>netAmount</c> (post-concession) for each fee head using the supplied
/// <c>basedOn</c>, <c>amount</c>, and <c>annualBaseAmount</c>.
/// </para>
/// </summary>
public class ValueEntryDto
{
    /// <summary>
    /// Full value-entry payload for every concession type in this request.
    /// All concession types from Step 3 must be included.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one concession type value block is required.")]
    public List<ConcessionTypeValueBlockDto> ConcessionTypeValues { get; set; } = new();
}
