using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// Request body for PUT /api/concession-requests/{concessionRequestId}/feeHeadsData.
///
/// <para>
/// Step 3 — Fee Head data (Save &amp; Continue). Full replacement operation.
/// Each entry maps one concession type to its selected fee heads.
/// All existing fee-head selections for the listed concession types are discarded
/// and replaced with the supplied <c>selectedFeeHeadIds</c>.
/// </para>
/// </summary>
public class SaveFeeHeadsDataDto
{
    /// <summary>
    /// One entry per concession type included in this student's Draft request batch.
    /// Must contain at least one item.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one concession type fee-heads entry is required.")]
    public List<ConcessionTypeFeeHeadsDto> FeeHeadsData { get; set; } = new();
}
