using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.FeeRefund.Dtos;

/// <summary>Response DTO for a single <see cref="FeeRefundRequest"/>.</summary>
public class FeeRefundRequestDto
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }

	public FeeRefundStatus Status { get; set; }

    public string VoucherNumber { get; set; } = default!;

    public double TotalAmount { get; set; }

    public string? RefundMethod { get; set; }

    public string? Reason { get; set; }

    public string? ApproverNotes { get; set; }

    public Guid? ProcessedByUserId { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public DateTimeOffset CreationTime { get; set; }

    public List<FeeRefundLineDto> Lines { get; set; } = [];

    // ── Enhanced Response Fields for Refund Request View Screen ──

    public Guid RefundRequestId { get; set; }

    public int TotalFeeHeads { get; set; }

    public double TotalPaidAmount { get; set; }

    public double TotalRefundedAmount { get; set; }

    public List<RefundFeeHeadDto> FeeHeads { get; set; } = [];

    // ── Denormalized student snapshot (populated by the list / get endpoints) ──

    /// <summary>Full display name: FirstName [MiddleName] LastName.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>School-assigned GR number.</summary>
    public int? StudentGrNo { get; set; }

    /// <summary>Roll number within the division.</summary>
    public int? StudentRollNo { get; set; }

    /// <summary>Standard / class name (e.g. "Class 5", "Nursery").</summary>
    public string? StandardName { get; set; }

    /// <summary>Division name (e.g. "A").</summary>
    public string? DivisionName { get; set; }

    /// <summary>Student's primary contact number.</summary>
    public string? StudentContactNo { get; set; }

    public Guid? BoardId { get; set; }

    public string? BoardName { get; set; }

    public Guid? EducationMediumId { get; set; }

    public string? EducationMediumName { get; set; }
}

/// <summary>Response DTO for a single refund line.</summary>
public class FeeRefundLineDto
{
    public Guid Id { get; set; }

    public Guid DueFeeId { get; set; }

    public double Amount { get; set; }
}

public class RefundFeeHeadDto
{
    public Guid FeeHeadId { get; set; }

    public string FeeHeadName { get; set; } = string.Empty;

    public double TotalPaid { get; set; }

    public double TotalRefunded { get; set; }

    public List<RefundMonthItemDto> Items { get; set; } = [];
}

public class RefundMonthItemDto
{
    public Guid InstallmentId { get; set; }

    public string Month { get; set; } = string.Empty;

    public double Total { get; set; }

    public double Paid { get; set; }

    public double RefundAmount { get; set; }

    public int Status { get; set; }
}

