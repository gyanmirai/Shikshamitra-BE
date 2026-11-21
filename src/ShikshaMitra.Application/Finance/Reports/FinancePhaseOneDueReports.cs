using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Finance.Reports;

public class FinancePhaseOneDueReports(
    IDueFeeRepository dues,
    IRepository<FeeHead, Guid> heads,
    IRepository<Standard, Guid> standards,
    IRepository<Division, Guid> divisions,
    IRepository<FeeCollectionDueFeeAllocation, Guid> allocations,
    IRepository<FeeRecordMetadata, Guid> collections,
    FinanceReportPayerQueryService payers,
    IAsyncQueryableExecuter queries) : ITransientDependency
{
    public async Task<FinanceReportPreviewDto> BuildAsync(FinanceReportRequestDto request,
        FinanceReportPreviewDto payload, FinanceReportExecutionScope scope, bool exportAllRows)
    {
        var filters = request.Filters;
        var yearId = filters.AcademicYearIds!.Single();
        var payerRows = await payers.GetPayersAsync(filters, scope, useHistoricalPlacement: true);
        if (request.ReportType == FinanceReportTypes.StudentStatement)
            payerRows = SelectStatementPayers(payerRows, filters);

        var payerMap = payerRows.ToDictionary(p => p.Key);
        var studentIds = payerRows.Where(p => !p.FinanceFeeAccountId.HasValue && p.StudentId.HasValue)
            .Select(p => p.StudentId!.Value).ToArray();
        var accountIds = payerRows.Where(p => p.FinanceFeeAccountId.HasValue)
            .Select(p => p.FinanceFeeAccountId!.Value).ToArray();
        var headIds = filters.FeeHeadIds?.ToArray() ?? [];
        var headQuery = (await heads.GetQueryableAsync()).Where(h => h.TenantId == scope.TenantId &&
            h.AcademicYearId == yearId && (headIds.Length == 0 || Enumerable.Contains(headIds, h.Id)));
        var feeHeads = await queries.ToListAsync(headQuery);
        var headMap = feeHeads.ToDictionary(h => h.Id, h => h.Name);
        var selectedHeadIds = headMap.Keys.ToArray();
        var dueQuery = (await dues.GetQueryableAsync()).Where(d => d.TenantId == scope.TenantId &&
            Enumerable.Contains(selectedHeadIds, d.FeeHeadId) &&
            ((d.FinanceFeeAccountId.HasValue && Enumerable.Contains(accountIds, d.FinanceFeeAccountId.Value)) ||
             (!d.FinanceFeeAccountId.HasValue && d.StudentId.HasValue && Enumerable.Contains(studentIds, d.StudentId.Value))));
        var dueRows = await queries.ToListAsync(dueQuery);
        var today = DateTimeOffset.UtcNow.ToOffset(FinancePhaseOneReportRows.SchoolOffset).Date;
        var classIds = payerRows.Where(p => p.StandardId.HasValue).Select(p => p.StandardId!.Value).Distinct().ToArray();
        var divisionIds = payerRows.Where(p => p.DivisionId.HasValue).Select(p => p.DivisionId!.Value).Distinct().ToArray();
        var classMap = (await standards.GetListAsync(s => Enumerable.Contains(classIds, s.Id))).ToDictionary(s => s.Id, s => s.Name);
        var divisionMap = (await divisions.GetListAsync(d => Enumerable.Contains(divisionIds, d.Id))).ToDictionary(d => d.Id, d => d.Name);
        var lines = dueRows.Select(d =>
        {
            var payer = payerMap[FinanceReportPayerKey.From(d.StudentId, d.FinanceFeeAccountId)];
            return new DueLine(d, payer, headMap[d.FeeHeadId], classMap.GetValueOrDefault(payer.StandardId ?? Guid.Empty),
                divisionMap.GetValueOrDefault(payer.DivisionId ?? Guid.Empty));
        });
        if (filters.FromDate is { } from && filters.ToDate is { } to)
            lines = lines.Where(l => l.ReportDemandDate >= from.Date && l.ReportDemandDate <= to.Date);

        var rows = new FinancePhaseOneReportRows(payload);
        var evidence = request.ReportType == FinanceReportTypes.FeeDemandSummary
            ? new Dictionary<Guid, DuePaymentEvidence>() : await GetEvidenceAsync(
                lines.Select(l => l.Due.Id).ToArray(), scope.TenantId, yearId);
        switch (request.ReportType)
        {
            case FinanceReportTypes.FeeDemandSummary:
                WriteSummary(rows, lines, today, filters.GroupBy);
                break;
            case FinanceReportTypes.OutstandingFees:
                WriteOutstanding(rows, lines, evidence, today, filters);
                break;
            default:
                WriteStatement(rows, lines, evidence, today);
                break;
        }
        return rows.Finish(request, exportAllRows);
    }

    private async Task<Dictionary<Guid, DuePaymentEvidence>> GetEvidenceAsync(Guid[] dueIds, Guid? tenantId, Guid yearId)
    {
        if (dueIds.Length == 0)
            return [];
        var rows = await queries.ToListAsync(
            from allocation in await allocations.GetQueryableAsync()
            join collection in await collections.GetQueryableAsync() on allocation.FeeRecordMetadataId equals collection.Id
            where allocation.TenantId == tenantId && collection.TenantId == tenantId &&
                collection.AcademicYearId == yearId && Enumerable.Contains(dueIds, allocation.DueFeeId)
            select new { allocation.DueFeeId, allocation.Amount, collection.Status, collection.Id });
        return rows.GroupBy(row => row.DueFeeId).ToDictionary(group => group.Key, group => new DuePaymentEvidence(
            group.Where(row => row.Status == FeeCollectionStatus.InReconciliation).Sum(row => row.Amount),
            group.Where(row => row.Status == FeeCollectionStatus.Reversed).Sum(row => (decimal)row.Amount),
            string.Join(", ", group.Select(row => row.Id).Distinct().Order())));
    }

    private static List<FinanceReportPayer> SelectStatementPayers(List<FinanceReportPayer> payers, FinanceReportFilterDto filters)
    {
        var search = filters.PayerSearch?.Trim();
        var matches = payers.Where(p => filters.StudentId.HasValue ? p.StudentId == filters.StudentId :
            p.StudentId?.ToString() == search || p.FinanceFeeAccountId?.ToString() == search ||
            p.GrNo?.ToString() == search || string.Equals(p.DisplayName, search, StringComparison.OrdinalIgnoreCase)).ToList();
        var identities = matches.Select(p => p.StudentId ?? p.FinanceFeeAccountId).Distinct().Count();
        if (identities > 1)
            throw new BusinessException(FinanceReportRequestGuard.InvalidRequest)
                .WithData("Field", "PayerSearch").WithData("Reason", "More than one payer matches. Use an admission number or payer ID.");
        return matches;
    }

    private static void WriteSummary(FinancePhaseOneReportRows rows, IEnumerable<DueLine> lines, DateTime today, string? groupBy)
    {
        rows.Define(("Class", "Class", null), ("Division", "Division", null), ("FeeHead", "Fee head", null),
            ("Students", "Students", "number"), ("Applicants", "Unlinked applicant accounts", "number"),
            ("Original", "Original demand (INR)", "money"), ("Concession", "Concessions (INR)", "money"),
            ("Credit", "Adjustment credits (INR)", "money"), ("Net", "Net demand (INR)", "money"),
            ("Applied", "Applied payments (INR)", "money"), ("Outstanding", "Outstanding (INR)", "money"),
            ("Overdue", "Overdue (INR)", "money"), ("Future", "Future unpaid (INR)", "money"),
            ("Rate", "Collection rate (%)", "number"), ("BalanceDate", "Balance date (IST)", null));
        foreach (var group in lines.GroupBy(l => groupBy == "feeHead"
                     ? (ClassId: (Guid?)null, DivisionId: (Guid?)null, FeeHeadId: (Guid?)l.Due.FeeHeadId)
                     : (ClassId: l.Payer.StandardId, DivisionId: l.Payer.DivisionId, FeeHeadId: (Guid?)null))
                 .OrderBy(g => g.First().Class).ThenBy(g => g.First().Division).ThenBy(g => g.First().FeeHead)
                 .ThenBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2).ThenBy(g => g.Key.Item3))
        {
            var first = group.First();
            var net = group.Sum(l => l.Due.NetPayableAmount);
            var applied = group.Sum(l => l.Due.AppliedPaymentAmount);
            rows.Add(groupBy == "feeHead" ? null : first.Class, groupBy == "feeHead" ? null : first.Division,
                groupBy == "feeHead" ? first.FeeHead : null,
                group.Select(l => l.Payer.StudentId).OfType<Guid>().Distinct().Count(),
                group.Where(l => !l.Payer.StudentId.HasValue).Select(l => l.Payer.FinanceFeeAccountId).Distinct().Count(),
                group.Sum(l => l.Due.OriginalAmount), group.Sum(l => l.Due.ConcessionAmount),
                group.Sum(l => l.Due.AdjustmentCreditAmount), net, applied, group.Sum(l => l.Due.OutstandingAmount),
                group.Where(l => l.DaysOverdue(today) > 0).Sum(l => l.Due.OutstandingAmount),
                group.Where(l => !l.Due.IsBillGeneratedOn(today) || l.DueDate > today).Sum(l => l.Due.OutstandingAmount),
                net > 0 ? decimal.Round(applied * 100m / net, 2) : null, today);
        }
    }

    private static void WriteOutstanding(FinancePhaseOneReportRows rows, IEnumerable<DueLine> lines,
        Dictionary<Guid, DuePaymentEvidence> evidence, DateTime today, FinanceReportFilterDto filters)
    {
        rows.Define(("Student", "Payer", null), ("GRNo", "Admission / GR no.", null), ("Class", "Class", null),
            ("Division", "Division", null), ("FeeHead", "Fee head", null), ("Installment", "Installment", null),
            ("DemandDate", "Demand date", null), ("DueDate", "Due date", null), ("Net", "Net payable (INR)", "money"),
            ("Applied", "Applied payment (INR)", "money"), ("Outstanding", "Outstanding principal (INR)", "money"),
            ("DaysOverdue", "Days overdue", "number"), ("AgeingBucket", "Ageing bucket", null),
            ("Status", "Fee status", null), ("BalanceDate", "Balance date (IST)", null));
        foreach (var line in lines.Where(l => l.Due.OutstandingAmount > 0).OrderBy(l => l.DueDate)
                     .ThenBy(l => l.Payer.DisplayName).ThenBy(l => l.Due.Id))
        {
            var days = line.DaysOverdue(today);
            var bucket = FinancePhaseOneReportRows.AgeingBucket(days);
            var status = !line.Due.IsBillGeneratedOn(today) ? "Future" :
                DueFeeCollectionStatusResolver.Resolve(line.Due, today, evidence.GetValueOrDefault(line.Due.Id)?.Pending ?? 0).ToString();
            if (!FinancePhaseOneReportRows.Matches(line.Installment, filters.Installment) ||
                (!string.IsNullOrWhiteSpace(filters.AgeingBucket) && filters.AgeingBucket != bucket) ||
                (!string.IsNullOrWhiteSpace(filters.Status) && filters.Status != status))
                continue;
            rows.Add(line.Payer.DisplayName, line.Payer.GrNo, line.Class, line.Division, line.FeeHead, line.Installment,
                line.Due.DemandDate, line.DueDate, line.Due.NetPayableAmount, line.Due.AppliedPaymentAmount,
                line.Due.OutstandingAmount, days, bucket, status, today);
        }
    }

    private static void WriteStatement(FinancePhaseOneReportRows rows, IEnumerable<DueLine> lines,
        Dictionary<Guid, DuePaymentEvidence> evidence, DateTime today)
    {
        rows.Define(("Payer", "Payer", null), ("GRNo", "Admission / GR no.", null), ("Class", "Class", null),
            ("Division", "Division", null), ("Date", "Demand date", null), ("DueDate", "Due date", null),
            ("Type", "Entry type", null), ("Installment", "Installment", null), ("FeeHead", "Fee head", null),
            ("Reference", "Due reference", null), ("Collections", "Collection references", null),
            ("Original", "Original demand (INR)", "money"),
            ("Concession", "Concession (INR)", "money"), ("Credit", "Adjustment credit (INR)", "money"),
            ("Paid", "Recorded payment (INR)", "money"), ("Refund", "Processed refund (INR)", "money"),
            ("Reversal", "Payment reversals (INR)", "money"),
            ("Outstanding", "Outstanding principal (INR)", "money"), ("Overpayment", "Overpayment (INR)", "money"),
            ("BalanceDate", "Balance date (IST)", null));
        foreach (var line in lines.OrderBy(l => l.ReportDemandDate).ThenBy(l => l.DueDate).ThenBy(l => l.Due.Id))
        {
            var due = line.Due;
            var payment = evidence.GetValueOrDefault(due.Id);
            rows.Add(line.Payer.DisplayName, line.Payer.GrNo, line.Class, line.Division, due.DemandDate, line.DueDate,
                "Fee installment", line.Installment, line.FeeHead, due.Id, payment?.CollectionReferences, due.OriginalAmount,
                due.ConcessionAmount, due.AdjustmentCreditAmount, due.PaidAmount, due.RefundedAmount,
                payment?.Reversed ?? 0, due.OutstandingAmount,
                due.OverpaymentAmount, today);
        }
    }

    private sealed record DuePaymentEvidence(double Pending, decimal Reversed, string CollectionReferences);

    private sealed record DueLine(DueFee Due, FinanceReportPayer Payer, string FeeHead, string? Class, string? Division)
    {
        public DateTime DueDate => (Due.ScheduleDueDate ?? Due.DueDate).Date;
        public DateTime ReportDemandDate => Due.DemandDate?.Date ?? DueDate;
        public string Installment => Due.InstallmentName ?? $"{Due.Month} {Due.Year}";
        public int DaysOverdue(DateTime today) =>
            Due.OutstandingAmount > 0 && Due.IsBillGeneratedOn(today) ? Math.Max(0, (today - DueDate).Days) : 0;
    }
}
