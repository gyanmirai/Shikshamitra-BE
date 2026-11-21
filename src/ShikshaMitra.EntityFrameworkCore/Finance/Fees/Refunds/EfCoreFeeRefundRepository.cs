using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Students;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public class EfCoreFeeRefundRepository(
    IDbContextProvider<ShikshaMitraDbContext> dbContextProvider
) : EfCoreRepository<ShikshaMitraDbContext, FeeRefundRequest, Guid>(dbContextProvider),
    IFeeRefundRepository
{
    public async Task<(List<FeeRefundRequest> Items, int TotalCount, Dictionary<FeeRefundStatus, int> StatusCounts)> GetListWithDetailsAsync(
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
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<FeeRefundColumnFilter>? columnFilters = null,
        IQueryable<Guid>? scopedAccountIdsQuery = null,
        Guid? financeFeeAccountId = null,
        IQueryable<Guid>? visibleRequestIdsQuery = null,
        IQueryable<Guid>? authorizedExternalRequestIdsQuery = null,
        Guid? financeExternalSourceLinkId = null,
        Guid? boardId = null)
    {
        var dbContext = await GetDbContextAsync();
        var dbSet = dbContext.Set<FeeRefundRequest>();
        var legacyScope = dbSet.Where(r => !r.FinanceExternalSourceLinkId.HasValue);
        if (visibleRequestIdsQuery != null)
            legacyScope = legacyScope.Where(r => visibleRequestIdsQuery.Contains(r.Id));
        if (scopedStudentIdsQuery != null && scopedAccountIdsQuery != null)
            legacyScope = legacyScope.Where(r => (r.StudentId.HasValue && scopedStudentIdsQuery.Contains(r.StudentId.Value)) ||
                (!r.StudentId.HasValue && r.FinanceFeeAccountId.HasValue && scopedAccountIdsQuery.Contains(r.FinanceFeeAccountId.Value)));
        else if (scopedStudentIdsQuery != null)
            legacyScope = legacyScope.Where(r => r.StudentId.HasValue && scopedStudentIdsQuery.Contains(r.StudentId.Value));
        else if (scopedAccountIdsQuery != null)
            legacyScope = legacyScope.Where(r => r.FinanceFeeAccountId.HasValue && scopedAccountIdsQuery.Contains(r.FinanceFeeAccountId.Value));
        var legacyIds = legacyScope.Select(r => r.Id);
        var externalIds = authorizedExternalRequestIdsQuery ?? dbSet.Where(_ => false).Select(r => r.Id);
        var query = dbSet.AsNoTracking()
            .Where(r => !r.FinanceExternalSourceLinkId.HasValue && legacyIds.Contains(r.Id) ||
                r.FinanceExternalSourceLinkId.HasValue && externalIds.Contains(r.Id));

        if (financeExternalSourceLinkId.HasValue)
            query = query.Where(r => r.FinanceExternalSourceLinkId == financeExternalSourceLinkId.Value);

        if (studentId.HasValue)
            query = query.Where(r => r.StudentId == studentId.Value);
        if (academicYearId.HasValue)
            query = query.Where(r => r.AcademicYearId == academicYearId.Value);
        if (financeFeeAccountId.HasValue)
            query = query.Where(r => r.FinanceFeeAccountId == financeFeeAccountId.Value);

        if (boardId.HasValue || academicMediumId.HasValue || standardId.HasValue || divisionId.HasValue)
        {
            var studentQuery = dbContext.Set<Student>().AsQueryable();
            var accountQuery = dbContext.Set<FinanceFeeAccount>().AsQueryable();
            var standardQuery = dbContext.Set<Standard>().AsQueryable();

            if (boardId.HasValue)
            {
                studentQuery = studentQuery.Where(s => s.AcademicBoardId == boardId.Value);
                accountQuery = accountQuery.Where(a => a.BoardId == boardId.Value);
                standardQuery = standardQuery.Where(s =>
                    s.EducationMediumStage.EducationMedium.EducationBoardId == boardId.Value);
            }

            if (academicMediumId.HasValue)
            {
                studentQuery = studentQuery.Where(s => s.AcademicMediumId == academicMediumId.Value);
                accountQuery = accountQuery.Where(a => a.EducationMediumId == academicMediumId.Value);
                standardQuery = standardQuery.Where(s =>
                    s.EducationMediumStage.EducationMediumId == academicMediumId.Value);
            }

            if (standardId.HasValue)
            {
                studentQuery = studentQuery.Where(s => s.StandardId == standardId.Value);
                accountQuery = accountQuery.Where(a => a.StandardId == standardId.Value);
                standardQuery = standardQuery.Where(s => s.Id == standardId.Value);
            }

            if (divisionId.HasValue)
            {
                studentQuery = studentQuery.Where(s => s.DivisionId == divisionId.Value);
                accountQuery = accountQuery.Where(a => a.DivisionId == divisionId.Value);
            }

            var filteredStudentIds = studentQuery.Select(s => s.Id);
            var filteredAccountIds = accountQuery.Select(a => a.Id);
            var filteredStandardIds = standardQuery.Select(s => s.Id);
            var currentStudentYears = dbContext.Set<Student>().Select(s => new { s.Id, s.AcademicYearId });
            var historicPlacements = dbContext.Set<StudentEnrollment>().Where(e =>
                e.StandardId.HasValue && filteredStandardIds.Contains(e.StandardId.Value) &&
                (!divisionId.HasValue || e.DivisionId == divisionId.Value));
            query = query.Where(r => (!r.FinanceFeeAccountId.HasValue && r.StudentId.HasValue &&
                ((filteredStudentIds.Contains(r.StudentId.Value) && currentStudentYears.Any(s =>
                    s.Id == r.StudentId.Value && s.AcademicYearId == r.AcademicYearId)) ||
                 historicPlacements.Any(e => e.StudentId == r.StudentId.Value && e.AcademicYearId == r.AcademicYearId))) ||
                (r.FinanceFeeAccountId.HasValue && filteredAccountIds.Contains(r.FinanceFeeAccountId.Value)));
        }

        if (legacyHistory)
            query = query.Where(r => r.WorkflowVersion == 0);
        else if (workflowVersion.HasValue)
            query = query.Where(r => r.WorkflowVersion == workflowVersion.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            var matchingStudents = dbContext.Set<Student>()
                .Where(s => (s.FirstName != null && s.FirstName.ToLower().Contains(term)) ||
                    (s.LastName != null && s.LastName.ToLower().Contains(term)) ||
                    (s.GrNo.HasValue && s.GrNo.Value.ToString().Contains(term)))
                .Select(s => s.Id);
            var matchingAccounts = dbContext.Set<FinanceFeeAccount>()
                .Where(a => a.ApplicantName.ToLower().Contains(term) ||
                    (a.StudentId.HasValue && matchingStudents.Contains(a.StudentId.Value)))
                .Select(a => a.Id);
            var matchingReceipts = dbContext.Set<FeePaymentReceipt>()
                .Where(receipt => receipt.ReceiptNumber.ToLower().Contains(term)).Select(receipt => receipt.Id);
            query = query.Where(r => r.VoucherNumber.ToLower().Contains(term) ||
                (r.OriginalReceiptNumber != null && r.OriginalReceiptNumber.ToLower().Contains(term)) ||
                r.Lines.Any(line => line.SourceReceiptId.HasValue && matchingReceipts.Contains(line.SourceReceiptId.Value)) ||
                (!r.FinanceFeeAccountId.HasValue && r.StudentId.HasValue && matchingStudents.Contains(r.StudentId.Value)) ||
                (r.FinanceFeeAccountId.HasValue && matchingAccounts.Contains(r.FinanceFeeAccountId.Value)));
        }

        query = FeeRefundColumnFilterQuery.Apply(query, dbContext.Set<Student>(), columnFilters ?? [],
            dbContext.Set<FeePaymentReceipt>(), dbContext.Set<StudentEnrollment>(), dbContext.Set<Standard>(),
            dbContext.Set<ShikshaMitra.TenantSettings.Divisions.Division>());
        var statusCounts = await query.GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
        var totalCount = status.HasValue ? statusCounts.GetValueOrDefault(status.Value) : statusCounts.Values.Sum();
        if (totalCount == 0)
            return ([], 0, statusCounts);
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        query = ApplySorting(query, sorting);

        var items = await query
            .Include(r => r.Lines)
            .Include(r => r.Reservations)
            .Include(r => r.Settlements)
            .AsSplitQuery()
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(cancellationToken);

        return (items, totalCount, statusCounts);
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
            .Where(r => r.DueFeeId.HasValue && dueFeeIds.Contains(r.DueFeeId.Value) && r.Status == FeeRefundReservationStatus.Active);
        return await query.GroupBy(r => r.DueFeeId!.Value)
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
        var dbContext = await GetDbContextAsync();
        var linkedAccounts = dbContext.Set<FinanceFeeAccount>()
            .Where(a => a.StudentId == studentId && a.AcademicYearId == academicYearId).Select(a => a.Id);
        var requests = dbContext.Set<FeeRefundRequest>().Where(r =>
            (r.StudentId == studentId || (r.FinanceFeeAccountId.HasValue && linkedAccounts.Contains(r.FinanceFeeAccountId.Value))) &&
            r.AcademicYearId == academicYearId &&
            (!excludingRequestId.HasValue || r.Id != excludingRequestId.Value));
        return await GetCommittedAmountAsync(requests, cancellationToken);
    }

    public async Task<decimal> GetAccountYearCommittedAmountAsync(Guid financeFeeAccountId, Guid academicYearId,
        Guid? excludingRequestId = null, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        var requests = dbSet.Where(r => r.FinanceFeeAccountId == financeFeeAccountId && r.AcademicYearId == academicYearId &&
            (!excludingRequestId.HasValue || r.Id != excludingRequestId.Value));
        return await GetCommittedAmountAsync(requests, cancellationToken);
    }

    private static async Task<decimal> GetCommittedAmountAsync(IQueryable<FeeRefundRequest> requests, CancellationToken cancellationToken)
    {
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
            return query.OrderByDescending(r => r.CreationTime).ThenByDescending(r => r.Id);

        return sorting.ToLower().Trim() switch
        {
            "createdat asc"  or "creationtime asc"  => query.OrderBy(r => r.CreationTime).ThenBy(r => r.Id),
            "createdat desc" or "creationtime desc" => query.OrderByDescending(r => r.CreationTime).ThenByDescending(r => r.Id),
            "totalamount asc"  => query.OrderBy(r => r.TotalAmount).ThenBy(r => r.Id),
            "totalamount desc" => query.OrderByDescending(r => r.TotalAmount).ThenByDescending(r => r.Id),
            "status asc"  => query.OrderBy(r => r.Status).ThenBy(r => r.Id),
            "status desc" => query.OrderByDescending(r => r.Status).ThenByDescending(r => r.Id),
            _ => query.OrderByDescending(r => r.CreationTime).ThenByDescending(r => r.Id),
        };
    }
}
