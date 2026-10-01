using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Students;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public class EfCoreFeeRefundRepository(
    IDbContextProvider<ShikshaMitraDbContext> dbContextProvider
) : EfCoreRepository<ShikshaMitraDbContext, FeeRefundRequest, Guid>(dbContextProvider),
    IFeeRefundRepository
{
    public async Task<(List<FeeRefundRequest> Items, int TotalCount)> GetListWithDetailsAsync(
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
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var dbSet = dbContext.Set<FeeRefundRequest>();
        var query = dbSet.Include(r => r.Lines).Include(r => r.Reservations).Include(r => r.Settlements).AsQueryable();

        if (studentId.HasValue)
            query = query.Where(r => r.StudentId == studentId.Value);
        if (academicYearId.HasValue)
            query = query.Where(r => r.AcademicYearId == academicYearId.Value);

        if (academicMediumId.HasValue || standardId.HasValue || divisionId.HasValue)
        {
            var studentQuery = dbContext.Set<Student>().AsQueryable();

            if (academicMediumId.HasValue)
            {
                studentQuery = studentQuery.Where(s => s.AcademicMediumId == academicMediumId.Value);
            }

            if (standardId.HasValue)
            {
                studentQuery = studentQuery.Where(s => s.StandardId == standardId.Value);
            }

            if (divisionId.HasValue)
            {
                studentQuery = studentQuery.Where(s => s.DivisionId == divisionId.Value);
            }

            var filteredStudentIds = studentQuery.Select(s => s.Id);
            query = query.Where(r => filteredStudentIds.Contains(r.StudentId));
        }

        // User scope — restrict to students within the caller's access scope
        if (scopedStudentIdsQuery != null)
            query = query.Where(r => scopedStudentIdsQuery.Contains(r.StudentId));

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);
        if (legacyHistory)
            query = query.Where(r => r.WorkflowVersion == 0);
        else if (workflowVersion.HasValue)
            query = query.Where(r => r.WorkflowVersion == workflowVersion.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            var matchingStudents = dbContext.Set<Student>()
                .Where(s => s.FirstName.ToLower().Contains(term) ||
                    s.LastName.ToLower().Contains(term) ||
                    (s.GrNo != null && s.GrNo.ToString().Contains(term)))
                .Select(s => s.Id);
            query = query.Where(r => r.VoucherNumber.ToLower().Contains(term) ||
                (r.OriginalReceiptNumber != null && r.OriginalReceiptNumber.ToLower().Contains(term)) ||
                matchingStudents.Contains(r.StudentId));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = ApplySorting(query, sorting);

        var items = await query
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<FeeRefundRequest?> FindWithLinesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .Include(r => r.Lines)
            .Include(r => r.Reservations)
            .Include(r => r.Settlements)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task StageNewReservationsAsync(IEnumerable<FeeRefundReservation> reservations,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        foreach (var reservation in reservations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            dbContext.Entry(reservation).State = EntityState.Added;
        }
    }

    public async Task StageNewSettlementAsync(FeeRefundSettlement settlement,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dbContext = await GetDbContextAsync();
        dbContext.Entry(settlement).State = EntityState.Added;
    }

    public async Task<bool> IsPayoutReferenceUsedAsync(string method, string externalReference,
        Guid? excludingRequestId = null, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet.Where(r => !excludingRequestId.HasValue || r.Id != excludingRequestId.Value)
            .SelectMany(r => r.Settlements)
            .AnyAsync(s => s.Method == method && s.ExternalReference == externalReference &&
                s.Status != FeeRefundSettlementStatus.Failed, cancellationToken);
    }

    public async Task<Dictionary<Guid, decimal>> GetActiveReservedAmountsAsync(
        IReadOnlyCollection<Guid> dueFeeIds,
        Guid? excludingRequestId = null,
        CancellationToken cancellationToken = default)
    {
        if (dueFeeIds.Count == 0) return [];
        var dbSet = await GetDbSetAsync();
        var query = dbSet.Where(r => !excludingRequestId.HasValue || r.Id != excludingRequestId.Value)
            .SelectMany(r => r.Reservations)
            .Where(r => dueFeeIds.Contains(r.DueFeeId) && r.Status == FeeRefundReservationStatus.Active);
        return await query.GroupBy(r => r.DueFeeId)
            .Select(g => new { DueFeeId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.DueFeeId, x => x.Amount, cancellationToken);
    }

    public async Task<Dictionary<Guid, decimal>> GetCommittedAllocationAmountsAsync(
        IReadOnlyCollection<Guid> allocationIds, Guid? excludingRequestId = null,
        CancellationToken cancellationToken = default)
    {
        if (allocationIds.Count == 0) return [];
        var dbSet = await GetDbSetAsync();
        var requests = dbSet.Where(r => !excludingRequestId.HasValue || r.Id != excludingRequestId.Value);
        var processed = await requests.SelectMany(r => r.Lines)
            .Where(x => x.SourceAllocationId.HasValue && allocationIds.Contains(x.SourceAllocationId.Value))
            .GroupBy(x => x.SourceAllocationId!.Value)
            .Select(g => new { g.Key, Amount = g.Sum(x => x.ProcessedAmount - x.ReversedAmount) })
            .ToDictionaryAsync(x => x.Key, x => x.Amount, cancellationToken);
        var reserved = await requests.SelectMany(r => r.Reservations)
            .Where(x => x.SourceAllocationId.HasValue && allocationIds.Contains(x.SourceAllocationId.Value) && x.Status == FeeRefundReservationStatus.Active)
            .GroupBy(x => x.SourceAllocationId!.Value)
            .Select(g => new { g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Amount, cancellationToken);
        foreach (var item in reserved) processed[item.Key] = processed.GetValueOrDefault(item.Key) + item.Value;
        return processed;
    }

    public async Task<decimal> GetStudentYearCommittedAmountAsync(Guid studentId, Guid academicYearId,
        Guid? excludingRequestId = null, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        var requests = dbSet.Where(r => r.StudentId == studentId && r.AcademicYearId == academicYearId &&
            (!excludingRequestId.HasValue || r.Id != excludingRequestId.Value));
        var processed = await requests.SumAsync(r => r.ProcessedAmount - r.ReversedAmount, cancellationToken);
        var activeReserved = await requests.SelectMany(r => r.Reservations)
            .Where(x => x.Status == FeeRefundReservationStatus.Active)
            .SumAsync(x => x.Amount, cancellationToken);
        return processed + activeReserved;
    }

    public override async Task<long> GetCountAsync(CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet.LongCountAsync(cancellationToken);
    }

    private static IQueryable<FeeRefundRequest> ApplySorting(
        IQueryable<FeeRefundRequest> query,
        string? sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting))
            return query.OrderByDescending(r => r.CreationTime);

        return sorting.ToLower().Trim() switch
        {
            "createdat asc"  or "creationtime asc"  => query.OrderBy(r => r.CreationTime),
            "createdat desc" or "creationtime desc" => query.OrderByDescending(r => r.CreationTime),
            "totalamount asc"  => query.OrderBy(r => r.TotalAmount),
            "totalamount desc" => query.OrderByDescending(r => r.TotalAmount),
            "status asc"  => query.OrderBy(r => r.Status),
            "status desc" => query.OrderByDescending(r => r.Status),
            _ => query.OrderByDescending(r => r.CreationTime),
        };
    }
}
