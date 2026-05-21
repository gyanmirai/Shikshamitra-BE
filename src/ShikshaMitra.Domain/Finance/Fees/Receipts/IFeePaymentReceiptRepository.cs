using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Receipts;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Receipts;

/// <summary>
/// Custom repository for <see cref="FeePaymentReceipt"/> that provides
/// optimised paged queries against the receipts table.
/// </summary>
public interface IFeePaymentReceiptRepository : IRepository<FeePaymentReceipt, Guid>
{
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

    /// <summary>Month (1–12) derived from <see cref="FeePaymentReceipt.ReceiptDate"/>. Null = all months.</summary>
    public int? Month { get; init; }

    /// <summary>Optional kind filter (null = all kinds).</summary>
    public FeeReceiptKind? ReceiptKind { get; init; }

    /// <summary>Free-text: matches receipt number, student name, or GR number.</summary>
    public string? Search { get; init; }

    public int SkipCount { get; init; }
    public int MaxResultCount { get; init; } = 20;
}
