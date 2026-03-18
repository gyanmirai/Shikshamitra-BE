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
}

