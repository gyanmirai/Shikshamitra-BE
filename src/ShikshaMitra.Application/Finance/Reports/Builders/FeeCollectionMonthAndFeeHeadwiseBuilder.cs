using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Finance.Reports.Builders;

[Volo.Abp.DependencyInjection.ExposeServices(typeof(IFinanceReportBuilder))]
public class FeeCollectionMonthAndFeeHeadwiseBuilder(
    IRepository<PaymentMethodRecord, Guid> paymentMethodRepository,
    IRepository<FeeRecordMetadata, Guid> feeRecordRepository,
    IRepository<FeeCollectionDueFeeAllocation, Guid> allocationRepository,
    IRepository<DueFee, Guid> dueFeeRepository,
    IRepository<FeeHead, Guid> feeHeadRepository,
    IAsyncQueryableExecuter asyncExecuter)
    : IFinanceReportBuilder, ITransientDependency
{
    public string ReportType => FinanceReportTypes.FeeCollectionMonthAndFeeHeadwise;

    public async Task<FinanceReportPreviewDto> BuildAsync(FinanceReportRequestDto request, FinanceReportPreviewDto payload, FinanceReportExecutionScope executionScope, bool exportAllRows)
    {
        var targetMonths = request.Filters.Months != null && request.Filters.Months.Any()
            ? request.Filters.Months
            : Enumerable.Range(1, 12).ToList();

        var feeHeadIds = request.Filters.FeeHeadIds ?? new List<Guid>();

        var feeHeadsQuery = (await feeHeadRepository.GetQueryableAsync())
            .WhereIf(feeHeadIds.Any(), x => feeHeadIds.Contains(x.Id));

        var targetFeeHeads = await asyncExecuter.ToListAsync(feeHeadsQuery);

        // Define dynamic columns
        payload.Columns.Add(new() { Id = "Month", Label = "Month Detail" });
        foreach (var feeHead in targetFeeHeads)
        {
            payload.Columns.Add(new() { Id = $"FH_{feeHead.Id}", Label = feeHead.Name });
        }
        payload.Columns.Add(new() { Id = "Total", Label = "Total" });

        var targetFeeHeadIds = targetFeeHeads.Select(x => x.Id).ToList();

        var academicYearIds = request.Filters.AcademicYearIds ?? [];
        var scopedMetadataIds = await asyncExecuter.ToListAsync(
            (await feeRecordRepository.GetQueryableAsync())
                .Where(metadata => academicYearIds.Contains(metadata.AcademicYearId))
                .Select(metadata => metadata.Id));

        var paymentsQuery = (await paymentMethodRepository.GetQueryableAsync())
            .Where(payment => scopedMetadataIds.Contains(payment.FeeRecordMetadataId));
        var paymentsList = await asyncExecuter.ToListAsync(paymentsQuery);

        // Filter payments by requested months
        paymentsList = paymentsList.Where(p => targetMonths.Contains(p.PaymentDate.Month)).ToList();
        var metadataIds = paymentsList.Select(p => p.FeeRecordMetadataId).Distinct().ToList();

        // Get Allocations
        var allocationsQuery = (await allocationRepository.GetQueryableAsync())
            .Where(a => metadataIds.Contains(a.FeeRecordMetadataId));
        var allocationsList = await asyncExecuter.ToListAsync(allocationsQuery);

        // Get DueFees for those allocations
        var dueFeeIds = allocationsList.Select(a => a.DueFeeId).Distinct().ToList();
        var dueFeesQuery = (await dueFeeRepository.GetQueryableAsync())
            .Where(d => dueFeeIds.Contains(d.Id));
        var dueFeesList = await asyncExecuter.ToListAsync(dueFeesQuery);

        var dueFeeMap = dueFeesList.ToDictionary(d => d.Id, d => d.FeeHeadId);

        // Group by Month -> FeeHeadId -> Amount
        var groupedData = new Dictionary<int, Dictionary<Guid, double>>();

        // Precompute allocations grouped by metadata ID
        var allocationsByTx = allocationsList.GroupBy(a => a.FeeRecordMetadataId).ToDictionary(g => g.Key, g => g.ToList());
        var paymentsByTx = paymentsList.GroupBy(p => p.FeeRecordMetadataId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var txKvp in paymentsByTx)
        {
            var txId = txKvp.Key;
            var txPayments = txKvp.Value;

            // Take the month from the first payment of the transaction (they should all be on the same date typically)
            var month = txPayments.First().PaymentDate.Month;

            if (!allocationsByTx.TryGetValue(txId, out var txAllocations)) continue;

            if (!groupedData.ContainsKey(month)) groupedData[month] = new();

            // Total amount paid in this transaction
            var txTotalPaid = txPayments.Sum(p => p.Amount);
            if (txTotalPaid == 0) continue;

            foreach (var allocation in txAllocations)
            {
                if (!dueFeeMap.TryGetValue(allocation.DueFeeId, out var feeHeadId)) continue;
                if (!targetFeeHeadIds.Contains(feeHeadId)) continue;

                if (!groupedData[month].ContainsKey(feeHeadId)) groupedData[month][feeHeadId] = 0;

                // Here we just take the raw allocation amount, because we are not splitting by Cash/Gateway.
                // The allocation tells us exactly how much of the transaction went to this FeeHead.
                // Note: The allocation amount might exceed the payments if partial payment was made and some allocations were dropped,
                // but usually Sum(Allocation.Amount) == txTotalPaid. Let's just use Allocation.Amount.
                groupedData[month][feeHeadId] += allocation.Amount;
            }
        }

        var rows = new List<Dictionary<string, string?>>();
        foreach (var month in targetMonths)
        {
            if (!groupedData.TryGetValue(month, out var feeHeadData))
            {
                feeHeadData = new();
            }

            var monthName = new DateTime(2000, month, 1).ToString("MMMM");
            var row = new Dictionary<string, string?>
            {
                { "Month", monthName },
                { "MonthIndex", month.ToString() }
            };

            double rowTotal = 0;

            foreach (var feeHead in targetFeeHeads)
            {
                if (feeHeadData.TryGetValue(feeHead.Id, out var amount))
                {
                    row[$"FH_{feeHead.Id}"] = Math.Ceiling(amount).ToString("N0");
                    rowTotal += amount;
                }
                else
                {
                    row[$"FH_{feeHead.Id}"] = "0.00";
                }
            }

            row["Total"] = Math.Ceiling(rowTotal).ToString("N0");
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
