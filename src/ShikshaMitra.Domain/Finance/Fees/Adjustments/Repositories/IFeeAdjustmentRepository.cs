using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment.Repositories;

public interface IFeeAdjustmentRepository : IRepository<FeeAdjustmentRequest, Guid>
{
    Task<FeeAdjustmentRequest> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FeeAdjustmentRequest?> FindByIdempotencyKeyAsync(
        Guid requestId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, double>> GetPendingSourceAmountsByDueFeeAsync(
        Guid studentId,
        Guid? excludeRequestId = null,
        CancellationToken cancellationToken = default);

    Task<List<FeeAdjustmentRequest>> GetListForApplicationsAsync(
        Guid boardId,
        Guid educationMediumId,
        Guid academicYearId,
        FeeAdjustmentStatus? status,
        bool? completedOnly,
        string? filter,
        Guid? standardId,
        Guid? divisionId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountForApplicationsAsync(
        Guid boardId,
        Guid educationMediumId,
        Guid academicYearId,
        FeeAdjustmentStatus? status,
        bool? completedOnly,
        string? filter,
        Guid? standardId,
        Guid? divisionId,
        CancellationToken cancellationToken = default);

    Task<Dictionary<FeeAdjustmentStatus, int>> GetStatusCountsAsync(
        Guid boardId,
        Guid educationMediumId,
        Guid academicYearId,
        Guid? standardId,
        Guid? divisionId,
        CancellationToken cancellationToken = default);

    Task<int> GetCountForVoucherAsync(CancellationToken cancellationToken = default);
}
