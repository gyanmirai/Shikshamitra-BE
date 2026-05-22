using System;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

/// <summary>
/// Application service for the Finance Reconciliation inbox.
/// List operations require <c>Finance.Accounts.Reconciliation.View</c>.
/// Approve / reject require <c>Finance.Accounts.Reconciliation.Add</c>.
/// </summary>
public interface IReconciliationAppService : IApplicationService
{
    /// <summary>
    /// Returns a paged list of provisional fee receipts (reconciliation inbox)
    /// together with tab badge counts for all 7 payment methods.
    /// </summary>
    Task<ReconciliationListResponse> GetListAsync(
        GetReconciliationListInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns badge counts for all 7 payment-method tabs for the given month/year.
    /// Lightweight call for refreshing counts without re-fetching rows.
    /// </summary>
    Task<ReconciliationTabCountsDto> GetTabCountsAsync(
        Guid academicYearId,
        int? month,
        int? year,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a provisional fee receipt.
    /// Creates a final FIN-* receipt, marks the provisional as Verified,
    /// and links the two receipts.
    /// </summary>
    Task<ReconciliationActionResponse> ApproveAsync(
        Guid provisionalReceiptId,
        ApproveReconciliationInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a provisional fee receipt with mandatory remarks.
    /// Marks the receipt as Rejected, reverses all DueFee allocations,
    /// and marks the collection transaction as Reversed.
    /// </summary>
    Task<ReconciliationActionResponse> RejectAsync(
        Guid provisionalReceiptId,
        RejectReconciliationInput input,
        CancellationToken cancellationToken = default);
}
