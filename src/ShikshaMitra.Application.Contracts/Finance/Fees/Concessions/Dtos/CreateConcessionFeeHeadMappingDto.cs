using System;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Finance.ConcessionRequests;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Input DTO for a single fee-head mapping within a ConcessionRequest.
/// </summary>
public class CreateConcessionFeeHeadMappingDto
{
    [Required]
    public Guid FeeHeadId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Pre-concession amount cannot be negative.")]
    public decimal PreConcessionFeeHeadAmount { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Concession value cannot be negative.")]
    public decimal UserSpecifiedConcessionAmountValue { get; set; }

    [Required]
    public AmountType UserSpecifiedConcessionAmountType { get; set; }
}
