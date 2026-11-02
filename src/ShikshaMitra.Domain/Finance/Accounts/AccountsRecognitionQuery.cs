using System.Linq;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Fees.FeeRefund;

namespace ShikshaMitra.Finance.Accounts;

/// <summary>
/// None retains source/payment evidence for cash, reconciliation and replay, but contributes
/// no Accounts income or expense. Keep this separate from payment confirmation.
/// </summary>
public static class AccountsRecognitionQuery
{
    public static IQueryable<Transaction> ExcludeNone(IQueryable<Transaction> entries, IQueryable<TransactionCategory> categories) =>
        entries.Where(entry =>
            (entry.CategoryNameSnapshot == null || entry.CategoryNameSnapshot.Trim().ToLower() != "none") &&
            !categories.Any(category => category.Id == entry.CategoryId && category.Name.Trim().ToLower() == "none"));

    public static IQueryable<FeeRefundRequest> ExcludeNone(IQueryable<FeeRefundRequest> refunds, IQueryable<TransactionCategory> categories) =>
        refunds.Where(refund =>
            (refund.ExpenseCategoryName == null || refund.ExpenseCategoryName.Trim().ToLower() != "none") &&
            !categories.Any(category => category.Id == refund.ExpenseCategoryId && category.Name.Trim().ToLower() == "none"));
}
