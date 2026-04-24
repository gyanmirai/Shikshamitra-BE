using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Finance.Fees.Concessions;

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

    public async Task<List<ConcessionRequest>> GetDraftRequestsWithMappingsAsync(
        Guid studentId,
        IReadOnlyList<Guid> concessionTypeIds,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        return await dbSet
            .Include(r => r.ConcessionFeeHeadMappings)
            .Where(r => r.StudentId == studentId
                     && r.Status == ConcessionRequestStatus.Draft
                     && concessionTypeIds.Contains(r.ConcessionTypeId))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ConcessionRequest>> GetBatchWithMappingsAsync(
        Guid studentId,
        ConcessionRequestStatus status,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        return await dbSet
            .Include(r => r.ConcessionFeeHeadMappings)
            .Where(r => r.StudentId == studentId && r.Status == status)
            .OrderBy(r => r.CreationTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ConcessionRequest>> GetBatchWithDocumentsAsync(
        Guid studentId,
        ConcessionRequestStatus status,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        return await dbSet
            .Include(r => r.ConcessionRequestDocuments)
            .Where(r => r.StudentId == studentId && r.Status == status)
            .OrderBy(r => r.CreationTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ConcessionRequest>> GetStudentRequestsWithDetailsAsync(
        Guid studentId,
        int skipCount,
        int maxResultCount,
        ConcessionRequestStatus? status = null,
        IReadOnlyList<Guid>? concessionTypeIds = null,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        return await dbSet
            .Include(r => r.ConcessionFeeHeadMappings)
            .Include(r => r.ConcessionRequestDocuments)
            .Where(r => r.StudentId == studentId)
            .WhereIf(status.HasValue, r => r.Status == status!.Value)
            .WhereIf(
                concessionTypeIds != null && concessionTypeIds.Count > 0,
                r => concessionTypeIds!.Contains(r.ConcessionTypeId))
            .OrderByDescending(r => r.CreationTime)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> GetStudentRequestsCountAsync(
        Guid studentId,
        ConcessionRequestStatus? status = null,
        IReadOnlyList<Guid>? concessionTypeIds = null,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        return await dbSet
            .Where(r => r.StudentId == studentId)
            .WhereIf(status.HasValue, r => r.Status == status!.Value)
            .WhereIf(
                concessionTypeIds != null && concessionTypeIds.Count > 0,
                r => concessionTypeIds!.Contains(r.ConcessionTypeId))
            .LongCountAsync(cancellationToken);
    }

    public async Task<ConcessionRequest> GetWithDocumentsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        var entity = await dbSet
            .Include(r => r.ConcessionRequestDocuments)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return entity ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(
            typeof(ConcessionRequest), id);
    }
}
