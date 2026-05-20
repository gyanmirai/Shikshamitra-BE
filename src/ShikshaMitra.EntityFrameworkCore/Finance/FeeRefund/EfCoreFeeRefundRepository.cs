using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.FeeRefund;
using ShikshaMitra.Students;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Finance.FeeRefund;

public class EfCoreFeeRefundRepository(
    IDbContextProvider<ShikshaMitraDbContext> dbContextProvider
) : EfCoreRepository<ShikshaMitraDbContext, FeeRefundRequest, Guid>(dbContextProvider),
    IFeeRefundRepository
{
    public async Task<(List<FeeRefundRequest> Items, int TotalCount)> GetListWithDetailsAsync(
        Guid? studentId = null,
        Guid? academicMediumId = null,
        FeeRefundStatus? status = null,
        string? search = null,
        int skipCount = 0,
        int maxResultCount = 20,
        string? sorting = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var dbSet = dbContext.Set<FeeRefundRequest>();
        var query = dbSet.Include(r => r.Lines).AsQueryable();

        if (studentId.HasValue)
            query = query.Where(r => r.StudentId == studentId.Value);

        if (academicMediumId.HasValue)
        {
            // Resolve the set of student IDs that belong to the requested medium,
            // then restrict the refund list to those students.
            var mediumStudentIds = dbContext.Set<Student>()
                .Where(s => s.AcademicMediumId == academicMediumId.Value)
                .Select(s => s.Id);

            query = query.Where(r => mediumStudentIds.Contains(r.StudentId));
        }

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(r => r.VoucherNumber.ToLower().Contains(term));
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
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<long> GetCountAsync(CancellationToken cancellationToken = default)
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
