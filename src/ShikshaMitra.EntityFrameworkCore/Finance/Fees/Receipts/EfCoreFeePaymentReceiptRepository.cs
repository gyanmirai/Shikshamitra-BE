using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Receipts;

public partial class EfCoreFeePaymentReceiptRepository(
    IDbContextProvider<ShikshaMitraDbContext> dbContextProvider
) : EfCoreRepository<ShikshaMitraDbContext, FeePaymentReceipt, Guid>(dbContextProvider),
    IFeePaymentReceiptRepository
{
    private static readonly IReadOnlyDictionary<string, LambdaExpression> ReceiptFilterFields =
        new Dictionary<string, LambdaExpression>
        {
            ["receiptNumber"] = (Expression<Func<FeePaymentReceipt, string>>)(r => r.ReceiptNumber),
            ["totalPaidAmount"] = (Expression<Func<FeePaymentReceipt, double>>)(r => r.TotalPaidAmount),
            ["receiptDate"] = (Expression<Func<FeePaymentReceipt, DateOnly>>)(r => r.ReceiptDate)
        };
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
            .ThenByDescending(r => r.Id)
            .Skip((int)Math.Min((long)(filter.Page - 1) * filter.PageSize, int.MaxValue))
            .Take(filter.PageSize)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        return (totalCount, ids);
    }

    public Task<List<ReconciliationRow>> GetReconciliationRowsByIdsAsync(
        List<Guid> receiptIds,
        CancellationToken cancellationToken = default) =>
        ReadReconciliationRowsAsync(receiptIds, true, cancellationToken);

    private async Task<List<ReconciliationRow>> ReadReconciliationRowsAsync(
        List<Guid> receiptIds, bool includeDetails, CancellationToken cancellationToken)
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
            join a in db.Set<FinanceFeeAccount>() on r.FinanceFeeAccountId equals (Guid?)a.Id into aj
            from a in aj.DefaultIfEmpty()
            join b in db.Set<EducationBoard>() on (a != null ? (Guid?)a.BoardId : s.AcademicBoardId) equals (Guid?)b.Id into bj
            from b in bj.DefaultIfEmpty()
            join m in db.Set<EducationMedium>() on (a != null ? a.EducationMediumId : s.AcademicMediumId) equals m.Id into mj
            from m in mj.DefaultIfEmpty()
            join st in db.Set<Standard>() on (a != null ? (Guid?)a.StandardId : s.StandardId) equals (Guid?)st.Id into stj
            from st in stj.DefaultIfEmpty()
            join d in db.Set<Division>() on (a != null ? a.DivisionId : s.DivisionId) equals (Guid?)d.Id into dj
            from d in dj.DefaultIfEmpty()
            let finalReceipt = includeDetails && r.LinkedFinalReceiptId.HasValue
                ? db.Set<FeePaymentReceipt>()
                      .Where(f => f.Id == r.LinkedFinalReceiptId)
                      .Select(f => f.ReceiptNumber)
                      .FirstOrDefault()
                : null
            select new
            {
                ReceiptScope = r.ReceiptScope,
                PaymentReceiptCount = includeDetails ? db.Set<FeePaymentReceipt>().Count(sibling => sibling.FeeCollectionTransactionId == r.FeeCollectionTransactionId && sibling.LinkedProvisionalReceiptId == null) : 1,
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
                FinanceFeeAccountId        = r.FinanceFeeAccountId,
                StudentName                = a != null ? a.ApplicantName : s != null
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
            .Where(p => txIds.Contains(p.FeeRecordMetadataId))
            .OrderBy(p => p.PaymentMethod == PaymentMethodEnum.Cash)
            .ThenBy(p => p.CreationTime)
            .ThenBy(p => p.Id)
            .Select(p => new { p.FeeRecordMetadataId, p.PaymentMethod, p.PaymentDate, p.BankAccountId, p.BankLabelSnapshot, p.PaymentDetails })
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
                    ReceiptScope = x.ReceiptScope, PaymentReceiptCount = x.PaymentReceiptCount,
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
                    PaymentDate                = pmr?.PaymentDate,
                    BankAccountId              = pmr?.BankAccountId,
                    BankLabel                  = pmr?.BankLabelSnapshot,
                    PaymentDetailsJson         = pmr?.PaymentDetails is null
                        ? null
                        : JsonSerializer.Serialize(pmr.PaymentDetails, PaymentDetailsJsonOptions),
                    StudentId                  = x.StudentId,
                    FinanceFeeAccountId        = x.FinanceFeeAccountId,
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
        CancellationToken cancellationToken = default,
        IQueryable<Guid>? scopedAccountIdsQuery = null)
    {
        var db = await GetDbContextAsync();

        // One grouped query: join receipts to payment method records, group by method + status
        var receiptQ = db.Set<FeePaymentReceipt>()
            .Where(r => r.AcademicYearId == academicYearId
                     && (r.ReceiptKind == FeeReceiptKind.ProvisionalFee
                         || r.ReceiptKind == FeeReceiptKind.ProvisionalCharge
                         || ((r.ReceiptKind == FeeReceiptKind.FinalFee || r.ReceiptKind == FeeReceiptKind.FinalCharge)
                             && db.Set<PaymentMethodRecord>().Any(p => p.FeeRecordMetadataId == r.FeeCollectionTransactionId)
                             && !db.Set<PaymentMethodRecord>().Any(p => p.FeeRecordMetadataId == r.FeeCollectionTransactionId && p.PaymentMethod != PaymentMethodEnum.Cash))));

        // User scope — only count receipts for students accessible to the caller
        receiptQ = ApplyPayerScope(receiptQ, scopedStudentIdsQuery, scopedAccountIdsQuery);

        var q = from r in receiptQ
                join pmr in db.Set<PaymentMethodRecord>()
                    on r.FeeCollectionTransactionId equals pmr.FeeRecordMetadataId
                where (pmr.PaymentMethod != PaymentMethodEnum.Cash &&
                    (r.ReceiptKind == FeeReceiptKind.ProvisionalFee || r.ReceiptKind == FeeReceiptKind.ProvisionalCharge)) ||
                    (pmr.PaymentMethod == PaymentMethodEnum.Cash &&
                    (r.ReceiptKind == FeeReceiptKind.FinalFee || r.ReceiptKind == FeeReceiptKind.FinalCharge))
                select new { r.Id, r.VerificationStatus, pmr.PaymentMethod, r.ReceiptDate };

        if (month.HasValue)
            q = q.Where(x => x.ReceiptDate.Month == month.Value);

        if (year.HasValue)
            q = q.Where(x => x.ReceiptDate.Year == year.Value);

        var grouped = await q.Distinct()
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
                     && (filter.PaymentMethod == PaymentMethodEnum.Cash
                         ? (r.ReceiptKind == FeeReceiptKind.FinalFee || r.ReceiptKind == FeeReceiptKind.FinalCharge) &&
                           !db.Set<PaymentMethodRecord>().Any(p => p.FeeRecordMetadataId == r.FeeCollectionTransactionId && p.PaymentMethod != PaymentMethodEnum.Cash)
                         : (r.ReceiptKind == FeeReceiptKind.ProvisionalFee || r.ReceiptKind == FeeReceiptKind.ProvisionalCharge)));

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

        if (filter.TableQuery != null)
        {
            query = ApplyReconciliationScalarHeaders(query, filter.TableQuery, filter.PaymentMethod);
            query = ApplyReconciliationContextHeaders(query, db, filter.TableQuery);
        }
        query = ApplySearch(query, db, filter.Search);
        return ApplyPayerScope(query, filter.ScopedStudentIdsQuery, filter.ScopedAccountIdsQuery);
    }


    public async Task<(int TotalCount, List<FeePaymentReceipt> Items)> GetPagedListAsync(
        FeeReceiptListFilter filter,
        CancellationToken cancellationToken = default,
        bool includeTotalCount = true)
    {
        var dbContext = await GetDbContextAsync();

        var query = BuildReceiptListQuery(dbContext, filter);

        var totalCount = includeTotalCount ? await query.CountAsync(cancellationToken) : -1;
        if (includeTotalCount && totalCount == 0)
            return (0, []);

        var items = await query
            .OrderByDescending(r => r.CreationTime)
            .ThenByDescending(r => r.ReceiptNumber)
            .Skip(filter.SkipCount)
            .Take(filter.MaxResultCount)
            .ToListAsync(cancellationToken);

        return (totalCount, items);
    }

    public async Task<List<FeeReceiptListFilterRow>> GetListFilterRowsAsync(
        FeeReceiptListFilter filter,
        bool includePaymentMethods,
        CancellationToken cancellationToken = default)
    {
        var db = await GetDbContextAsync();
        var query = BuildReceiptListQuery(db, filter);
        var rawRows = await (
            from receipt in query
            join student in db.Set<Student>().AsNoTracking() on receipt.StudentId equals (Guid?)student.Id into students
            from student in students.DefaultIfEmpty()
            join account in db.Set<FinanceFeeAccount>().AsNoTracking() on receipt.FinanceFeeAccountId equals (Guid?)account.Id into accounts
            from account in accounts.DefaultIfEmpty()
            join linked in db.Set<FeePaymentReceipt>().AsNoTracking() on receipt.LinkedFinalReceiptId equals (Guid?)linked.Id into linkedReceipts
            from linked in linkedReceipts.DefaultIfEmpty()
            orderby receipt.CreationTime descending, receipt.ReceiptNumber descending
            select new
            {
                receipt.Id,
                receipt.ReceiptNumber,
                receipt.ReceiptKind,
                receipt.ReceiptScope,
                receipt.VerificationStatus,
                LinkedFinalReceiptNumber = linked != null ? linked.ReceiptNumber : null,
                receipt.TotalPaidAmount,
                receipt.ReceiptDate,
                StudentName = student != null
                    ? (student.FirstName + " " + (student.MiddleName ?? "") + " " + student.LastName).Trim().Replace("  ", " ")
                    : account != null ? account.ApplicantName : string.Empty,
                GrNo = student != null ? student.GrNo : null,
                RollNo = student != null ? student.RollNo : null,
            }).ToListAsync(cancellationToken);

        var rows = rawRows.Select(row => new FeeReceiptListFilterRow
        {
            Id = row.Id,
            ReceiptNumber = row.ReceiptNumber,
            ReceiptKind = row.ReceiptKind,
            ReceiptScope = row.ReceiptScope,
            VerificationStatus = row.VerificationStatus,
            LinkedFinalReceiptNumber = row.LinkedFinalReceiptNumber,
            TotalPaidAmount = row.TotalPaidAmount,
            ReceiptDate = row.ReceiptDate,
            StudentName = row.StudentName,
            GrNo = row.GrNo,
            RollNo = row.RollNo?.ToString(),
        }).ToList();

        if (includePaymentMethods && rows.Count > 0)
        {
            var payments = await (
                from payment in db.Set<PaymentMethodRecord>().AsNoTracking()
                join receipt in query on payment.FeeRecordMetadataId equals receipt.FeeCollectionTransactionId
                select new { receipt.Id, payment.PaymentMethod }).ToListAsync(cancellationToken);
            var labels = payments.GroupBy(payment => payment.Id).ToDictionary(
                group => group.Key,
                group => string.Join(", ", group.Select(payment => payment.PaymentMethod).Distinct()
                    .OrderBy(method => method).Select(FeeReceiptPresentation.GetPaymentMethodLabel)));
            foreach (var row in rows)
                if (labels.TryGetValue(row.Id, out var label)) row.PaymentMethodLabel = label;
        }

        return rows;
    }

    private static IQueryable<FeePaymentReceipt> BuildReceiptListQuery(
        ShikshaMitraDbContext dbContext, FeeReceiptListFilter filter)
    {

        // ── Build student sub-query for denormalized board/medium match ────────
        var query = dbContext.Set<FeePaymentReceipt>().AsNoTracking()
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
            var accountQ = dbContext.Set<FinanceFeeAccount>().AsQueryable();

            if (filter.StandardId.HasValue)
            {
                studentQ = studentQ.Where(s => s.StandardId == filter.StandardId.Value);
                accountQ = accountQ.Where(a => a.StandardId == filter.StandardId.Value);
            }

            if (filter.DivisionId.HasValue)
            {
                studentQ = studentQ.Where(s => s.DivisionId == filter.DivisionId.Value);
                accountQ = accountQ.Where(a => a.DivisionId == filter.DivisionId.Value);
            }

            var studentIds = studentQ.Select(s => s.Id);
            var accountIds = accountQ.Select(a => a.Id);
            query = query.Where(r => (!r.FinanceFeeAccountId.HasValue && r.StudentId.HasValue && studentIds.Contains(r.StudentId.Value)) ||
                (r.FinanceFeeAccountId.HasValue && accountIds.Contains(r.FinanceFeeAccountId.Value)));
        }

        // ── User scope filter ─────────────────────────────────────────────────
        query = ApplyPayerScope(query, filter.ScopedStudentIdsQuery, filter.ScopedAccountIdsQuery);

        // ── Free-text search ──────────────────────────────────────────────────
        query = ApplySearch(query, dbContext, filter.Search);

        if (filter.TableQuery is not null)
            query = filter.TableQuery.Apply(query, ReceiptFilterFields, ReceiptFilterFields.ContainsKey);

        return query;
    }

    private static IQueryable<FeePaymentReceipt> ApplyPayerScope(IQueryable<FeePaymentReceipt> query,
        IQueryable<Guid>? studentIds, IQueryable<Guid>? accountIds)
    {
        if (studentIds != null && accountIds != null)
            return query.Where(r => (!r.FinanceFeeAccountId.HasValue && r.StudentId.HasValue && studentIds.Contains(r.StudentId.Value)) ||
                (r.FinanceFeeAccountId.HasValue && accountIds.Contains(r.FinanceFeeAccountId.Value)));
        if (studentIds != null)
            return query.Where(r => !r.FinanceFeeAccountId.HasValue && r.StudentId.HasValue && studentIds.Contains(r.StudentId.Value));
        if (accountIds != null)
            return query.Where(r => r.FinanceFeeAccountId.HasValue && accountIds.Contains(r.FinanceFeeAccountId.Value));
        return query;
    }

    private static IQueryable<FeePaymentReceipt> ApplySearch(IQueryable<FeePaymentReceipt> query,
        ShikshaMitraDbContext db, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim().ToLowerInvariant();
        var pattern = "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        var numericGr = int.TryParse(term, out var grNo);
        var students = db.Set<Student>().AsQueryable();
        var studentIds = numericGr
            ? students.Where(s => s.GrNo == grNo).Select(s => s.Id)
            : students.Where(s => EF.Functions.Like((s.FirstName + " " + s.LastName).ToLower(), pattern, "\\") ||
                EF.Functions.Like((s.FirstName + " " + s.MiddleName + " " + s.LastName).ToLower(), pattern, "\\")).Select(s => s.Id);
        var accountIds = db.Set<FinanceFeeAccount>()
            .Where(a => EF.Functions.Like(a.ApplicantName.ToLower(), pattern, "\\") ||
                (a.StudentId.HasValue && studentIds.Contains(a.StudentId.Value)))
            .Select(a => a.Id);
        return query.Where(r => EF.Functions.Like(r.ReceiptNumber.ToLower(), pattern, "\\") ||
            (!r.FinanceFeeAccountId.HasValue && r.StudentId.HasValue && studentIds.Contains(r.StudentId.Value)) ||
            (r.FinanceFeeAccountId.HasValue && accountIds.Contains(r.FinanceFeeAccountId.Value)));
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
