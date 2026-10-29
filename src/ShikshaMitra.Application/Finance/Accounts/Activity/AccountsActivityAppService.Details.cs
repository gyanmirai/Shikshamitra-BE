using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Accounts.Activity;

public partial class AccountsActivityAppService
{
    public async Task<AccountsActivityDetailDto> GetDetailAsync(Guid academicYearId, string source, Guid id)
    {
        source = source.Trim().ToLowerInvariant();
        if (source is not ("income" or "expense" or "collection" or "refund"))
            throw new BusinessException(AccountsRegisterErrorCodes.InvalidFilter);
        var income = source is "income" or "collection";
        await CheckWorkspacePermissionAsync(income);
        var scopedAccounts = await scopeAccessService.GetScopedFeeAccountIdsQueryAsync();
        if (!await scopeAccessService.IsAcademicContextAccessibleAsync(academicYearId, null, null, null, null) &&
            !await AsyncExecuter.AnyAsync((await accountRepository.GetQueryableAsync())
                .Where(a => a.AcademicYearId == academicYearId && scopedAccounts.Contains(a.Id)))) throw new AbpAuthorizationException();
        var year = await academicYears.GetAsync(academicYearId);
        var detail = new AccountsActivityDetailDto { Id = id, AcademicYearId = academicYearId, AcademicYearName = year.Name, CanOpenSource = true };
        var transaction = await transactionRepository.FindAsync(id);
        if (transaction != null)
        {
            var expectedSource = transaction.IsAutomatic ? (income ? "collection" : "refund") : (income ? "income" : "expense");
            if (transaction.TenantId != CurrentTenant.Id || transaction.AcademicYearId != academicYearId || !transaction.IsConfirmed ||
                transaction.TransactionType != (income ? TransactionType.Income : TransactionType.Expense) || source != expectedSource)
                throw new AbpAuthorizationException();
            if (transaction.StudentId.HasValue || transaction.FinanceFeeAccountId.HasValue)
                await scopeAccessService.EnsureCollectionPayerAccessibleAsync(transaction.StudentId, transaction.FinanceFeeAccountId);
            await scopeAccessService.EnsurePooledCashTransactionAccessibleAsync(transaction);
            var category = await categoryRepository.FindAsync(transaction.CategoryId);
            detail.SourceId = transaction.SourceId ?? transaction.Id;
            detail.StudentId = transaction.StudentId;
            detail.FinanceFeeAccountId = transaction.FinanceFeeAccountId;
            detail.Reference = transaction.Name;
            detail.Source = source == "income" ? "Income" : source == "expense" ? "Expense" : income ? "Collection" : "Refund";
            detail.CategoryId = transaction.CategoryId;
            detail.CategoryName = transaction.CategoryNameSnapshot ?? category?.Name ?? "Unavailable category";
            detail.BankAccountId = transaction.BankAccountId;
            detail.BankLabel = transaction.BankLabelSnapshot ?? "";
            detail.Amount = transaction.Amount;
            detail.OccurredAt = Utc(transaction.PaymentDate);
            detail.SourceStatus = transaction.Amount < 0 ? (income ? "Reversed" : "Returned") : income ? "Received" : "Paid";
            detail.Notes = transaction.Notes;
            if (!transaction.IsAutomatic)
            {
                transaction.ExtraProperties.TryGetValue("PaymentReference", out var reference);
                transaction.ExtraProperties.TryGetValue("Counterparty", out var counterparty);
                detail.Counterparty = counterparty?.ToString();
                detail.Payments.Add(new(((PaymentMethodEnum)transaction.PaymentMethod).ToString(), transaction.Amount,
                    detail.OccurredAt, detail.BankLabel, reference?.ToString()));
            }
            else
            {
                var all = await transactionRepository.GetListAsync(t => t.SourceId == transaction.SourceId && t.IsConfirmed &&
                    t.AcademicYearId == academicYearId && t.StudentId == transaction.StudentId &&
                    t.FinanceFeeAccountId == transaction.FinanceFeeAccountId && t.TransactionType == transaction.TransactionType);
                detail.CategoryAmounts = all.Select(t => new AccountsCategoryAmountDto(t.CategoryNameSnapshot ?? "Unavailable",
                    t.Amount, t.BankLabelSnapshot)).ToList();
            }
        }
        else if (source == "collection")
        {
            var receipt = await receiptRepository.GetAsync(id);
            if (receipt.TenantId != CurrentTenant.Id || receipt.AcademicYearId != academicYearId ||
                receipt.ReceiptKind is not (FeeReceiptKind.FinalFee or FeeReceiptKind.FinalCharge)) throw new AbpAuthorizationException();
            await scopeAccessService.EnsureCollectionPayerAccessibleAsync(receipt.StudentId, receipt.FinanceFeeAccountId);
            // Receipt evidence stays available in Receipts. Modern register entries
            // must be opened through their own confirmed Income rows, never through
            // this legacy receipt fallback (which would bypass cash approval).
            if (await transactionRepository.AnyAsync(t => t.SourceId == receipt.FeeCollectionTransactionId && t.SourceKey != null))
                throw new AbpAuthorizationException();
            detail.SourceId = receipt.FeeCollectionTransactionId;
            detail.StudentId = receipt.StudentId;
            detail.FinanceFeeAccountId = receipt.FinanceFeeAccountId;
            detail.Reference = receipt.ReceiptNumber;
            detail.Amount = (decimal)receipt.TotalPaidAmount;
            detail.OccurredAt = Utc(receipt.CreationTime);
            detail.Source = "Collection";
            detail.SourceStatus = "Received";
        }
        else if (source == "refund")
        {
            var entry = await refundEntryRepository.FindAsync(id);
            var refund = await refundRepository.GetAsync(entry?.RefundRequestId ?? id);
            if (entry == null && refund.Status is FeeRefundStatus.Processed or FeeRefundStatus.Reversed or FeeRefundStatus.Failed)
                entry = (await refundEntryRepository.GetListAsync(x => x.RefundRequestId == refund.Id)).OrderByDescending(x => x.PostedAt).FirstOrDefault();
            if (refund.TenantId != CurrentTenant.Id || refund.AcademicYearId != academicYearId ||
                (entry == null && refund.Status is not (FeeRefundStatus.Approved or FeeRefundStatus.Processing)))
                throw new AbpAuthorizationException();
            await scopeAccessService.EnsureCollectionPayerAccessibleAsync(refund.StudentId, refund.FinanceFeeAccountId);
            detail.SourceId = refund.Id;
            detail.StudentId = refund.StudentId;
            detail.FinanceFeeAccountId = refund.FinanceFeeAccountId;
            detail.Source = "Refund";
            detail.Reference = refund.VoucherNumber;
            detail.CategoryId = refund.ExpenseCategoryId;
            detail.CategoryName = refund.ExpenseCategoryName ?? "";
            detail.BankAccountId = refund.PayingBankAccountId;
            detail.BankLabel = refund.PayingBankLabel ?? "";
            detail.Amount = entry == null ? refund.ApprovedAmount : entry.IsCorrection ? -entry.Amount : entry.Amount;
            detail.OccurredAt = entry?.PostedAt ?? Utc(refund.CreationTime);
            detail.SourceStatus = entry == null ? "Awaiting payout" : entry.IsCorrection ? "Returned" : "Paid";
        }
        else throw new AbpAuthorizationException();

        if (detail.Source == "Collection") await AddCollectionDetailsAsync(detail);
        if (detail.Source == "Refund") await AddRefundDetailsAsync(detail);
        if (detail.StudentId.HasValue)
        {
            // Project only register identity fields; entity auto-includes load the full student graph.
            var studentQuery = await studentRepository.GetQueryableAsync();
            var student = await AsyncExecuter.FirstAsync(studentQuery.Where(x => x.Id == detail.StudentId.Value)
                .Select(x => new { x.Id, x.FirstName, x.MiddleName, x.LastName, x.GrNumber, x.GrNo,
                    x.AcademicYearId, x.StandardId, x.DivisionId }));
            detail.StudentName = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
            detail.GrNumber = student.GrNumber ?? student.GrNo?.ToString();
            // Use this academic year's enrollment, never the student's current-year placement.
            var enrollmentQuery = await enrollments.GetQueryableAsync();
            var enrollment = await AsyncExecuter.FirstOrDefaultAsync(enrollmentQuery.Where(x => x.StudentId == student.Id &&
                x.AcademicYearId == academicYearId).OrderByDescending(x => x.IsActive).ThenByDescending(x => x.CreationTime).ThenByDescending(x => x.Id));
            var standardId = enrollment?.StandardId ?? (student.AcademicYearId == academicYearId ? student.StandardId : null);
            var divisionIdForYear = enrollment?.DivisionId ?? (student.AcademicYearId == academicYearId ? student.DivisionId : null);
            if (standardId is { } classId)
                detail.ClassName = await AsyncExecuter.FirstOrDefaultAsync((await standards.GetQueryableAsync()).Where(x => x.Id == classId).Select(x => x.Name));
            if (divisionIdForYear is { } divisionId)
                detail.DivisionName = await AsyncExecuter.FirstOrDefaultAsync((await divisions.GetQueryableAsync()).Where(x => x.Id == divisionId).Select(x => x.Name));
            detail.ClassDivision = detail.ClassName == null && detail.DivisionName == null ? "" :
                $"{detail.ClassName ?? "—"} - {detail.DivisionName ?? "—"}";
        }
        if (detail.FinanceFeeAccountId.HasValue)
        {
            var account = await accountRepository.GetAsync(detail.FinanceFeeAccountId.Value);
            if (account.AcademicYearId != academicYearId) throw new AbpAuthorizationException();
            detail.StudentName = account.ApplicantName;
        }
        return detail;
    }

