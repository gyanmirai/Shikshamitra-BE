using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.FeeRefund.Repositories;

/// <summary>
/// Domain repository interface for <see cref="FeeRefundRequest"/>.
/// Extend with domain-query methods to keep LINQ out of application services.
/// </summary>
public interface IFeeRefundRepository : IRepository<FeeRefundRequest, Guid>
{
    /// <summary>
    /// Fetches a page of refund requests with their lines, applying optional filters.
    /// </summary>
    Task<(List<FeeRefundRequest> Items, int TotalCount)> GetListWithDetailsAsync(
        Guid? studentId = null,
        Guid? academicYearId = null,
        Guid? academicMediumId = null,
        Guid? standardId = null,
        Guid? divisionId = null,
        FeeRefundStatus? status = null,
        bool legacyHistory = false,
        int? workflowVersion = null,
        string? search = null,
        int skipCount = 0,
        int maxResultCount = 20,
        string? sorting = null,
        IQueryable<Guid>? scopedStudentIdsQuery = null,
        CancellationToken cancellationToken = default);

    /// <summary>Loads a single request with its <see cref="FeeRefundRequest.Lines"/> collection.</summary>
    Task<FeeRefundRequest?> FindWithLinesAsync(Guid id, CancellationToken cancellationToken = default);

    Task StageNewReservationsAsync(IEnumerable<FeeRefundReservation> reservations,
        CancellationToken cancellationToken = default);

    Task StageNewSettlementAsync(FeeRefundSettlement settlement,
        CancellationToken cancellationToken = default);

    Task<bool> IsPayoutReferenceUsedAsync(string method, string externalReference,
        Guid? excludingRequestId = null, CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, decimal>> GetActiveReservedAmountsAsync(
        IReadOnlyCollection<Guid> dueFeeIds,
        Guid? excludingRequestId = null,
        CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, decimal>> GetCommittedAllocationAmountsAsync(
        IReadOnlyCollection<Guid> allocationIds,
        Guid? excludingRequestId = null,
        CancellationToken cancellationToken = default);

    Task<decimal> GetStudentYearCommittedAmountAsync(Guid studentId, Guid academicYearId,
        Guid? excludingRequestId = null, CancellationToken cancellationToken = default);

    /// <summary>Total count of all refund requests (used for voucher number generation).</summary>
    new Task<long> GetCountAsync(CancellationToken cancellationToken = default);
}
