using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Receipts;

/// <summary>
/// Custom repository for <see cref="FeePaymentReceipt"/> that provides
/// optimised paged queries against the receipts table.
/// </summary>
public interface IFeePaymentReceiptRepository : IRepository<FeePaymentReceipt, Guid>
{
    // ── Reconciliation queries ─────────────────────────────────────────────

    /// <summary>
    /// Two-phase paged query for the reconciliation inbox.
    /// Phase 1: count + page of receipt IDs.
    /// </summary>
    Task<(int TotalCount, List<Guid> Ids)> GetReconciliationPagedIdsAsync(
        ReconciliationListFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hydrates full reconciliation row projections for the given receipt IDs.
    /// Returns instrument details from the linked <see cref="PaymentMethodRecord"/>.
    /// </summary>
    Task<List<ReconciliationRow>> GetReconciliationRowsByIdsAsync(
        List<Guid> receiptIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns per-method, per-status counts for tab badge rendering.
    /// One grouped DB query — not 7 round-trips.
    /// </summary>
    Task<List<ReconciliationStatusCount>> GetReconciliationStatusCountsAsync(
        Guid academicYearId,
        int? month,
        int? year,
        IQueryable<Guid>? scopedStudentIdsQuery = null,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns the total count and a paged, sorted slice of receipts matching
    /// the supplied filter. Results are ordered by <c>CreationTime</c> descending
    /// (newest first) with <c>ReceiptNumber</c> descending as a tie-breaker.
    /// </summary>
    Task<(int TotalCount, List<FeePaymentReceipt> Items)> GetPagedListAsync(
        FeeReceiptListFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the <see cref="FeePaymentReceipt"/> for a given receipt number, or null.
    /// </summary>
    Task<FeePaymentReceipt?> FindByReceiptNumberAsync(
        string receiptNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of receipts of a specific kind (used for receipt number sequencing).
    /// </summary>
    Task<long> GetCountByKindAsync(
        FeeReceiptKind kind,
        CancellationToken cancellationToken = default);
}

/// <summary>Filter parameters for the paginated receipt list query.</summary>
public sealed class FeeReceiptListFilter
{
    public Guid AcademicYearId { get; init; }
    public Guid BoardId { get; init; }
    public Guid EducationMediumId { get; init; }

    /// <summary>When set, restricts results to students accessible by the current user scope.</summary>
    public IQueryable<Guid>? ScopedStudentIdsQuery { get; init; }

    /// <summary>Month (1–12) derived from <see cref="FeePaymentReceipt.ReceiptDate"/>. Null = all months.</summary>
    public int? Month { get; init; }

    /// <summary>Optional kind filter (null = all kinds).</summary>
    public FeeReceiptKind? ReceiptKind { get; init; }

    /// <summary>Free-text: matches receipt number, student name, or GR number.</summary>
    public string? Search { get; init; }

    public Guid? StandardId { get; init; }

    public Guid? DivisionId { get; init; }

    public int SkipCount { get; init; }
    public int MaxResultCount { get; init; } = 20;
}

/// <summary>Filter for the reconciliation inbox query.</summary>
public sealed class ReconciliationListFilter
{
    public Guid AcademicYearId { get; init; }
    public int? Month { get; init; }
    public int? Year { get; init; }

    /// <summary>Specific payment method tab, or null for all methods.</summary>
    public PaymentMethodEnum? PaymentMethod { get; init; }

    /// <summary>Specific verification status, or null for all statuses.</summary>
    public FeeReceiptVerificationStatus? VerificationStatus { get; init; }

    /// <summary>Free-text search: student name, GR number, receipt number.</summary>
    public string? Search { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    /// <summary>When set, restricts results to students accessible by the current user scope.</summary>
    public IQueryable<Guid>? ScopedStudentIdsQuery { get; init; }
}

/// <summary>
/// Projection for a single reconciliation inbox row.
/// Joins FeePaymentReceipt + PaymentMethodRecord + Student.
/// </summary>
public sealed class ReconciliationRow
{
    public Guid ReceiptId { get; init; }
    public string ReceiptNumber { get; init; } = string.Empty;
    public FeeReceiptVerificationStatus VerificationStatus { get; init; }
    public DateOnly ReceiptDate { get; init; }
    public double TotalPaidAmount { get; init; }
    public string? Remarks { get; init; }
    public Guid? LinkedFinalReceiptId { get; init; }
    public string? LinkedFinalReceiptNumber { get; init; }
    public Guid FeeCollectionTransactionId { get; init; }

    // Payment instrument
    public PaymentMethodEnum PaymentMethod { get; init; }
    public string? PaymentDetailsJson { get; init; }

    // Student
    public Guid StudentId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public int? GrNumber { get; init; }
    public int? RollNumber { get; init; }

    // Denormalized lookups
    public string Board { get; init; } = string.Empty;
    public string Medium { get; init; } = string.Empty;
    public string Class { get; init; } = string.Empty;
    public string Division { get; init; } = string.Empty;
}

/// <summary>One row from the tab-count grouped query.</summary>
public sealed class ReconciliationStatusCount
{
    public PaymentMethodEnum PaymentMethod { get; init; }
    public FeeReceiptVerificationStatus VerificationStatus { get; init; }
    public int Count { get; init; }
}
