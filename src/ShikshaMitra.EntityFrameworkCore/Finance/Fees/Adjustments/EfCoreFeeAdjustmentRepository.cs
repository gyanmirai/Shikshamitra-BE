using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Repositories;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

public class EfCoreFeeAdjustmentRepository(
    IDbContextProvider<ShikshaMitraDbContext> dbContextProvider,
    ICurrentTenant currentTenant)
    : EfCoreRepository<ShikshaMitraDbContext, FeeAdjustmentRequest, Guid>(dbContextProvider),
        IFeeAdjustmentRepository
{
    // Dedicated kind sentinel for adjustment voucher sequences in the shared sequence table.
    // Must not collide with FeeReceiptKind values (0=FinalFee, 1=ProvisionalFee, 2=ProvisionalCharge).
    private const int AdjustmentVoucherKind = 100;
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

    /// <inheritdoc />
    public async Task<long> IncrementAndGetVoucherSequenceAsync(int year, CancellationToken cancellationToken = default)
    {
        var storedTenantId = currentTenant.Id ?? ReceiptNumberSequence.HostTenantSentinel;

        for (var retry = 0; retry < 10; retry++)
        {
            try
            {
                var dbContext = await GetDbContextAsync();
                var sequences = dbContext.Set<ReceiptNumberSequence>();

                var sequence = await sequences.FirstOrDefaultAsync(
                    s => s.StoredTenantId == storedTenantId &&
                         s.Year == year &&
                         s.StoredKind == AdjustmentVoucherKind,
                    cancellationToken);

                if (sequence == null)
                {
                    sequence = ReceiptNumberSequence.New(Guid.NewGuid(), currentTenant.Id, year, kind: null);
                    // Overwrite the stored kind with the adjustment voucher sentinel
                    // by creating a raw instance — ReceiptNumberSequence.New sets Kind=null (StoredKind=-1)
                    // so we need a different approach: use reflection-free workaround via direct SQL.
                    // Instead: create via raw upsert so we control StoredKind=AdjustmentVoucherKind.
                    await dbContext.Database.ExecuteSqlRawAsync(
                        @"INSERT INTO ""Finance_FinanceReceiptNumberSequences"" (""Id"", ""TenantId"", ""Year"", ""Kind"", ""NextValue"")
                          VALUES ({0}, {1}, {2}, {3}, 2)
                          ON CONFLICT (""TenantId"", ""Year"", ""Kind"") DO NOTHING",
                        Guid.NewGuid(), storedTenantId, year, AdjustmentVoucherKind);

                    sequence = await sequences.FirstOrDefaultAsync(
                        s => s.StoredTenantId == storedTenantId &&
                             s.Year == year &&
                             s.StoredKind == AdjustmentVoucherKind,
                        cancellationToken);
                }

                var returnValue = sequence!.NextValue;
                sequence.Increment();
                await dbContext.SaveChangesAsync(cancellationToken);
                return returnValue;
            }
            catch (Exception ex) when (ex is DbUpdateConcurrencyException or DbUpdateException)
            {
                var dbContext = await GetDbContextAsync();
                dbContext.ChangeTracker.Clear();
                if (retry == 9) throw;
                await Task.Delay(5, cancellationToken);
            }
        }

        throw new InvalidOperationException("Failed to generate adjustment voucher sequence due to high concurrency.");
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
