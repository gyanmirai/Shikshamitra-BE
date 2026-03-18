using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;

namespace ShikshaMitra.Finance.ConcessionRequests;

public class EfCoreConcessionRequestRepository
    : EfCoreRepository<ShikshaMitraDbContext, ConcessionRequest, Guid>,
      IConcessionRequestRepository
{
    public EfCoreConcessionRequestRepository(
        IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

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
        var dbContext = await GetDbContextAsync();

        var query = dbContext.ConcessionRequests
            .Include(x => x.ConcessionFeeHeadMappings)
            .AsNoTracking()
            .WhereIf(studentId.HasValue, x => x.StudentId == studentId!.Value)
            .WhereIf(concessionTypeId.HasValue, x => x.ConcessionTypeId == concessionTypeId!.Value)
            .WhereIf(status.HasValue, x => x.Status == status!.Value);

        // Dynamic sorting (falls back to CreationTime desc)
        query = string.IsNullOrWhiteSpace(sorting)
            ? query.OrderByDescending(x => x.CreationTime)
            : query.OrderBy(sorting);

        return await query
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<long> GetCountAsync(
        Guid? studentId = null,
        Guid? concessionTypeId = null,
        ConcessionRequestStatus? status = null,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        return await dbContext.ConcessionRequests
            .AsNoTracking()
            .WhereIf(studentId.HasValue, x => x.StudentId == studentId!.Value)
            .WhereIf(concessionTypeId.HasValue, x => x.ConcessionTypeId == concessionTypeId!.Value)
            .WhereIf(status.HasValue, x => x.Status == status!.Value)
            .LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<ConcessionRequest> GetWithMappingsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        var entity = await dbContext.ConcessionRequests
            .Include(x => x.ConcessionFeeHeadMappings)
            .FirstOrDefaultAsync(x => x.Id == id, GetCancellationToken(cancellationToken));

        if (entity == null)
        {
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(
                typeof(ConcessionRequest), id);
        }

        return entity;
    }
}

