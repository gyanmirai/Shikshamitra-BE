using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.FeeRefund.Dtos;

/// <summary>Response DTO for a single <see cref="FeeRefundRequest"/>.</summary>
public class FeeRefundRequestDto
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }

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

    // ── Denormalized student snapshot (populated by the list / get endpoints) ──

    /// <summary>Full display name: FirstName [MiddleName] LastName.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>School-assigned GR number.</summary>
    public int? StudentGrNo { get; set; }

    /// <summary>Roll number within the division.</summary>
    public int? StudentRollNo { get; set; }

    /// <summary>Standard / class name (e.g. "Class 5", "Nursery").</summary>
    public string? ClassName { get; set; }

    /// <summary>Division name (e.g. "A").</summary>
    public string? DivisionName { get; set; }

    /// <summary>Student's primary contact number.</summary>
    public string? StudentContactNo { get; set; }
}

/// <summary>Response DTO for a single refund line.</summary>
public class FeeRefundLineDto
{
    public Guid Id { get; set; }

    public Guid DueFeeId { get; set; }

    public double Amount { get; set; }
}
