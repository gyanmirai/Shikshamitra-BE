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
        Guid? academicMediumId = null,
        Guid? standardId = null,
        Guid? divisionId = null,
        FeeRefundStatus? status = null,
        string? search = null,
        int skipCount = 0,
        int maxResultCount = 20,
        string? sorting = null,
        IQueryable<Guid>? scopedStudentIdsQuery = null,
        CancellationToken cancellationToken = default);

    /// <summary>Loads a single request with its <see cref="FeeRefundRequest.Lines"/> collection.</summary>
    Task<FeeRefundRequest?> FindWithLinesAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Total count of all refund requests (used for voucher number generation).</summary>
    new Task<long> GetCountAsync(CancellationToken cancellationToken = default);
}
