using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Receipts;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Receipts;

public class EfCoreFeePaymentReceiptRepository(
    IDbContextProvider<ShikshaMitraDbContext> dbContextProvider
) : EfCoreRepository<ShikshaMitraDbContext, FeePaymentReceipt, Guid>(dbContextProvider),
    IFeePaymentReceiptRepository
{
    private static readonly JsonSerializerOptions PaymentDetailsJsonOptions =
        new(JsonSerializerDefaults.Web);

    // ── Reconciliation queries ────────────────────────────────────────────────

    public async Task<(int TotalCount, List<Guid> Ids)> GetReconciliationPagedIdsAsync(
        ReconciliationListFilter filter,
        CancellationToken cancellationToken = default)
    {
        var db = await GetDbContextAsync();

        var query = BuildReconciliationBaseQuery(db, filter);

        var totalCount = await query.CountAsync(cancellationToken);
        if (totalCount == 0)
            return (0, []);

        var ids = await query
            .OrderByDescending(r => r.CreationTime)
            .ThenByDescending(r => r.ReceiptNumber)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        return (totalCount, ids);
    }

    public async Task<List<ReconciliationRow>> GetReconciliationRowsByIdsAsync(
        List<Guid> receiptIds,
        CancellationToken cancellationToken = default)
    {
        if (receiptIds.Count == 0)
            return [];

        var db = await GetDbContextAsync();

        // Phase 1 — receipt + student projection (no PaymentDetails; jsonb converter
        // cannot be read as string inside an EF-translated Select).
        var rawRows = await (
            from r in db.Set<FeePaymentReceipt>()
            where receiptIds.Contains(r.Id)
            join s in db.Set<Student>() on r.StudentId equals s.Id into sj
            from s in sj.DefaultIfEmpty()
            join b in db.Set<EducationBoard>() on s.AcademicBoardId equals (Guid?)b.Id into bj
            from b in bj.DefaultIfEmpty()
            join m in db.Set<EducationMedium>() on s.AcademicMediumId equals m.Id into mj
            from m in mj.DefaultIfEmpty()
            join st in db.Set<Standard>() on s.StandardId equals (Guid?)st.Id into stj
            from st in stj.DefaultIfEmpty()
            join d in db.Set<Division>() on s.DivisionId equals (Guid?)d.Id into dj
            from d in dj.DefaultIfEmpty()
            let finalReceipt = r.LinkedFinalReceiptId.HasValue
                ? db.Set<FeePaymentReceipt>()
                      .Where(f => f.Id == r.LinkedFinalReceiptId)
                      .Select(f => f.ReceiptNumber)
                      .FirstOrDefault()
                : null
            select new
            {
                ReceiptId                  = r.Id,
                ReceiptNumber              = r.ReceiptNumber,
                VerificationStatus         = r.VerificationStatus,
                ReceiptDate                = r.ReceiptDate,
                TotalPaidAmount            = r.TotalPaidAmount,
                Remarks                    = r.Remarks,
                LinkedFinalReceiptId       = r.LinkedFinalReceiptId,
                LinkedFinalReceiptNumber   = finalReceipt,
                FeeCollectionTransactionId = r.FeeCollectionTransactionId,
                StudentId                  = r.StudentId,
                StudentName                = s != null
                    ? (s.FirstName + " " + (s.MiddleName ?? "") + " " + s.LastName).Trim().Replace("  ", " ")
                    : string.Empty,
                GrNumber                   = s != null ? s.GrNo : null,
                RollNumber                 = s != null ? s.RollNo : null,
                Board                      = b != null ? b.Type.ToString() : string.Empty,
                Medium                     = m != null ? m.Name : string.Empty,
                Class                      = st != null ? st.Name : string.Empty,
                Division                   = d != null ? d.Name : string.Empty,
            }
        ).ToListAsync(cancellationToken);

        // Phase 2 — hydrate payment instrument details in memory.
        var txIds = rawRows.Select(x => x.FeeCollectionTransactionId).Distinct().ToList();

        var paymentRecords = await db.Set<PaymentMethodRecord>()
            .Where(p => txIds.Contains(p.FeeRecordMetadataId)
                     && p.PaymentMethod != PaymentMethodEnum.Cash)
            .OrderBy(p => p.CreationTime)
            .ToListAsync(cancellationToken);

        var paymentByTx = paymentRecords
            .GroupBy(p => p.FeeRecordMetadataId)
            .ToDictionary(g => g.Key, g => g.First());

        var rowById = rawRows.ToDictionary(
            x => x.ReceiptId,
            x =>
            {
                paymentByTx.TryGetValue(x.FeeCollectionTransactionId, out var pmr);
                return new ReconciliationRow
                {
                    ReceiptId                  = x.ReceiptId,
                    ReceiptNumber              = x.ReceiptNumber,
                    VerificationStatus         = x.VerificationStatus,
                    ReceiptDate                = x.ReceiptDate,
                    TotalPaidAmount            = x.TotalPaidAmount,
                    Remarks                    = x.Remarks,
                    LinkedFinalReceiptId       = x.LinkedFinalReceiptId,
                    LinkedFinalReceiptNumber   = x.LinkedFinalReceiptNumber,
                    FeeCollectionTransactionId = x.FeeCollectionTransactionId,
                    PaymentMethod              = pmr?.PaymentMethod ?? PaymentMethodEnum.Cash,
                    PaymentDetailsJson         = pmr?.PaymentDetails is null
                        ? null
                        : JsonSerializer.Serialize(pmr.PaymentDetails, PaymentDetailsJsonOptions),
                    StudentId                  = x.StudentId,
                    StudentName                = x.StudentName,
                    GrNumber                   = x.GrNumber,
                    RollNumber                 = x.RollNumber,
                    Board                      = x.Board,
                    Medium                     = x.Medium,
                    Class                      = x.Class,
                    Division                   = x.Division,
                };
            });

        // Preserve the paging order from phase-1 ID query.
        return receiptIds
            .Where(rowById.ContainsKey)
            .Select(id => rowById[id])
            .ToList();
    }

    public async Task<List<ReconciliationStatusCount>> GetReconciliationStatusCountsAsync(
        Guid academicYearId,
        int? month,
        int? year,
        IQueryable<Guid>? scopedStudentIdsQuery = null,
        CancellationToken cancellationToken = default)
    {
        var db = await GetDbContextAsync();

        // One grouped query: join receipts to payment method records, group by method + status
        var receiptQ = db.Set<FeePaymentReceipt>()
            .Where(r => r.AcademicYearId == academicYearId
                     && r.ReceiptKind == FeeReceiptKind.ProvisionalFee
                     && r.ReceiptScope == FeeReceiptScope.Fee);

        // User scope — only count receipts for students accessible to the caller
        if (scopedStudentIdsQuery != null)
            receiptQ = receiptQ.Where(r => scopedStudentIdsQuery.Contains(r.StudentId));

        var q = from r in receiptQ
                join pmr in db.Set<PaymentMethodRecord>()
                    on r.FeeCollectionTransactionId equals pmr.FeeRecordMetadataId
                where pmr.PaymentMethod != PaymentMethodEnum.Cash
                select new { r.VerificationStatus, pmr.PaymentMethod, r.ReceiptDate };

        if (month.HasValue)
            q = q.Where(x => x.ReceiptDate.Month == month.Value);

        if (year.HasValue)
            q = q.Where(x => x.ReceiptDate.Year == year.Value);

        var grouped = await q
            .GroupBy(x => new { x.PaymentMethod, x.VerificationStatus })
            .Select(g => new ReconciliationStatusCount
            {
                PaymentMethod      = g.Key.PaymentMethod,
                VerificationStatus = g.Key.VerificationStatus,
                Count              = g.Count(),
            })
            .ToListAsync(cancellationToken);

        return grouped;
    }

    private static IQueryable<FeePaymentReceipt> BuildReconciliationBaseQuery(
        ShikshaMitraDbContext db,
        ReconciliationListFilter filter)
    {
        var query = db.Set<FeePaymentReceipt>()
            .Where(r => r.AcademicYearId == filter.AcademicYearId
                     && r.ReceiptKind == FeeReceiptKind.ProvisionalFee
                     && r.ReceiptScope == FeeReceiptScope.Fee);

        if (filter.Month.HasValue)
            query = query.Where(r => r.ReceiptDate.Month == filter.Month.Value);

        if (filter.Year.HasValue)
            query = query.Where(r => r.ReceiptDate.Year == filter.Year.Value);

        if (filter.VerificationStatus.HasValue)
            query = query.Where(r => r.VerificationStatus == filter.VerificationStatus.Value);

        // Payment method tab filter — check that the transaction has a matching PaymentMethodRecord
        if (filter.PaymentMethod.HasValue)
        {
            var pm = filter.PaymentMethod.Value;
            var txIds = db.Set<PaymentMethodRecord>()
                .Where(p => p.PaymentMethod == pm)
                .Select(p => p.FeeRecordMetadataId);
            query = query.Where(r => txIds.Contains(r.FeeCollectionTransactionId));
        }

        // Free-text search
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            if (int.TryParse(term, out var grNo))
            {
                var studentIds = db.Set<Student>()
                    .Where(s => s.GrNo == grNo)
                    .Select(s => s.Id);
                query = query.Where(r =>
                    r.ReceiptNumber.ToLower().Contains(term) ||
                    studentIds.Contains(r.StudentId));
            }
            else
            {
                var studentIds = db.Set<Student>()
                    .Where(s => (s.FirstName + " " + s.LastName).ToLower().Contains(term)
                             || (s.FirstName + " " + s.MiddleName + " " + s.LastName).ToLower().Contains(term))
                    .Select(s => s.Id);
                query = query.Where(r =>
                    r.ReceiptNumber.ToLower().Contains(term) ||
                    studentIds.Contains(r.StudentId));
            }
        }

        // User scope — only receipts whose student is within the caller's access scope
        if (filter.ScopedStudentIdsQuery != null)
            query = query.Where(r => filter.ScopedStudentIdsQuery.Contains(r.StudentId));

        return query;
    }


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

        // ── Class / division filter (via student) ─────────────────────────────
        if (filter.StandardId.HasValue || filter.DivisionId.HasValue)
        {
            var studentQ = dbContext.Set<Student>().AsQueryable();

            if (filter.StandardId.HasValue)
                studentQ = studentQ.Where(s => s.StandardId == filter.StandardId.Value);

            if (filter.DivisionId.HasValue)
                studentQ = studentQ.Where(s => s.DivisionId == filter.DivisionId.Value);

            var studentIds = studentQ.Select(s => s.Id);
            query = query.Where(r => studentIds.Contains(r.StudentId));
        }

        // ── User scope filter ─────────────────────────────────────────────────
        if (filter.ScopedStudentIdsQuery != null)
            query = query.Where(r => filter.ScopedStudentIdsQuery.Contains(r.StudentId));

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
