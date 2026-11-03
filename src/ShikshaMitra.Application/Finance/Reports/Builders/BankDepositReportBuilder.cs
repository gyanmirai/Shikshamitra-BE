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
public class BankDepositReportBuilder(
    IRepository<FeePaymentReceipt, Guid> receiptRepository,
    FinanceReportPayerQueryService payerService,
    IRepository<PaymentMethodRecord, Guid> paymentMethodRepository,
    IRepository<Standard, Guid> standardRepository,
    IAsyncQueryableExecuter asyncExecuter)
    : IFinanceReportBuilder, ITransientDependency
{
    public string ReportType => FinanceReportTypes.BankDepositReport;

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
            new() { Id = "SrNo", Label = "Sr. No." },
            new() { Id = "ReceiptNo", Label = "Receipt No." },
            new() { Id = "ReceiptDate", Label = "Reciept Date" },
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "Class", Label = "Class" },
            new() { Id = "ChequeRefNo", Label = "Cheque No / Reference No" },
            new() { Id = "Account", Label = "Account" },
            new() { Id = "Amount", Label = "Amount" }
        });

        // 6. Build Rows
        var rows = new List<Dictionary<string, string?>>();
        int srNo = 1;

        foreach (var receipt in receipts)
        {
            if (!payerMap.TryGetValue(FinanceReportPayerKey.From(receipt.StudentId, receipt.FinanceFeeAccountId), out var payer)) continue;

            string chequeRefNo = "";
            string account = "Unknown";

            if (paymentsByTx.TryGetValue(receipt.FeeCollectionTransactionId, out var txPayments))
            {
                var mainPayment = txPayments.FirstOrDefault(p => p.PaymentMethod != PaymentMethodEnum.Cash) ?? txPayments.FirstOrDefault();

                if (mainPayment != null)
                {
                    if (mainPayment.PaymentMethod == PaymentMethodEnum.Cash)
                    {
                        account = "Cash";
                    }
                    else
                    {
                        if (mainPayment.PaymentDetails is ChequeDetails cheque)
                        {
                            chequeRefNo = cheque.ChequeNumber;
                            account = cheque.BankName;
                        }
                        else if (mainPayment.PaymentDetails is DemandDraftDetails dd)
                        {
                            chequeRefNo = dd.DDNumber;
                            account = dd.IssuingBankName;
                        }
                        else if (mainPayment.PaymentDetails is BankTransferDetails bt)
                        {
                            chequeRefNo = bt.TransactionReferenceNumber;
                            account = bt.BankName;
                        }
                        else if (mainPayment.PaymentDetails is UpiDetails upi)
                        {
                            chequeRefNo = upi.UpiTransactionId;
                            account = upi.AppName ?? "UPI";
                        }
                        else
                        {
                            account = mainPayment.PaymentMethod.ToString();
                        }
                    }
                }
            }

            var stdName = payer.StandardId.HasValue && stdMap.TryGetValue(payer.StandardId.Value, out var sn) ? sn : "N/A";
            var payerName = payer.DisplayName;

            rows.Add(new Dictionary<string, string?>
            {
                { "SrNo", srNo.ToString() },
                { "ReceiptNo", receipt.ReceiptNumber },
                { "ReceiptDate", receipt.ReceiptDate.ToString("dd/MM/yyyy") },
                { "StudentName", payerName },
                { "Class", stdName },
                { "ChequeRefNo", chequeRefNo },
                { "Account", account },
                { "Amount", Math.Ceiling(receipt.TotalPaidAmount).ToString("N0") }
            });

            srNo++;
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;

        // Sort by Account, then Receipt Date
        var sortedRows = rows.OrderBy(r => r["Account"]).ThenBy(r => DateTime.ParseExact(r["ReceiptDate"]!, "dd/MM/yyyy", null)).ToList();

        // Re-assign Sr. No after sort
        for (int i = 0; i < sortedRows.Count; i++)
        {
            sortedRows[i]["SrNo"] = (i + 1).ToString();
        }

        payload.Rows = exportAllRows
            ? sortedRows
            : sortedRows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();

        return payload;
    }
}
