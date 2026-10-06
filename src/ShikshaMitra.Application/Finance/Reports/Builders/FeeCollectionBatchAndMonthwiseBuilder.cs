using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Finance.Reports.Builders;

[Volo.Abp.DependencyInjection.ExposeServices(typeof(IFinanceReportBuilder))]
public class FeeCollectionBatchAndMonthwiseBuilder(
    IRepository<FeeRecordMetadata, Guid> feeRecordRepository,
    IRepository<PaymentMethodRecord, Guid> paymentMethodRepository,
    IRepository<AcademicYear, Guid> academicYearRepository,
    IAsyncQueryableExecuter asyncExecuter)
    : IFinanceReportBuilder, ITransientDependency
{
    public string ReportType => FinanceReportTypes.FeeCollectionBatchAndMonthwise;

    public async Task<FinanceReportPreviewDto> BuildAsync(FinanceReportRequestDto request, FinanceReportPreviewDto payload, FinanceReportExecutionScope executionScope, bool exportAllRows)
    {
        var academicYearIds = request.Filters.AcademicYearIds ?? new List<Guid>();
        var targetMonths = request.Filters.Months != null && request.Filters.Months.Any()
            ? request.Filters.Months
            : Enumerable.Range(1, 12).ToList();

        var targetAcademicYears = await asyncExecuter.ToListAsync(
            (await academicYearRepository.GetQueryableAsync())
            .WhereIf(academicYearIds.Any(), x => academicYearIds.Contains(x.Id))
            .OrderByDescending(x => x.StartDate)
        );

        // Define dynamic columns
        payload.Columns.Add(new() { Id = "Month", Label = "Month Detail" });
        foreach (var year in targetAcademicYears)
        {
            payload.Columns.Add(new() { Id = $"Cash_{year.Id}", Label = $"{year.Name} Cash" });
            payload.Columns.Add(new() { Id = $"Gateway_{year.Id}", Label = $"{year.Name} Payment Gateway" });
            payload.Columns.Add(new() { Id = $"Total_{year.Id}", Label = $"{year.Name} Total" });
        }

        var targetYearIds = targetAcademicYears.Select(x => x.Id).ToList();

        var metadataQuery = (await feeRecordRepository.GetQueryableAsync())
            .Where(m => targetYearIds.Contains(m.AcademicYearId));

        var metadataList = await asyncExecuter.ToListAsync(metadataQuery);
        var metadataMap = metadataList.ToDictionary(m => m.Id, m => m.AcademicYearId);

        var metadataIds = metadataMap.Keys.ToList();

        var paymentsQuery = (await paymentMethodRepository.GetQueryableAsync())
            .Where(p => metadataIds.Contains(p.FeeRecordMetadataId));

        var paymentsList = await asyncExecuter.ToListAsync(paymentsQuery);

        paymentsList = paymentsList.Where(p => targetMonths.Contains(p.PaymentDate.Month)).ToList();

        var groupedData = new Dictionary<int, Dictionary<Guid, (double Cash, double Gateway, double Total)>>();

        foreach (var p in paymentsList)
        {
            var month = p.PaymentDate.Month;
            if (!metadataMap.TryGetValue(p.FeeRecordMetadataId, out var acYearId)) continue;

            if (!groupedData.ContainsKey(month)) groupedData[month] = new();
            if (!groupedData[month].ContainsKey(acYearId)) groupedData[month][acYearId] = (0, 0, 0);

            var existing = groupedData[month][acYearId];
            var amount = (double)p.Amount;

            var isCash = p.PaymentMethod == PaymentMethodEnum.Cash;

            groupedData[month][acYearId] = (
                existing.Cash + (isCash ? amount : 0),
                existing.Gateway + (isCash ? 0 : amount),
                existing.Total + amount
            );
        }

        var rows = new List<Dictionary<string, string?>>();
        foreach (var month in targetMonths)
        {
            if (!groupedData.TryGetValue(month, out var yearData))
            {
                yearData = new();
            }

            var monthName = new DateTime(2000, month, 1).ToString("MMMM");
            var row = new Dictionary<string, string?>
            {
                { "Month", monthName },
                { "MonthIndex", month.ToString() }
            };

            foreach (var year in targetAcademicYears)
            {
                if (yearData.TryGetValue(year.Id, out var amounts))
                {
                    row[$"Cash_{year.Id}"] = Math.Ceiling(amounts.Cash).ToString("N0");
                    row[$"Gateway_{year.Id}"] = Math.Ceiling(amounts.Gateway).ToString("N0");
                    row[$"Total_{year.Id}"] = Math.Ceiling(amounts.Total).ToString("N0");
                }
                else
                {
                    row[$"Cash_{year.Id}"] = "0.00";
                    row[$"Gateway_{year.Id}"] = "0.00";
                    row[$"Total_{year.Id}"] = "0.00";
                }
            }
            rows.Add(row);
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;

        var sortedRows = rows.OrderBy(r => int.Parse(r["MonthIndex"]!)).ToList();

        payload.Rows = exportAllRows
            ? sortedRows
            : sortedRows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();

        return payload;
    }
}
