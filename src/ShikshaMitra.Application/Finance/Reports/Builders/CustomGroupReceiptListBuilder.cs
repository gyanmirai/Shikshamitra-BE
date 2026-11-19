using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Finance.Reports.Builders;

[Volo.Abp.DependencyInjection.ExposeServices(typeof(IFinanceReportBuilder))]
public class CustomGroupReceiptListBuilder(
    IRepository<FeePaymentReceipt, Guid> receiptRepository,
    FinanceReportPayerQueryService payerService,
    IRepository<PaymentMethodRecord, Guid> paymentMethodRepository,
    IRepository<Standard, Guid> standardRepository,
    IAsyncQueryableExecuter asyncExecuter)
    : IFinanceReportBuilder, ITransientDependency
{
    public string ReportType => FinanceReportTypes.CustomGroupReceiptList;

    public async Task<FinanceReportPreviewDto> BuildAsync(FinanceReportRequestDto request, FinanceReportPreviewDto payload, FinanceReportExecutionScope executionScope, bool exportAllRows)
    {
        var receiptBookIds = request.Filters.ReceiptBookIds ?? new List<Guid>();

        var fromDate = request.Filters.FromDate;
        var toDate = request.Filters.ToDate;

        // Use canonical payer identities; applicant account evidence keeps its original scope.
        var payers = await payerService.GetPayersAsync(request.Filters, executionScope);

        if (payers.Count == 0) return payload;
        // 2. Fetch Receipts
        var receiptQuery = FinanceReportPayerQueryService.FilterReceipts(await receiptRepository.GetQueryableAsync(), payers)
            .WhereIf(receiptBookIds.Any(), r => r.ReceiptBookId.HasValue && receiptBookIds.Contains(r.ReceiptBookId.Value))
            .WhereIf(fromDate.HasValue, r => r.ReceiptDate >= DateOnly.FromDateTime(fromDate!.Value))
            .WhereIf(toDate.HasValue, r => r.ReceiptDate <= DateOnly.FromDateTime(toDate!.Value));

        var receipts = await asyncExecuter.ToListAsync(receiptQuery);
        if (!receipts.Any())
        {
            return payload;
        }

        // 3. Fetch associated PaymentMethodRecords to extract Bank / Cheque info
        var txIds = receipts.Select(r => r.FeeCollectionTransactionId).Distinct().ToList();
        var paymentsQuery = (await paymentMethodRepository.GetQueryableAsync())
            .Where(p => txIds.Contains(p.FeeRecordMetadataId));
        var payments = await asyncExecuter.ToListAsync(paymentsQuery);

        var paymentsByTx = payments.GroupBy(p => p.FeeRecordMetadataId).ToDictionary(g => g.Key, g => g.ToList());

        // 4. Resolve Standard Names
        var stdIds = payers.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
        var standards = await asyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync()).Where(s => stdIds.Contains(s.Id)));
        var stdMap = standards.ToDictionary(s => s.Id, s => s.Name);
        var payerMap = payers.ToDictionary(s => s.Key, s => s);

        // 5. Define Columns
        payload.Columns.AddRange(new List<FinanceReportColumnDto>
        {
            new() { Id = "ReceiptNo", Label = "Rec. No" },
            new() { Id = "Class", Label = "Class" },
            new() { Id = "RollNo", Label = "Roll No." },
            new() { Id = "GRNo", Label = "ID No." },
            new() { Id = "StudentName", Label = "Name" },
            new() { Id = "PayType", Label = "Pay Type" },
            new() { Id = "ChequeRefNo", Label = "Cheque No. / Reference No." },
            new() { Id = "BankName", Label = "Bank Name" },
            new() { Id = "Amount", Label = "Amount" }
        });

        // 6. Build Rows
        var rows = new List<Dictionary<string, string?>>();

        foreach (var receipt in receipts)
        {
            if (!payerMap.TryGetValue(FinanceReportPayerKey.From(receipt.StudentId, receipt.FinanceFeeAccountId), out var payer)) continue;

            string payType = "N/A";
            string chequeRefNo = "-";
            string bankName = "-";

            if (paymentsByTx.TryGetValue(receipt.FeeCollectionTransactionId, out var txPayments))
            {
                var mainPayment = txPayments.FirstOrDefault(p => p.PaymentMethod != PaymentMethodEnum.Cash) ?? txPayments.FirstOrDefault();

                if (mainPayment != null)
                {
                    payType = mainPayment.PaymentMethod.ToString();

                    if (mainPayment.PaymentMethod != PaymentMethodEnum.Cash)
                    {
                        if (mainPayment.PaymentDetails is ChequeDetails cheque)
                        {
                            chequeRefNo = cheque.ChequeNumber;
                            bankName = cheque.BankName;
                        }
                        else if (mainPayment.PaymentDetails is DemandDraftDetails dd)
                        {
                            chequeRefNo = dd.DDNumber;
                            bankName = dd.IssuingBankName;
                        }
                        else if (mainPayment.PaymentDetails is BankTransferDetails bt)
                        {
                            chequeRefNo = bt.TransactionReferenceNumber;
                            bankName = bt.BankName;
                        }
                        else if (mainPayment.PaymentDetails is UpiDetails upi)
                        {
                            chequeRefNo = upi.UpiTransactionId;
                            bankName = upi.AppName ?? "UPI";
                        }
                    }
                }
            }

            var stdName = payer.StandardId.HasValue && stdMap.TryGetValue(payer.StandardId.Value, out var sn) ? sn : "N/A";
            var payerName = payer.DisplayName;

            rows.Add(new Dictionary<string, string?>
            {
                { "ReceiptNo", receipt.ReceiptNumber },
                { "Class", stdName },
                { "RollNo", payer.RollNo?.ToString() ?? "N/A" },
                { "GRNo", payer.GrNo?.ToString() ?? "N/A" },
                { "StudentName", payerName },
                { "PayType", payType },
                { "ChequeRefNo", chequeRefNo },
                { "BankName", bankName },
                { "Amount", Math.Ceiling(receipt.TotalPaidAmount).ToString("N0") }
            });
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;

        var sortedRows = rows.OrderBy(r => r["ReceiptNo"]).ToList();

        payload.Rows = exportAllRows
            ? sortedRows
            : sortedRows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();

        return payload;
    }
}