    private async Task AddCollectionDetailsAsync(AccountsActivityDetailDto detail)
    {
        var collection = await collectionRepository.GetAsync(detail.SourceId);
        if (collection.AcademicYearId != detail.AcademicYearId || collection.StudentId != detail.StudentId ||
            collection.FinanceFeeAccountId != detail.FinanceFeeAccountId ||
            collection.Status is not (FeeCollectionStatus.Paid or FeeCollectionStatus.Partial or FeeCollectionStatus.Reversed)) throw new AbpAuthorizationException();
        detail.Notes = collection.Remarks;
        var lines = await allocations.GetListAsync(x => x.FeeRecordMetadataId == collection.Id);
        detail.Allocations = lines.Select(x => new AccountsAllocationDto(x.FeeHeadName ?? "Fee",
            x.InstallmentLabel, (decimal)x.Amount, x.CoverageStartDate, x.CoverageEndDate)).ToList();
        if (detail.Allocations.Count == 0)
        {
            var records = await feeRecords.GetQueryableAsync();
            var heads = await feeHeads.GetQueryableAsync();
            var legacy = await AsyncExecuter.ToListAsync(from paid in records join head in heads on paid.FeeHeadId equals head.Id
                where paid.FeeRecordMetadataId == collection.Id && paid.StudentId == collection.StudentId
                select new { head.Name, paid.Amount });
            detail.Allocations.AddRange(legacy.Select(x => new AccountsAllocationDto(x.Name, "Legacy fee payment", (decimal)x.Amount)));
        }
        var lateQuery = await lateFeeAllocations.GetQueryableAsync();
        var dueQuery = await dues.GetQueryableAsync();
        var lateLines = await AsyncExecuter.ToListAsync(from late in lateQuery join due in dueQuery on late.DueFeeId equals due.Id
            where late.FeeRecordMetadataId == collection.Id && due.StudentId == collection.StudentId
            select new { due.InstallmentName, late.Amount });
        detail.Allocations.AddRange(lateLines.Select(x => new AccountsAllocationDto("Late fee", x.InstallmentName, (decimal)x.Amount)));
        var chargeQuery = await chargeCollections.GetQueryableAsync();
        var chargeNames = await charges.GetQueryableAsync();
        var chargeLines = await AsyncExecuter.ToListAsync(from paid in chargeQuery
            join charge in chargeNames on paid.ChargeId equals charge.Id
            where paid.FeeRecordMetadataId == collection.Id && charge.StudentId == collection.StudentId
            select new { charge.ChargeName, paid.Amount });
        detail.Allocations.AddRange(chargeLines.Select(x => new AccountsAllocationDto(x.ChargeName, "Charge", (decimal)x.Amount)));
        var instruments = await paymentMethods.GetListAsync(x => x.FeeRecordMetadataId == collection.Id);
        detail.Payments = instruments.Select(x => new AccountsPaymentDto(x.PaymentMethod.ToString(), x.Amount, x.PaymentDate,
            x.BankLabelSnapshot, PaymentReference(x.PaymentDetails))).ToList();
    }

