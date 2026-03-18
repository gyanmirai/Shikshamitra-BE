using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

/// <summary>
/// DTO for creating a new ConcessionRequest.
/// </summary>
public class CreateConcessionRequestDto
{
    [Required]
    public Guid StudentId { get; set; }

    [Required]
    public Guid ConcessionTypeId { get; set; }

    [StringLength(2000)]
    public string? Remarks { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one fee head mapping is required.")]
    public List<CreateConcessionFeeHeadMappingDto> FeeHeadMappings { get; set; } = new();
}

/// <summary>
/// DTO for updating a Draft ConcessionRequest.
/// </summary>
public class UpdateConcessionRequestDto
{
    [Required]
    public Guid ConcessionTypeId { get; set; }

    [StringLength(2000)]
    public string? Remarks { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one fee head mapping is required.")]
    public List<CreateConcessionFeeHeadMappingDto> FeeHeadMappings { get; set; } = new();
}

/// <summary>
/// DTO for rejecting a ConcessionRequest.
/// </summary>
public class RejectConcessionRequestDto
{
    [Required]
    [StringLength(2000, MinimumLength = 10,
        ErrorMessage = "Rejection reason must be between 10 and 2000 characters.")]
    public string RejectionReason { get; set; } = string.Empty;
}

/// <summary>
/// Input DTO for a single fee head mapping within a ConcessionRequest.
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

// Keep backward-compat alias used by the existing controller stub
public class CreateUpdateConcessionRequestDto : CreateConcessionRequestDto { }
