using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Dashboard;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Dashboard;

[RemoteService(false)]
public class FinanceDashboardActivityReader(FinanceDashboardPayerScope payerScope, FinanceScopeAccessService financeScope,
    IReadOnlyRepository<FeePaymentReceipt, Guid> receipts, IReadOnlyRepository<FeeRecordMetadata, Guid> collections,
    IReadOnlyRepository<PaymentMethodRecord, Guid> paymentRecords,
    IReadOnlyRepository<Transaction, Guid> transactions, IReadOnlyRepository<TransactionCategory, Guid> categories)
    : ApplicationService
{
    internal async Task<FinanceDashboardActivityDto> ReadAsync(Guid yearId, DateOnly from, DateOnly to,
        bool canReadCollections, bool canReadIncome, bool canReadExpenses, bool yearWide,
        CancellationToken cancellationToken)
    {
        if (!canReadCollections && !canReadIncome && !canReadExpenses)
            return new(null, null, [], null, null, null, null, []);

        var scope = await payerScope.ReadAsync(yearId, cancellationToken);
        var students = scope.Placements.Select(row => row.StudentId);
        var accounts = scope.Accounts.Select(row => row.Id);

        decimal? confirmedFees = null;
        int? receiptCount = null;
        string? unavailable = null;
        var days = new List<FinanceDashboardDayDto>();
        IReadOnlyList<FinanceDashboardPaymentMethodDto> paymentMethods = [];
        if (canReadCollections)
        {
            var validCollections = (await collections.GetQueryableAsync())
                .Where(DashboardCollectionEvidence.ConfirmedTransaction)
                .Where(row => row.TenantId == CurrentTenant.Id && row.AcademicYearId == yearId);
            var confirmedReceipts = (await receipts.GetQueryableAsync())
                .Where(DashboardCollectionEvidence.ConfirmedReceipt)
                .Where(row => row.TenantId == CurrentTenant.Id && row.AcademicYearId == yearId &&
                    row.ReceiptDate >= from && row.ReceiptDate <= to &&
                    validCollections.Any(collection => collection.Id == row.FeeCollectionTransactionId &&
                        collection.StudentId == row.StudentId && collection.FinanceFeeAccountId == row.FinanceFeeAccountId) &&
                    (row.FinanceFeeAccountId.HasValue ? accounts.Contains(row.FinanceFeeAccountId.Value) :
                        row.StudentId.HasValue && students.Contains(row.StudentId.Value)));
            var rows = await AsyncExecuter.ToListAsync(confirmedReceipts
                .Select(row => new { row.Id, row.ReceiptDate, row.TotalPaidAmount, row.ReceiptScope, row.ExtraProperties }),
                cancellationToken);
            var receiptCollectionIds = confirmedReceipts.Select(row => row.FeeCollectionTransactionId);
            if (rows.Count > 0)
            {
                paymentMethods = await AsyncExecuter.ToListAsync((await paymentRecords.GetQueryableAsync())
                    .Where(payment => payment.TenantId == CurrentTenant.Id &&
                        receiptCollectionIds.Contains(payment.FeeRecordMetadataId))
                    .GroupBy(payment => payment.PaymentMethod)
                    .Select(group => new FinanceDashboardPaymentMethodDto(group.Key, group.Sum(payment => payment.Amount))),
                    cancellationToken);
            }
            var amountKey = nameof(FeePaymentReceipt.FeePaidAmount);
            if (rows.Any(row => row.ReceiptScope == FeeReceiptScope.Combined && !row.ExtraProperties.ContainsKey(amountKey)))
            {
                unavailable = "Some older receipts lack a fee breakdown. Choose a shorter period or review them in Finance.";
            }
            else
            {
                days = rows.GroupBy(row => row.ReceiptDate)
                    .OrderBy(group => group.Key).Select(group => new FinanceDashboardDayDto(group.Key,
                        group.Sum(row =>
                        {
                            var receipt = new FeePaymentReceipt(row.Id)
                                { ReceiptScope = row.ReceiptScope, TotalPaidAmount = row.TotalPaidAmount };
                            if (row.ExtraProperties.TryGetValue(amountKey, out var value)) receipt.ExtraProperties[amountKey] = value;
                            return receipt.FeePaidAmount;
                        }))).ToList();
                confirmedFees = days.Sum(row => row.Amount);
                receiptCount = rows.Count;
            }
        }

        decimal? income = null;
        decimal? expenses = null;
        int? accountEntryCount = null;
        if (canReadIncome || canReadExpenses)
        {
            var (start, end) = AccountsRegisterPeriod.Resolve(from.ToDateTime(TimeOnly.MinValue),
                to.ToDateTime(TimeOnly.MinValue), null, null);
            var periodTransactions = (await transactions.GetQueryableAsync()).Where(entry =>
                entry.PaymentDate >= start && entry.PaymentDate < end && entry.IsConfirmed &&
                (canReadIncome && entry.TransactionType == TransactionType.Income ||
                 canReadExpenses && entry.TransactionType == TransactionType.Expense) &&
                (entry.FinanceFeeAccountId.HasValue ? accounts.Contains(entry.FinanceFeeAccountId.Value) :
                    entry.StudentId.HasValue ? students.Contains(entry.StudentId.Value) : yearWide));
            var scoped = await financeScope.FilterPooledCashTransactionsAsync(periodTransactions, yearId);
            var categoryQuery = (await categories.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id);
            var totals = await AsyncExecuter.ToListAsync(AccountsRecognitionQuery.ExcludeNone(scoped, categoryQuery)
                .Where(entry => categoryQuery.Any(category => category.Id == entry.CategoryId))
                .GroupBy(entry => entry.TransactionType)
                .Select(group => new { Type = group.Key, Amount = group.Sum(entry => entry.Amount), Count = group.Count() }),
                cancellationToken);
            if (canReadIncome) income = totals.SingleOrDefault(row => row.Type == TransactionType.Income)?.Amount ?? 0m;
            if (canReadExpenses) expenses = totals.SingleOrDefault(row => row.Type == TransactionType.Expense)?.Amount ?? 0m;
            accountEntryCount = totals.Sum(row => row.Count);
        }

        return new(confirmedFees, receiptCount, days, income, expenses, accountEntryCount, unavailable, paymentMethods);
    }
}