    private async Task AddRefundDetailsAsync(AccountsActivityDetailDto detail)
    {
        var refund = await refundRepository.FindWithLinesAsync(detail.SourceId) ?? throw new AbpAuthorizationException();
        if (refund.AcademicYearId != detail.AcademicYearId || refund.StudentId != detail.StudentId ||
            refund.FinanceFeeAccountId != detail.FinanceFeeAccountId) throw new AbpAuthorizationException();
        detail.OriginalReceiptNumber = refund.OriginalReceiptNumber;
        detail.RefundReason = refund.Reason;
        detail.ApprovalNotes = refund.ApproverNotes;
        detail.RefundStatus = refund.Status.ToString();
        detail.ApprovedPaymentMethod = refund.RefundMethod;
        detail.ProcessedAt = refund.ProcessedAt;
        detail.FailureReason = refund.FailureReason ?? refund.ReversalReason;
        var lines = refund.Lines;
        detail.Allocations = lines.Select(x => new AccountsAllocationDto(x.FeeHeadName, x.InstallmentLabel, x.ApprovedAmount)).ToList();
        var payments = refund.Settlements;
        detail.Payments = payments.OrderBy(x => x.AttemptNumber).Select(x => new AccountsPaymentDto(x.Method,
            x.Amount, x.SettlementDate, refund.PayingBankLabel, x.ExternalReference, x.EvidenceReference, x.Status.ToString())).ToList();
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static string? PaymentReference(PaymentDetails? details) => details switch
    {
        BankTransferDetails x => x.TransactionReferenceNumber,
        ChequeDetails x => x.ChequeNumber,
        DemandDraftDetails x => x.DDNumber,
        CreditCardDetails x => x.TransactionId,
        UpiDetails x => x.UpiTransactionId,
        OnlineGatewayDetails x => x.TransactionId,
        WalletDetails x => x.TransactionId,
        _ => null
    };
}
