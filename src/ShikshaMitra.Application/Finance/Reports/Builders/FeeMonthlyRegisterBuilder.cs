using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Finance.Reports.Builders;

[Volo.Abp.DependencyInjection.ExposeServices(typeof(IFinanceReportBuilder))]
public class FeeMonthlyRegisterBuilder(
    IRepository<FeePaymentReceipt, Guid> receiptRepository,
    FinanceReportPayerQueryService payerService,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Division, Guid> divisionRepository,
    IAsyncQueryableExecuter asyncExecuter)
    : IFinanceReportBuilder, ITransientDependency
{
    public string ReportType => FinanceReportTypes.FeeMonthlyRegister;

    public async Task<FinanceReportPreviewDto> BuildAsync(FinanceReportRequestDto request, FinanceReportPreviewDto payload, FinanceReportExecutionScope executionScope, bool exportAllRows)
    {
        var receiptBookIds = request.Filters.ReceiptBookIds ?? new List<Guid>();

        // Use canonical payer identities; applicant account evidence keeps its original scope.
        var payers = await payerService.GetPayersAsync(request.Filters, executionScope);

        if (payers.Count == 0) return payload;
        // 2. Fetch Receipts
        var receiptQuery = FinanceReportPayerQueryService.FilterReceipts(await receiptRepository.GetQueryableAsync(), payers)
            .WhereIf(receiptBookIds.Any(), r => r.ReceiptBookId.HasValue && receiptBookIds.Contains(r.ReceiptBookId.Value));

        var receipts = await asyncExecuter.ToListAsync(receiptQuery);

        // 3. Resolve Standards and Divisions
        var stdIds = payers.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
        var divIds = payers.Where(s => s.DivisionId.HasValue).Select(s => s.DivisionId!.Value).Distinct().ToList();

        var standards = await asyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync()).Where(s => stdIds.Contains(s.Id)));
        var divisions = await asyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync()).Where(d => divIds.Contains(d.Id)));

        var stdMap = standards.ToDictionary(s => s.Id, s => s.Name);
        var divMap = divisions.ToDictionary(d => d.Id, d => d.Name);

        // Group receipts by canonical payer and month.
        var groupedData = new Dictionary<FinanceReportPayerKey, Dictionary<int, double>>();

        foreach (var receipt in receipts)
        {
            var payerKey = FinanceReportPayerKey.From(receipt.StudentId, receipt.FinanceFeeAccountId);
            var month = receipt.ReceiptDate.Month;

            if (!groupedData.ContainsKey(payerKey)) groupedData[payerKey] = new();
            if (!groupedData[payerKey].ContainsKey(month)) groupedData[payerKey][month] = 0;

            groupedData[payerKey][month] += receipt.TotalPaidAmount;
        }

        // 5. Define Columns
        payload.Columns.Add(new() { Id = "RollNo", Label = "Roll No." });
        payload.Columns.Add(new() { Id = "GRNo", Label = "ID No." });
        payload.Columns.Add(new() { Id = "StudentName", Label = "Name" });
        payload.Columns.Add(new() { Id = "Class", Label = "Class" });
        payload.Columns.Add(new() { Id = "Division", Label = "Division" });

        // Month columns (using standard Jan-Dec order for simplicity, though academic years might vary)
        var months = Enumerable.Range(1, 12).ToList();
        foreach (var month in months)
        {
            var monthName = new DateTime(2000, month, 1).ToString("MMM");
            payload.Columns.Add(new() { Id = $"M_{month}", Label = monthName });
        }
        payload.Columns.Add(new() { Id = "Total", Label = "Total" });

        // 6. Build Rows
        var rows = new List<Dictionary<string, string?>>();
        foreach (var payer in payers)
        {
            if (!groupedData.TryGetValue(payer.Key, out var monthData))
            {
                continue; // Skip payers with no payments, or we could include them with 0. Let's include only those with payments.
            }

            var stdName = payer.StandardId.HasValue && stdMap.TryGetValue(payer.StandardId.Value, out var sn) ? sn : "N/A";
            var divName = payer.DivisionId.HasValue && divMap.TryGetValue(payer.DivisionId.Value, out var dn) ? dn : "N/A";
            var payerName = payer.DisplayName;

            var row = new Dictionary<string, string?>
            {
                { "RollNo", payer.RollNo?.ToString() ?? "N/A" },
                { "GRNo", payer.GrNo?.ToString() ?? "N/A" },
                { "StudentName", payerName },
                { "Class", stdName },
                { "Division", divName }
            };

            double rowTotal = 0;
            foreach (var month in months)
            {
                if (monthData.TryGetValue(month, out var amount))
                {
                    row[$"M_{month}"] = Math.Ceiling(amount).ToString("N0");
                    rowTotal += amount;
                }
                else
                {
                    row[$"M_{month}"] = "0.00";
                }
            }

            row["Total"] = Math.Ceiling(rowTotal).ToString("N0");
            rows.Add(row);
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;

        var sortedRows = rows.OrderBy(r => r["Class"]).ThenBy(r => r["Division"]).ThenBy(r => r["StudentName"]).ToList();

        payload.Rows = exportAllRows
            ? sortedRows
            : sortedRows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();

        return payload;
    }
}
