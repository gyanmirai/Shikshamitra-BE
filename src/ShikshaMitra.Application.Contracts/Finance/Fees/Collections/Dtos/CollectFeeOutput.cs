using ShikshaMitra.Finance.FeeCollection;
using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Collections.Dtos;

/// <summary>Response returned after successfully collecting a fee payment.</summary>
public class CollectFeeOutput
{
    /// <summary>Unique identifier for this fee collection transaction.</summary>
    public Guid Id { get; set; }

    /// <summary>Unique receipt number formatted as REC-{YYYY}-{NNNNNN}.</summary>
    public string ReceiptNumber { get; set; } = string.Empty;

    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }

    /// <summary>Total amount collected.</summary>
    public double TotalAmount { get; set; }

    /// <summary>Fee heads that were paid in this transaction.</summary>
    public List<PaidFeeHeadInfoDto> PaidFeeHeads { get; set; } = [];

    /// <summary>Charges that were paid in this transaction.</summary>
    public List<PaidChargeInfoDto>? PaidCharges { get; set; }

    public FeeCollectionStatus Status { get; set; }

    /// <summary>User ID who collected the payment.</summary>
    public Guid CollectedBy { get; set; }

    /// <summary>Timestamp when payment was collected.</summary>
    public DateTimeOffset CollectedAt { get; set; }

    /// <summary>URL to download the payment receipt PDF (null until generated).</summary>
    public string? ReceiptUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Information about a paid fee head.</summary>
public class PaidFeeHeadInfoDto
{
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public double AmountPaid { get; set; }
}

/// <summary>Information about a paid charge.</summary>
public class PaidChargeInfoDto
{
    public Guid ChargeId { get; set; }
    public string ChargeName { get; set; } = string.Empty;
    public double AmountPaid { get; set; }
}
