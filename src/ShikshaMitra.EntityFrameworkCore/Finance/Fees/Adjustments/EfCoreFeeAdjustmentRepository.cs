using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Repositories;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

public class EfCoreFeeAdjustmentRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
    : EfCoreRepository<ShikshaMitraDbContext, FeeAdjustmentRequest, Guid>(dbContextProvider),
        IFeeAdjustmentRepository
{
    public async Task<FeeAdjustmentRequest> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        var entity = await queryable.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(FeeAdjustmentRequest), id);
        }

        return entity;
    }

    public async Task<FeeAdjustmentRequest?> FindByIdempotencyKeyAsync(
        Guid requestId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await queryable.FirstOrDefaultAsync(
            x => x.Id == requestId && x.LastIdempotencyKey == idempotencyKey,
            cancellationToken);
    }

    public async Task<Dictionary<Guid, double>> GetPendingSourceAmountsByDueFeeAsync(
        Guid studentId,
        Guid? excludeRequestId = null,
        CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        var pending = await queryable
            .Where(x => x.StudentId == studentId
                && (x.Status == FeeAdjustmentStatus.Pending || x.Status == FeeAdjustmentStatus.PartiallyApplied)
                && (excludeRequestId == null || x.Id != excludeRequestId.Value))
            .SelectMany(x => x.SourceLines)
            .GroupBy(l => l.DueFeeId)
            .Select(g => new { DueFeeId = g.Key, Amount = g.Sum(l => l.Amount) })
            .ToListAsync(cancellationToken);

        return pending.ToDictionary(x => x.DueFeeId, x => x.Amount);
    }

    public async Task<List<FeeAdjustmentRequest>> GetListForApplicationsAsync(
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
        CancellationToken cancellationToken = default)
    {
        var query = await BuildApplicationsQueryAsync(
            boardId, educationMediumId, academicYearId, status, completedOnly, filter, standardId, divisionId, cancellationToken);
        return await query
            .OrderByDescending(x => x.CreationTime)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountForApplicationsAsync(
        Guid boardId,
        Guid educationMediumId,
        Guid academicYearId,
        FeeAdjustmentStatus? status,
        bool? completedOnly,
        string? filter,
        Guid? standardId,
        Guid? divisionId,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildApplicationsQueryAsync(
            boardId, educationMediumId, academicYearId, status, completedOnly, filter, standardId, divisionId, cancellationToken);
        return await query.CountAsync(cancellationToken);
    }

    public async Task<Dictionary<FeeAdjustmentStatus, int>> GetStatusCountsAsync(
        Guid boardId,
        Guid educationMediumId,
        Guid academicYearId,
        Guid? standardId,
        Guid? divisionId,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildApplicationsQueryAsync(
            boardId, educationMediumId, academicYearId, status: null, completedOnly: null, filter: null, standardId, divisionId, cancellationToken);

        return await query
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
    }

    public async Task<int> GetCountForVoucherAsync(CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await queryable.CountAsync(cancellationToken);
    }

    private async Task<IQueryable<FeeAdjustmentRequest>> BuildApplicationsQueryAsync(
        Guid boardId,
        Guid educationMediumId,
        Guid academicYearId,
        FeeAdjustmentStatus? status,
        bool? completedOnly,
        string? filter,
        Guid? standardId,
        Guid? divisionId,
        CancellationToken cancellationToken)
    {
        var queryable = await GetQueryableAsync();
        var query = queryable.Where(x =>
            x.BoardId == boardId
            && x.EducationMediumId == educationMediumId
            && x.AcademicYearId == academicYearId);

        if (standardId.HasValue)
        {
            query = query.Where(x => x.StandardId == standardId.Value);
        }

        if (divisionId.HasValue)
        {
            query = query.Where(x => x.DivisionId == divisionId.Value);
        }

        if (completedOnly == true)
        {
            query = query.Where(x =>
                x.Status == FeeAdjustmentStatus.PartiallyApplied
                || x.Status == FeeAdjustmentStatus.Applied);
        }
        else if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(x => x.VoucherNumber.ToLower().Contains(f) || x.Reason.ToLower().Contains(f));
        }

        return query;
    }
}
