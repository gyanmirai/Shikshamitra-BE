using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Accounts.Activity;

/// <summary>
/// Read-only source activity. A final receipt or refund subledger entry is evidence
/// of a Fees action, not a General Accounts journal posting.
/// </summary>
[RemoteService(false)]
public class AccountsActivityAppService(
    IRepository<FeePaymentReceipt, Guid> receiptRepository,
    IRepository<FeeRecordMetadata, Guid> collectionRepository,
    IRepository<FeeRefundAccountingEntry, Guid> refundEntryRepository,
    IRepository<FeeRefundRequest, Guid> refundRepository,
    IRepository<Transaction, Guid> transactionRepository,
    IRepository<TransactionCategory, Guid> categoryRepository,
    IReadOnlyRepository<Student, Guid> studentRepository,
    FinanceScopeAccessService scopeAccessService) : ApplicationService, IAccountsActivityAppService
{
    public async Task<AccountsActivityPageDto> GetListAsync(AccountsActivityInput input)
    {
        if (input.AcademicYearId == Guid.Empty)
            throw new UserFriendlyException("Select an academic year to view Accounts activity.");
        var source = input.Source?.Trim().ToLowerInvariant();
        if (source is not (null or "collection" or "refund" or "income" or "expense"))
            throw new UserFriendlyException("Source must be collection, refund, income, or expense.");
        var search = input.Search?.Trim();
        if (search?.Length > 100)
            throw new UserFriendlyException("Search must be 100 characters or fewer.");

        var page = Math.Clamp(input.Page, 1, 1000);
        var pageSize = Math.Clamp(input.PageSize, 1, 100);
        var window = checked(page * pageSize);
        var canViewFees = await AuthorizationService.IsGrantedAsync(
            ShikshaMitraSubmodulePermissions.Finance.Accounts_Activity.View);
        var canViewIncome = await AuthorizationService.IsGrantedAsync(
            ShikshaMitraSubmodulePermissions.Finance.Accounts_IncomeTracking.View);
        var canViewExpense = await AuthorizationService.IsGrantedAsync(
            ShikshaMitraSubmodulePermissions.Finance.Accounts_ExpenseTracking.View);
        if (!canViewFees && !canViewIncome && !canViewExpense)
            await AuthorizationService.CheckAsync(
                ShikshaMitraSubmodulePermissions.Finance.Accounts_Activity.View);
        if ((source is "collection" or "refund") && !canViewFees)
            await AuthorizationService.CheckAsync(
                ShikshaMitraSubmodulePermissions.Finance.Accounts_Activity.View);
        if (source == "income" && !canViewIncome)
            await AuthorizationService.CheckAsync(
                ShikshaMitraSubmodulePermissions.Finance.Accounts_IncomeTracking.View);
        if (source == "expense" && !canViewExpense)
            await AuthorizationService.CheckAsync(
                ShikshaMitraSubmodulePermissions.Finance.Accounts_ExpenseTracking.View);
        if (!await scopeAccessService.IsAcademicContextAccessibleAsync(
                input.AcademicYearId, null, null, null, null))
            return new AccountsActivityPageDto
                { Page = page, PageSize = pageSize, TotalCount = 0, Items = [] };
        var scopedStudentIds = await scopeAccessService.GetScopedStudentIdsQueryAsync();
        var rows = new List<AccountsActivityRowDto>();
        var total = 0;

        if (canViewFees && (source is null or "collection"))
        {
            var receipts = await receiptRepository.GetQueryableAsync();
            var collections = await collectionRepository.GetQueryableAsync();
            var query = from receipt in receipts
                        join collection in collections on receipt.FeeCollectionTransactionId equals collection.Id
                        where receipt.AcademicYearId == input.AcademicYearId
                            && scopedStudentIds.Contains(receipt.StudentId)
                            && (collection.Status == FeeCollectionStatus.Paid ||
                                collection.Status == FeeCollectionStatus.Partial)
                            && (receipt.ReceiptKind == FeeReceiptKind.FinalFee ||
                                receipt.ReceiptKind == FeeReceiptKind.FinalCharge)
                        select new { receipt, collection };
            if (!string.IsNullOrEmpty(search))
                query = query.Where(x => x.receipt.ReceiptNumber.Contains(search) ||
                    x.collection.ReceiptNumber.Contains(search));
            total += await AsyncExecuter.CountAsync(query);
            var selected = await AsyncExecuter.ToListAsync(query
                .OrderByDescending(x => x.receipt.CreationTime).ThenByDescending(x => x.receipt.Id)
                .Take(window).Select(x => new
                {
                    x.receipt.Id, x.receipt.StudentId, x.receipt.ReceiptNumber,
                    x.receipt.TotalPaidAmount, x.receipt.CreationTime,
                    x.receipt.FeeCollectionTransactionId, x.collection.Status
                }));
            var canOpen = await AuthorizationService.IsGrantedAsync(
                ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View);
            rows.AddRange(selected.Select(x => new AccountsActivityRowDto
            {
                Id = x.Id,
                SourceId = x.FeeCollectionTransactionId,
                StudentId = x.StudentId,
                Source = "Collection",
                Reference = x.ReceiptNumber,
                SourceStatus = "Accepted",
                Amount = Convert.ToDecimal(x.TotalPaidAmount),
                OccurredAt = new DateTimeOffset(DateTime.SpecifyKind(x.CreationTime, DateTimeKind.Utc)),
                CanOpenSource = canOpen
            }));
        }

        if (canViewFees && (source is null or "refund"))
        {
            var entries = await refundEntryRepository.GetQueryableAsync();
            var refunds = await refundRepository.GetQueryableAsync();
            var query = from entry in entries
                        join refund in refunds on entry.RefundRequestId equals refund.Id
                        where refund.AcademicYearId == input.AcademicYearId
                            && scopedStudentIds.Contains(refund.StudentId)
                        select new { entry, refund };
            if (!string.IsNullOrEmpty(search))
                query = query.Where(x => x.refund.VoucherNumber.Contains(search) ||
                    (x.refund.OriginalReceiptNumber != null &&
                     x.refund.OriginalReceiptNumber.Contains(search)));
            total += await AsyncExecuter.CountAsync(query);
            var selected = await AsyncExecuter.ToListAsync(query
                .OrderByDescending(x => x.entry.PostedAt).ThenByDescending(x => x.entry.Id)
                .Take(window).Select(x => new
                {
                    x.entry.Id, x.entry.RefundRequestId, x.entry.Amount,
                    x.entry.IsCorrection, x.entry.PostedAt,
                    x.refund.StudentId, x.refund.VoucherNumber
                }));
            var canOpen = await AuthorizationService.IsGrantedAsync(
                ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View);
            rows.AddRange(selected.Select(x => new AccountsActivityRowDto
            {
                Id = x.Id,
                SourceId = x.RefundRequestId,
                StudentId = x.StudentId,
                Source = "Refund",
                Reference = x.VoucherNumber,
                SourceStatus = x.IsCorrection ? "Returned" : "Paid",
                Amount = x.IsCorrection ? x.Amount : -x.Amount,
                OccurredAt = x.PostedAt,
                CanOpenSource = canOpen
            }));
        }

        if ((canViewIncome && (source is null or "income")) ||
            (canViewExpense && (source is null or "expense")))
        {
            var transactions = await transactionRepository.GetQueryableAsync();
            var categories = await categoryRepository.GetQueryableAsync();
            var query = from transaction in transactions
                        join category in categories on transaction.CategoryId equals category.Id
                        where transaction.AcademicYearId == input.AcademicYearId
                            && ((transaction.TransactionType == TransactionType.Income && canViewIncome &&
                                    (source == null || source == "income")) ||
                                (transaction.TransactionType == TransactionType.Expense && canViewExpense &&
                                    (source == null || source == "expense")))
                        select new { transaction, category };
            if (!string.IsNullOrEmpty(search))
                query = query.Where(x => x.transaction.Name.Contains(search) ||
                    x.category.Name.Contains(search));
            total += await AsyncExecuter.CountAsync(query);
            var selected = await AsyncExecuter.ToListAsync(query
                .OrderByDescending(x => x.transaction.PaymentDate)
                .ThenByDescending(x => x.transaction.Id)
                .Take(window).Select(x => new
                {
                    x.transaction.Id, x.transaction.Name, x.transaction.Amount,
                    x.transaction.PaymentDate, x.transaction.TransactionType,
                    CategoryName = x.category.Name
                }));
            rows.AddRange(selected.Select(x => new AccountsActivityRowDto
            {
                Id = x.Id, SourceId = x.Id, StudentId = null,
                Source = x.TransactionType == TransactionType.Income ? "Income" : "Expense",
                Reference = x.Name, CategoryName = x.CategoryName,
                SourceStatus = "Recorded (legacy)",
                AccountingStatus = "Not posted",
                Amount = x.TransactionType == TransactionType.Income ? x.Amount : -x.Amount,
                OccurredAt = new DateTimeOffset(DateTime.SpecifyKind(x.PaymentDate, DateTimeKind.Utc)),
                CanOpenSource = true
            }));
        }

        var pageRows = rows.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var studentIds = pageRows.Where(x => x.StudentId.HasValue)
            .Select(x => x.StudentId!.Value).Distinct().ToList();
        if (studentIds.Count > 0)
        {
            var students = await studentRepository.GetQueryableAsync();
            var names = await AsyncExecuter.ToListAsync(students.Where(x => studentIds.Contains(x.Id))
                .Select(x => new { x.Id, x.FirstName, x.MiddleName, x.LastName }));
            var byId = names.ToDictionary(x => x.Id, x => string.Join(" ",
                new[] { x.FirstName, x.MiddleName, x.LastName }
                    .Where(value => !string.IsNullOrWhiteSpace(value))));
            foreach (var row in pageRows)
                if (row.StudentId.HasValue)
                    row.StudentName = byId.GetValueOrDefault(row.StudentId.Value) ?? string.Empty;
        }

        return new AccountsActivityPageDto
        {
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            Items = pageRows
        };
    }
}
