using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Finance.ConcessionRequests;

public class EfCoreConcessionRequestRepository(
    IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
    : EfCoreRepository<ShikshaMitraDbContext, ConcessionRequest, Guid>(dbContextProvider),
      IConcessionRequestRepository
{
    public async Task<List<ConcessionRequest>> GetListAsync(
        int skipCount,
        int maxResultCount,
        string? sorting = null,
        Guid? studentId = null,
        Guid? concessionTypeId = null,
        ConcessionRequestStatus? status = null,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        return await dbSet
            .WhereIf(studentId.HasValue, r => r.StudentId == studentId!.Value)
            .WhereIf(concessionTypeId.HasValue, r => r.ConcessionTypeId == concessionTypeId!.Value)
            .WhereIf(status.HasValue, r => r.Status == status!.Value)
            .OrderBy(r => r.CreationTime)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> GetCountAsync(
        Guid? studentId = null,
        Guid? concessionTypeId = null,
        ConcessionRequestStatus? status = null,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        return await dbSet
            .WhereIf(studentId.HasValue, r => r.StudentId == studentId!.Value)
            .WhereIf(concessionTypeId.HasValue, r => r.ConcessionTypeId == concessionTypeId!.Value)
            .WhereIf(status.HasValue, r => r.Status == status!.Value)
            .LongCountAsync(cancellationToken);
    }

    public async Task<ConcessionRequest> GetWithMappingsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        var entity = await dbSet
            .Include(r => r.ConcessionFeeHeadMappings)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return entity ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(
            typeof(ConcessionRequest), id);
    }
}
