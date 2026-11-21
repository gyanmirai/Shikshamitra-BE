using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Audit;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;

namespace ShikshaMitra.Finance.Reports;

public class FinancePhaseOnePaymentReports(
    IRepository<FeeRecordMetadata, Guid> collections,
    IRepository<PaymentMethodRecord, Guid> payments,
    IRepository<FeePaymentReceipt, Guid> receipts,
    IRepository<ReceiptBook, Guid> books,
    IRepository<Standard, Guid> standards,
    IRepository<Division, Guid> divisions,
    IRepository<IdentityUser, Guid> users,
    FinanceReportPayerQueryService payerService,
    IAsyncQueryableExecuter queries) : ITransientDependency
{
    public async Task<FinanceReportPreviewDto> BuildAsync(FinanceReportRequestDto request,
        FinanceReportPreviewDto payload, FinanceReportExecutionScope scope, bool exportAllRows)
    {
        var filters = request.Filters;
        var yearId = filters.AcademicYearIds!.Single();
        var payers = await payerService.GetPayersAsync(filters, scope, useHistoricalPlacement: true);
        var payerMap = payers.ToDictionary(p => p.Key);
        var studentIds = payers.Where(p => p.StudentId.HasValue && !p.FinanceFeeAccountId.HasValue)
            .Select(p => p.StudentId!.Value).ToArray();
        var accountIds = payers.Where(p => p.FinanceFeeAccountId.HasValue)
            .Select(p => p.FinanceFeeAccountId!.Value).ToArray();
        var txQuery = (await collections.GetQueryableAsync()).Where(t => t.TenantId == scope.TenantId &&
            t.AcademicYearId == yearId &&
            ((t.FinanceFeeAccountId.HasValue && Enumerable.Contains(accountIds, t.FinanceFeeAccountId.Value)) ||
            (!t.FinanceFeeAccountId.HasValue && t.StudentId.HasValue && Enumerable.Contains(studentIds, t.StudentId.Value))));
        var receiptQuery = (await receipts.GetQueryableAsync()).Where(r => r.TenantId == scope.TenantId &&
            r.AcademicYearId == yearId && txQuery.Any(t => t.Id == r.FeeCollectionTransactionId));
        var bookIds = filters.ReceiptBookIds?.ToArray() ?? [];
        var classIds = payers.Where(p => p.StandardId.HasValue).Select(p => p.StandardId!.Value).Distinct().ToArray();
        var divisionIds = payers.Where(p => p.DivisionId.HasValue).Select(p => p.DivisionId!.Value).Distinct().ToArray();
        var classMap = (await standards.GetListAsync(s => Enumerable.Contains(classIds, s.Id))).ToDictionary(s => s.Id, s => s.Name);
        var divisionMap = (await divisions.GetListAsync(d => Enumerable.Contains(divisionIds, d.Id))).ToDictionary(d => d.Id, d => d.Name);
        var rows = new FinancePhaseOneReportRows(payload);

        if (request.ReportType == FinanceReportTypes.ReceiptRegister)
        {
            if (filters.FromDate.HasValue && filters.ToDate.HasValue)
            {
                var from = DateOnly.FromDateTime(filters.FromDate.Value);
                var to = DateOnly.FromDateTime(filters.ToDate.Value);
                txQuery = txQuery.Where(t => receiptQuery.Any(r => r.FeeCollectionTransactionId == t.Id &&
                    r.ReceiptDate >= from && r.ReceiptDate <= to));
            }
        }
        else if (filters.FromDate.HasValue && filters.ToDate.HasValue && filters.DateBasis != "decision")
        {
            var start = InSchoolTime(filters.FromDate.Value.Date);
            var end = InSchoolTime(filters.ToDate.Value.Date.AddDays(1));
            var instruments = (await payments.GetQueryableAsync()).Where(p => p.TenantId == scope.TenantId &&
                p.PaymentDate >= start && p.PaymentDate < end);
            txQuery = txQuery.Where(t => instruments.Any(p => p.FeeRecordMetadataId == t.Id));
        }

        var txRows = await queries.ToListAsync(txQuery);
        var txIds = txRows.Select(t => t.Id).ToArray();
        var receiptRows = await queries.ToListAsync(receiptQuery.Where(r => Enumerable.Contains(txIds, r.FeeCollectionTransactionId)));
        var receiptGroups = receiptRows.ToLookup(r => r.FeeCollectionTransactionId);
        var bookMap = (await books.GetListAsync()).ToDictionary(b => b.Id, b => b.Name);
        if (request.ReportType == FinanceReportTypes.ReceiptRegister)
        {
            var txMap = txRows.ToDictionary(t => t.Id);
            var byId = receiptRows.ToDictionary(r => r.Id);
            var userMap = await GetActorNamesAsync(txRows.Select(t => t.CreatorId).Concat(receiptRows.Select(r => r.LastModifierId)));
            WriteReceipts(rows, receiptRows, txMap, byId, payerMap, classMap, divisionMap, bookMap, userMap, filters, bookIds);
        }
        else
        {
            var instrumentRows = await queries.ToListAsync((await payments.GetQueryableAsync())
                .Where(p => p.TenantId == scope.TenantId && Enumerable.Contains(txIds, p.FeeRecordMetadataId)));
            var instrumentGroups = instrumentRows.ToLookup(p => p.FeeRecordMetadataId);
            var actorMap = await GetActorNamesAsync(txRows.Select(t => t.CreatorId).Concat(receiptRows.Select(r => r.LastModifierId)));
            WriteInstruments(rows, txRows, instrumentGroups, receiptGroups, payerMap, classMap, divisionMap,
                bookMap, actorMap, filters, bookIds, request.ReportType == FinanceReportTypes.PaymentVerification);
        }
        return rows.Finish(request, exportAllRows);
    }

    private async Task<Dictionary<Guid, string>> GetActorNamesAsync(IEnumerable<Guid?> actors)
    {
        var ids = actors.OfType<Guid>().Distinct().ToArray();
        return (await queries.ToListAsync((await users.GetQueryableAsync()).Where(u => Enumerable.Contains(ids, u.Id))
            .Select(u => new { u.Id, u.Name, u.Surname, u.UserName })))
            .ToDictionary(u => u.Id, u => string.IsNullOrWhiteSpace(u.Name) ? u.UserName : $"{u.Name} {u.Surname}".Trim());
    }

    private static void WriteReceipts(FinancePhaseOneReportRows rows, IEnumerable<FeePaymentReceipt> receipts,
        Dictionary<Guid, FeeRecordMetadata> transactions, Dictionary<Guid, FeePaymentReceipt> byId,
        Dictionary<FinanceReportPayerKey, FinanceReportPayer> payers, Dictionary<Guid, string> classes,
        Dictionary<Guid, string> divisions, Dictionary<Guid, string> books, Dictionary<Guid, string> actors,
        FinanceReportFilterDto filters, Guid[] bookIds)
    {
        rows.Define(("Receipt", "Receipt no.", null), ("Date", "Receipt date", null), ("Book", "Receipt book", null),
            ("Payer", "Payer", null), ("Class", "Class", null), ("Division", "Division", null),
            ("Scope", "Receipt scope", null), ("Kind", "Document kind", null), ("Status", "Document status", null),
            ("Amount", "Document total (INR)", "money"), ("LinkedReceipt", "Linked receipt no.", null),
            ("CorrectionReference", "Correction reference", null), ("Reason", "Recorded reason", null),
            ("Actor", "Decision by", null));
        foreach (var receipt in receipts.OrderByDescending(r => r.ReceiptDate).ThenBy(r => r.ReceiptNumber).ThenBy(r => r.Id))
        {
            var transaction = transactions[receipt.FeeCollectionTransactionId];
            var payer = payers[FinanceReportPayerKey.From(transaction.StudentId, transaction.FinanceFeeAccountId)];
            var kind = IsProvisional(receipt) ? "Provisional" : "Official";
            var status = receipt.VerificationStatus switch
            {
                FeeReceiptVerificationStatus.PendingVerification => "Pending verification",
                FeeReceiptVerificationStatus.Verified => "Verified provisional",
                FeeReceiptVerificationStatus.Rejected => "Rejected",
                _ => transaction.Status == FeeCollectionStatus.Reversed ? "Reversed" : kind
            };
            if (!InDateRange(receipt.ReceiptDate, filters) ||
                (bookIds.Length > 0 && (!receipt.ReceiptBookId.HasValue || !bookIds.Contains(receipt.ReceiptBookId.Value))) ||
                !FinancePhaseOneReportRows.Matches(receipt.ReceiptNumber, filters.ReceiptNumber) ||
                !FinancePhaseOneReportRows.Matches(payer.DisplayName, filters.PayerSearch) ||
                (filters.ReceiptKind != null && filters.ReceiptKind != kind) ||
                (filters.Status != null && filters.Status != status))
                continue;
            var linked = byId.GetValueOrDefault(receipt.LinkedProvisionalReceiptId ?? receipt.LinkedFinalReceiptId ?? Guid.Empty);
            var corrected = status is "Rejected" or "Reversed";
            rows.Add(receipt.ReceiptNumber, receipt.ReceiptDate, books.GetValueOrDefault(receipt.ReceiptBookId ?? Guid.Empty),
                payer.DisplayName, classes.GetValueOrDefault(payer.StandardId ?? Guid.Empty),
                divisions.GetValueOrDefault(payer.DivisionId ?? Guid.Empty), receipt.ReceiptScope, kind, status,
                (decimal)receipt.TotalPaidAmount, linked?.ReceiptNumber,
                corrected ? transaction.ReceiptNumber : null, corrected ? receipt.Remarks : null,
                corrected ? actors.GetValueOrDefault(receipt.LastModifierId ?? Guid.Empty) : null);
        }
    }

    private static void WriteInstruments(FinancePhaseOneReportRows rows, IEnumerable<FeeRecordMetadata> transactions,
        ILookup<Guid, PaymentMethodRecord> instruments, ILookup<Guid, FeePaymentReceipt> receipts,
        Dictionary<FinanceReportPayerKey, FinanceReportPayer> payers, Dictionary<Guid, string> classes,
        Dictionary<Guid, string> divisions, Dictionary<Guid, string> books, Dictionary<Guid, string> actors,
        FinanceReportFilterDto filters, Guid[] bookIds, bool verification)
    {
        if (verification)
            rows.Define(("Collection", "Collection reference", null), ("Payer", "Payer", null),
                ("PaymentDate", "Payment date/time (IST)", null), ("Method", "Method", null), ("Bank", "Bank", null),
                ("Reference", "Masked instrument reference", null), ("Amount", "Instrument amount (INR)", "money"),
                ("Provisional", "Provisional receipts", null), ("Status", "Verification status", null),
                ("Age", "Days pending", "number"), ("AgeingBucket", "Pending age", null), ("Reviewer", "Reviewer", null),
                ("DecisionDate", "Decision date/time (IST)", null), ("Official", "Official receipts", null),
                ("Reason", "Recorded rejection reason", null));
        else
            rows.Define(("Collection", "Collection reference", null), ("PaymentDate", "Payment date/time (IST)", null),
                ("Payer", "Payer", null), ("PayerType", "Payer type", null), ("Class", "Class", null),
                ("Division", "Division", null), ("Method", "Method", null), ("Reference", "Masked reference", null),
                ("Amount", "Instrument amount (INR)", "money"), ("Status", "Verification status", null),
                ("Collector", "Collector", null), ("ReceiptBook", "Receipt books", null), ("Receipts", "Linked receipts", null));

        var today = DateTimeOffset.UtcNow.ToOffset(FinancePhaseOneReportRows.SchoolOffset).Date;
        var entries = from transaction in transactions
                      from instrument in instruments[transaction.Id]
                      orderby instrument.PaymentDate descending, instrument.Id
                      select (Transaction: transaction, Instrument: instrument);
        foreach (var (transaction, instrument) in entries)
        {
            var payer = payers[FinanceReportPayerKey.From(transaction.StudentId, transaction.FinanceFeeAccountId)];
            var documents = receipts[transaction.Id].ToArray();
            var cash = instrument.PaymentMethod == PaymentMethodEnum.Cash;
            if (verification && cash)
                continue;
            var provisional = documents.Where(IsProvisional).OrderBy(r => r.ReceiptNumber).ToArray();
            var official = documents.Where(r => !IsProvisional(r)).OrderBy(r => r.ReceiptNumber).ToArray();
            var hasSingleInstrument = instruments[transaction.Id].Count() == 1;
            var evidence = hasSingleInstrument && !cash ? provisional : [];
            var decision = evidence.Where(r => r.VerificationStatus is FeeReceiptVerificationStatus.Verified or FeeReceiptVerificationStatus.Rejected)
                .OrderByDescending(r => r.LastModificationTime).ThenBy(r => r.Id).FirstOrDefault();
            var status = GetInstrumentStatus(transaction, cash, evidence);
            var paymentDate = DateOnly.FromDateTime(instrument.PaymentDate.ToOffset(FinancePhaseOneReportRows.SchoolOffset).Date);
            DateTimeOffset? decisionInstant = decision?.LastModificationTime is { } changed
                ? new DateTimeOffset(AuditTimestampDisplay.NormalizeUtc(changed)) : null;
            DateOnly? decisionDate = decisionInstant.HasValue
                ? DateOnly.FromDateTime(decisionInstant.Value.ToOffset(FinancePhaseOneReportRows.SchoolOffset).Date) : null;
            var bank = instrument.BankLabelSnapshot ?? instrument.PaymentDetails switch
            {
                ChequeDetails cheque => cheque.BankName,
                DemandDraftDetails draft => draft.IssuingBankName,
                BankTransferDetails transfer => transfer.BankName,
                _ => null
            };
            var collector = actors.GetValueOrDefault(transaction.CreatorId ?? Guid.Empty);
            int? age = status == "Pending verification" ? Math.Max(0, (DateOnly.FromDateTime(today).DayNumber - paymentDate.DayNumber)) : null;
            var bucket = age.HasValue ? age.Value == 0 ? "Same day" : FinancePhaseOneReportRows.AgeingBucket(age.Value) : null;
            if (!InDateRange(filters.DateBasis == "decision" ? decisionDate : paymentDate, filters) ||
                !FinancePhaseOneReportRows.Matches(collector, filters.Collector) ||
                !FinancePhaseOneReportRows.Matches(bank, filters.Bank) ||
                (filters.PaymentMethod != null && filters.PaymentMethod != instrument.PaymentMethod.ToString()) ||
                (filters.Status != null && filters.Status != status) ||
                (filters.AgeingBucket != null && filters.AgeingBucket != bucket) ||
                (bookIds.Length > 0 && !documents.Any(r => r.ReceiptBookId.HasValue && bookIds.Contains(r.ReceiptBookId.Value))))
                continue;
            var reference = MaskReference(instrument.PaymentDetails switch
            {
                ChequeDetails cheque => cheque.ChequeNumber,
                DemandDraftDetails draft => draft.DDNumber,
                BankTransferDetails transfer => transfer.TransactionReferenceNumber,
                UpiDetails upi => upi.UpiTransactionId,
                _ => null
            });
            if (verification)
                rows.Add(transaction.Id, payer.DisplayName, instrument.PaymentDate, instrument.PaymentMethod, bank, reference,
                    instrument.Amount, string.Join(", ", evidence.Select(r => r.ReceiptNumber)), status, age, bucket,
                    actors.GetValueOrDefault(decision?.LastModifierId ?? Guid.Empty), decisionInstant,
                    string.Join(", ", official.Where(r => evidence.Any(p => p.Id == r.LinkedProvisionalReceiptId)).Select(r => r.ReceiptNumber)),
                    status == "Rejected" ? decision?.Remarks : null);
            else
                rows.Add(transaction.Id, instrument.PaymentDate, payer.DisplayName,
                    payer.FinanceFeeAccountId.HasValue ? "Applicant account" : "Student",
                    classes.GetValueOrDefault(payer.StandardId ?? Guid.Empty), divisions.GetValueOrDefault(payer.DivisionId ?? Guid.Empty),
                    instrument.PaymentMethod, reference, instrument.Amount, status, collector,
                    string.Join(", ", documents.Where(r => r.ReceiptBookId.HasValue)
                        .Select(r => books.GetValueOrDefault(r.ReceiptBookId!.Value)).OfType<string>().Distinct().Order()),
                    string.Join(", ", documents.Select(r => r.ReceiptNumber).Order()));
        }
    }

    internal static string? MaskReference(string? reference) => string.IsNullOrWhiteSpace(reference)
        ? null : reference.Length <= 4 ? "••••" : $"••••{reference[^4..]}";

    private static DateTimeOffset InSchoolTime(DateTime date) =>
        new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), FinancePhaseOneReportRows.SchoolOffset)
            .ToUniversalTime();

    private static bool IsProvisional(FeePaymentReceipt receipt) =>
        receipt.ReceiptKind is FeeReceiptKind.ProvisionalFee or FeeReceiptKind.ProvisionalCharge;

    private static string GetInstrumentStatus(FeeRecordMetadata transaction, bool cash, FeePaymentReceipt[] evidence)
    {
        if (evidence.Any(r => r.VerificationStatus == FeeReceiptVerificationStatus.Rejected))
            return "Rejected";
        if (transaction.Status == FeeCollectionStatus.Reversed)
            return "Reversed";
        if (cash)
            return "Cash";
        if (evidence.Length == 0)
            return "Unavailable";
        if (evidence.All(r => r.VerificationStatus == FeeReceiptVerificationStatus.Verified))
            return "Verified";
        return evidence.Any(r => r.VerificationStatus == FeeReceiptVerificationStatus.PendingVerification)
            ? "Pending verification" : "Unavailable";
    }

    private static bool InDateRange(DateOnly? date, FinanceReportFilterDto filters) =>
        !filters.FromDate.HasValue || (date.HasValue && date.Value >= DateOnly.FromDateTime(filters.FromDate.Value) &&
            (!filters.ToDate.HasValue || date.Value <= DateOnly.FromDateTime(filters.ToDate.Value)));
}
