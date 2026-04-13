using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.ConcessionRequests;

public interface IConcessionRequestRepository : IRepository<ConcessionRequest, Guid>
{
    /// <summary>
    /// Returns a paged list filtered by optional student and status criteria.
    /// The result includes the <see cref="ConcessionRequest.ConcessionFeeHeadMappings"/> collection.
    /// </summary>
    Task<List<ConcessionRequest>> GetListAsync(
        int skipCount,
        int maxResultCount,
        string? sorting = null,
        Guid? studentId = null,
        Guid? concessionTypeId = null,
        ConcessionRequestStatus? status = null,
        string? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the total count matching the given filter criteria.
    /// </summary>
    Task<long> GetCountAsync(
        Guid? studentId = null,
        Guid? concessionTypeId = null,
        ConcessionRequestStatus? status = null,
        string? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single request including its fee-head mappings.
    /// </summary>
    Task<ConcessionRequest> GetWithMappingsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all Draft requests for a student whose concession type matches any of
    /// <paramref name="concessionTypeIds"/>, including their fee-head mappings.
    /// Used during Step 3 (fee-head selection) to perform a batch replace.
    /// </summary>
    Task<List<ConcessionRequest>> GetDraftRequestsWithMappingsAsync(
        Guid studentId,
        IReadOnlyList<Guid> concessionTypeIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all requests belonging to the same logical batch as the anchor request:
    /// same student, same lifecycle status — with fee-head mappings eagerly loaded.
    /// Used by the Step 3 GET (Edit / Resume) to reconstruct the full fee-head state.
    /// </summary>
    Task<List<ConcessionRequest>> GetBatchWithMappingsAsync(
        Guid studentId,
        ConcessionRequestStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all requests belonging to the same logical batch as the anchor request:
    /// same student, same lifecycle status — with document uploads eagerly loaded.
    /// Used by Step 5 (Document Upload) to perform a full replacement of uploads.
    /// </summary>
    Task<List<ConcessionRequest>> GetBatchWithDocumentsAsync(
        Guid studentId,
        ConcessionRequestStatus status,
        CancellationToken cancellationToken = default);
}

