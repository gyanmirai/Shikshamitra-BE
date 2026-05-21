using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Receipts;
using ShikshaMitra.Students;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Receipts;

public class EfCoreFeePaymentReceiptRepository(
    IDbContextProvider<ShikshaMitraDbContext> dbContextProvider
) : EfCoreRepository<ShikshaMitraDbContext, FeePaymentReceipt, Guid>(dbContextProvider),
    IFeePaymentReceiptRepository
{
    public async Task<(int TotalCount, List<FeePaymentReceipt> Items)> GetPagedListAsync(
        FeeReceiptListFilter filter,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        // ── Build student sub-query for denormalized board/medium match ────────
        var query = dbContext.Set<FeePaymentReceipt>()
            .Where(r => r.AcademicYearId == filter.AcademicYearId
                     && r.BoardId == filter.BoardId
                     && r.EducationMediumId == filter.EducationMediumId);

        // ── Month filter (by receipt generation date) ─────────────────────────
        if (filter.Month.HasValue)
            query = query.Where(r => r.ReceiptDate.Month == filter.Month.Value);

        // ── Receipt kind filter ───────────────────────────────────────────────
        if (filter.ReceiptKind.HasValue)
            query = query.Where(r => r.ReceiptKind == filter.ReceiptKind.Value);

        // ── Free-text search ──────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            var numericGr = int.TryParse(term, out var grNo);

            // Join to students for name/GR search
            var studentQ = dbContext.Set<Student>().AsQueryable();
            if (numericGr)
            {
                var studentIds = studentQ
                    .Where(s => s.GrNo == grNo)
                    .Select(s => s.Id);
                query = query.Where(r =>
                    r.ReceiptNumber.ToLower().Contains(term) ||
                    studentIds.Contains(r.StudentId));
            }
            else
            {
                var studentIds = studentQ
                    .Where(s => (s.FirstName + " " + s.LastName).ToLower().Contains(term)
                             || (s.FirstName + " " + s.MiddleName + " " + s.LastName).ToLower().Contains(term))
                    .Select(s => s.Id);
                query = query.Where(r =>
                    r.ReceiptNumber.ToLower().Contains(term) ||
                    studentIds.Contains(r.StudentId));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        if (totalCount == 0)
            return (0, []);

        var items = await query
            .OrderByDescending(r => r.CreationTime)
            .ThenByDescending(r => r.ReceiptNumber)
            .Skip(filter.SkipCount)
            .Take(filter.MaxResultCount)
            .ToListAsync(cancellationToken);

        return (totalCount, items);
    }

    public async Task<FeePaymentReceipt?> FindByReceiptNumberAsync(
        string receiptNumber,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .FirstOrDefaultAsync(r => r.ReceiptNumber == receiptNumber, cancellationToken);
    }

    public async Task<long> GetCountByKindAsync(
        FeeReceiptKind kind,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet.LongCountAsync(r => r.ReceiptKind == kind, cancellationToken);
    }
}
