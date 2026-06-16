using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Finance.Reports.Builders;

[Volo.Abp.DependencyInjection.ExposeServices(typeof(IFinanceReportBuilder))]
public class FeeListChequeClearDateWiseBuilder(
    IRepository<FeePaymentReceipt, Guid> receiptRepository,
    IRepository<Student, Guid> studentRepository,
    IRepository<PaymentMethodRecord, Guid> paymentMethodRepository,
    IRepository<Standard, Guid> standardRepository,
    IAsyncQueryableExecuter asyncExecuter)
    : IFinanceReportBuilder, ITransientDependency
{
    public string ReportType => FinanceReportTypes.FeeListChequeClearDateWise;

    public async Task<FinanceReportPreviewDto> BuildAsync(FinanceReportRequestDto request, FinanceReportPreviewDto payload, bool exportAllRows)
    {
        var boardId = request.Filters.BoardId;
        var mediumId = request.Filters.MediumId;
        var organizationIds = request.Filters.OrganizationIds ?? new List<Guid>();
        var batchId = request.Filters.BatchId; // Maps to AcademicYearId
        var classIds = request.Filters.ClassIds ?? new List<Guid>();

        var fromDate = request.Filters.FromDate;
        var toDate = request.Filters.ToDate;

        // 1. Fetch Students
        var studentQuery = (await studentRepository.GetQueryableAsync())
            .WhereIf(boardId.HasValue, s => s.AcademicBoardId == boardId)
            .WhereIf(mediumId.HasValue, s => s.AcademicMediumId == mediumId)
            .WhereIf(organizationIds.Any(), s => s.AcademicMediumStageId.HasValue && organizationIds.Contains(s.AcademicMediumStageId.Value))
            .WhereIf(batchId.HasValue, s => s.AcademicYearId == batchId)
            .WhereIf(classIds.Any(), s => s.StandardId.HasValue && classIds.Contains(s.StandardId.Value));

        var students = await asyncExecuter.ToListAsync(studentQuery);
        var studentIds = students.Select(s => s.Id).ToList();

        if (!studentIds.Any())
        {
            return payload;
        }

        // 2. Fetch Receipts
        // We look for FinalFee receipts that have a linked provisional receipt (meaning it was a cleared non-cash instrument)
        var receiptQuery = (await receiptRepository.GetQueryableAsync())
            .Where(r => studentIds.Contains(r.StudentId))
            .Where(r => r.ReceiptKind == FeeReceiptKind.FinalFee && r.LinkedProvisionalReceiptId != null)
            .WhereIf(fromDate.HasValue, r => r.ReceiptDate >= DateOnly.FromDateTime(fromDate.Value))
            .WhereIf(toDate.HasValue, r => r.ReceiptDate <= DateOnly.FromDateTime(toDate.Value));

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

        // Group payments by Transaction Id
        var paymentsByTx = payments.GroupBy(p => p.FeeRecordMetadataId).ToDictionary(g => g.Key, g => g.ToList());

        // 4. Resolve Standard Names
        var stdIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
        var standards = await asyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync()).Where(s => stdIds.Contains(s.Id)));
        var stdMap = standards.ToDictionary(s => s.Id, s => s.Name);
        var studentMap = students.ToDictionary(s => s.Id, s => s);

        // 5. Define Columns
        payload.Columns.AddRange(new List<FinanceReportColumnDto>
        {
            new() { Id = "ClearDate", Label = "Clear Date" },
            new() { Id = "ReceiptNo", Label = "Rec. No" },
            new() { Id = "Class", Label = "Class" },
            new() { Id = "RollNo", Label = "Roll No." },
            new() { Id = "GRNo", Label = "ID No." },
            new() { Id = "StudentName", Label = "Name" },
            new() { Id = "PayType", Label = "Pay Type" },
            new() { Id = "ChequeRefNo", Label = "Cheq/Ref No." },
            new() { Id = "BankName", Label = "Bank Name" },
            new() { Id = "Amount", Label = "Amount" }
        });

        // 6. Build Rows
        var rows = new List<Dictionary<string, string?>>();

        foreach (var receipt in receipts)
        {
            if (!studentMap.TryGetValue(receipt.StudentId, out var student)) continue;

            string payType = "N/A";
            string chequeRefNo = "N/A";
            string bankName = "N/A";

            if (paymentsByTx.TryGetValue(receipt.FeeCollectionTransactionId, out var txPayments))
            {
                // Find the non-cash payment method
                var mainPayment = txPayments.FirstOrDefault(p => p.PaymentMethod != PaymentMethodEnum.Cash) ?? txPayments.FirstOrDefault();
                
                if (mainPayment != null)
                {
                    payType = mainPayment.PaymentMethod.ToString();
                    
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
                    }
                }
            }

            var stdName = student.StandardId.HasValue && stdMap.TryGetValue(student.StandardId.Value, out var sn) ? sn : "N/A";
            var studentName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Replace("  ", " ").Trim();

            rows.Add(new Dictionary<string, string?>
            {
                { "ClearDate", receipt.ReceiptDate.ToString("dd/MM/yyyy") },
                { "ReceiptNo", receipt.ReceiptNumber },
                { "Class", stdName },
                { "RollNo", student.RollNo?.ToString() ?? "N/A" },
                { "GRNo", student.GrNo?.ToString() ?? "N/A" },
                { "StudentName", studentName },
                { "PayType", payType },
                { "ChequeRefNo", chequeRefNo },
                { "BankName", bankName },
                { "Amount", receipt.TotalPaidAmount.ToString("N2") }
            });
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;

        // Sort by ClearDate, then ReceiptNo
        var sortedRows = rows.OrderBy(r => DateTime.ParseExact(r["ClearDate"]!, "dd/MM/yyyy", null)).ThenBy(r => r["ReceiptNo"]).ToList();

        payload.Rows = exportAllRows
            ? sortedRows
            : sortedRows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();

        return payload;
    }
}
