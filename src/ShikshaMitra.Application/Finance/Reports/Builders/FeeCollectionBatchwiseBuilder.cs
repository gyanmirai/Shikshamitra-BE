using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Finance.Reports.Builders;

[Volo.Abp.DependencyInjection.ExposeServices(typeof(IFinanceReportBuilder))]
public class FeeCollectionBatchwiseBuilder(
    IRepository<PaymentMethodRecord, Guid> paymentMethodRepository,
    IRepository<FeePaymentReceipt, Guid> feePaymentReceiptRepository,
    IRepository<AcademicYear, Guid> academicYearRepository,
    IRepository<ReceiptBook, Guid> receiptBookRepository,
    IAsyncQueryableExecuter asyncExecuter,
    FinanceReportPayerQueryService payerService)
    : IFinanceReportBuilder, ITransientDependency
{
    public string ReportType => FinanceReportTypes.FeeCollectionBatchwise;

    public async Task<FinanceReportPreviewDto> BuildAsync(FinanceReportRequestDto request, FinanceReportPreviewDto payload, FinanceReportExecutionScope executionScope, bool exportAllRows)
    {
        var payers = await payerService.GetPayersAsync(request.Filters, executionScope);
        var academicYearIds = request.Filters.AcademicYearIds ?? new List<Guid>();
        var targetAcademicYears = await asyncExecuter.ToListAsync(
            (await academicYearRepository.GetQueryableAsync())
            .WhereIf(academicYearIds.Any(), x => academicYearIds.Contains(x.Id))
            .OrderByDescending(x => x.StartDate)
        );

        // Define dynamic columns
        payload.Columns.Add(new() { Id = "ReceiptBook", Label = "Receipt Book" });
        foreach (var year in targetAcademicYears)
        {
            payload.Columns.Add(new() { Id = $"Cash_{year.Id}", Label = $"{year.Name} Cash" });
            payload.Columns.Add(new() { Id = $"Gateway_{year.Id}", Label = $"{year.Name} Payment Gateway" });
            payload.Columns.Add(new() { Id = $"Total_{year.Id}", Label = $"{year.Name} Total" });
        }

        var targetYearIds = targetAcademicYears.Select(x => x.Id).ToList();

        var receiptBooks = await asyncExecuter.ToListAsync(await receiptBookRepository.GetQueryableAsync());
        var rbMap = receiptBooks.ToDictionary(r => r.Id, r => r.Name);

        var receiptsQuery = FinanceReportPayerQueryService.FilterReceipts(await feePaymentReceiptRepository.GetQueryableAsync(), payers)
            .Where(r => targetYearIds.Contains(r.AcademicYearId));

        var receiptsList = await asyncExecuter.ToListAsync(receiptsQuery);

        var transactionIds = receiptsList.Select(r => r.FeeCollectionTransactionId).Distinct().ToList();
        var paymentsQuery = (await paymentMethodRepository.GetQueryableAsync())
            .Where(p => transactionIds.Contains(p.FeeRecordMetadataId));

        var paymentsList = await asyncExecuter.ToListAsync(paymentsQuery);

        var groupedData = new Dictionary<Guid, Dictionary<Guid, (double Cash, double Gateway, double Total)>>();
        var txPayments = paymentsList.GroupBy(p => p.FeeRecordMetadataId).ToDictionary(g => g.Key, g => g.ToList());
        var txReceipts = receiptsList.GroupBy(r => r.FeeCollectionTransactionId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var kvp in txReceipts)
        {
            var txId = kvp.Key;
            var txReceiptsList = kvp.Value;
            var txTotalPaid = txReceiptsList.Sum(r => r.TotalPaidAmount);

            if (txTotalPaid == 0) continue;

            if (!txPayments.TryGetValue(txId, out var payments)) continue;

            var cashTotal = (double)payments.Where(p => p.PaymentMethod == PaymentMethodEnum.Cash).Sum(p => p.Amount);
            var gatewayTotal = (double)payments.Where(p => p.PaymentMethod != PaymentMethodEnum.Cash).Sum(p => p.Amount);

            foreach (var receipt in txReceiptsList)
            {
                if (receipt.ReceiptBookId == null) continue;

                var rbId = receipt.ReceiptBookId.Value;
                var acYearId = receipt.AcademicYearId;

                if (!groupedData.ContainsKey(rbId)) groupedData[rbId] = new();
                if (!groupedData[rbId].ContainsKey(acYearId)) groupedData[rbId][acYearId] = (0, 0, 0);

                var ratio = receipt.TotalPaidAmount / txTotalPaid;
                var receiptCash = cashTotal * ratio;
                var receiptGateway = gatewayTotal * ratio;

                var existing = groupedData[rbId][acYearId];
                groupedData[rbId][acYearId] = (
                    existing.Cash + receiptCash,
                    existing.Gateway + receiptGateway,
                    existing.Total + receipt.TotalPaidAmount
                );
            }
        }

        var rows = new List<Dictionary<string, string?>>();
        foreach (var kvp in groupedData)
        {
            var rbId = kvp.Key;
            var yearData = kvp.Value;

            var row = new Dictionary<string, string?>
            {
                { "ReceiptBook", rbMap.GetValueOrDefault(rbId, "Unknown") }
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
        payload.Rows = exportAllRows
            ? rows.OrderBy(r => r["ReceiptBook"]).ToList()
            : rows.OrderBy(r => r["ReceiptBook"]).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();

        return payload;
    }
}
