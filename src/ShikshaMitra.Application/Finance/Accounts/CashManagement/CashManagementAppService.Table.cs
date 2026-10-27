using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ShikshaMitra.Finance.Common;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

public partial class CashManagementAppService
{
    private static readonly IReadOnlyDictionary<string, FinanceTableColumn> TableColumns =
        new Dictionary<string, FinanceTableColumn>
        {
            ["reference"] = new(FinanceTableValueType.Text),
            ["payerName"] = new(FinanceTableValueType.Text),
            ["bankLabel"] = new(FinanceTableValueType.Text),
            ["amount"] = new(FinanceTableValueType.Number),
            ["spentAmount"] = new(FinanceTableValueType.Number),
            ["depositedAmount"] = new(FinanceTableValueType.Number),
            ["inTransitAmount"] = new(FinanceTableValueType.Number),
            ["remainingAmount"] = new(FinanceTableValueType.Number),
            ["collectedAt"] = new(FinanceTableValueType.Date),
            ["status"] = new(FinanceTableValueType.Enum, Values: [0, 1])
        };

    private static readonly IReadOnlyDictionary<string, LambdaExpression> TableFields =
        new Dictionary<string, LambdaExpression>
        {
            ["reference"] = (Expression<Func<CashCollection, string>>)(x => x.Reference),
            ["payerName"] = (Expression<Func<CashCollection, string>>)(x => x.PayerName),
            ["bankLabel"] = (Expression<Func<CashCollection, string?>>)(x => x.BankLabel),
            ["amount"] = (Expression<Func<CashCollection, decimal>>)(x => x.Amount),
            ["spentAmount"] = (Expression<Func<CashCollection, decimal>>)(x => x.SpentAmount),
            ["depositedAmount"] = (Expression<Func<CashCollection, decimal>>)(x => x.DepositedAmount),
            ["inTransitAmount"] = (Expression<Func<CashCollection, decimal>>)(x => x.InTransitAmount),
            // Use the stored facts in SQL; RemainingAmount is a domain-only computed property.
            ["remainingAmount"] = (Expression<Func<CashCollection, decimal>>)(x => x.Amount - x.SpentAmount - x.DepositedAmount - x.InTransitAmount),
            ["collectedAt"] = (Expression<Func<CashCollection, DateTimeOffset>>)(x => x.CollectedAt),
            ["status"] = (Expression<Func<CashCollection, int>>)(x => x.Status),
            ["creationTime"] = (Expression<Func<CashCollection, DateTime>>)(x => x.CreationTime)
        };
}
