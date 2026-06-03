using ShikshaMitra.Finance.Audit;
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

    /// <summary>
    /// Provisional fee receipt number (KFE-*) when payment required verification,
    /// or final receipt number (FIN-*) for immediately-confirmed payments.
    /// Null when no fee lines were present.
    /// </summary>
    public string? FeeReceiptNumber { get; set; }

    /// <summary>
    /// Database ID of the primary fee receipt (FIN-* or KFE-*) created for this transaction.
    /// Use this to open the receipt preview dialog. Null when no fee lines were present.
    /// </summary>
    public Guid? FeeReceiptId { get; set; }

    public double ReceivedAmount { get; set; }

    public List<PaidDueFeeInfoDto> PaidDueFees { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Groups all audit events written for this collection action.</summary>
    public string? CorrelationId { get; set; }

    public List<Guid> AuditEventIds { get; set; } = [];

    /// <summary>Immediate audit summary for post-payment UI (Layer C).</summary>
    public FinanceAuditSummaryDto? AuditSummary { get; set; }
}

/// <summary>Information about a paid due fee.</summary>
public class PaidDueFeeInfoDto
{
    public Guid DueFeeId { get; set; }
    public Guid FeeHeadId { get; set; }
    public double AmountPaid { get; set; }
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
