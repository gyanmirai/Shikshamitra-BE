using System.Collections.Generic;
using ShikshaMitra.Finance.Common;

namespace ShikshaMitra.Finance.Accounts;

public static class AccountsRegisterTable
{
    public static readonly IReadOnlyDictionary<string, FinanceTableColumn> Columns =
        new Dictionary<string, FinanceTableColumn>
        {
            ["reference"] = new(FinanceTableValueType.Text),
            ["studentIdentity"] = new(FinanceTableValueType.Text),
            ["classDivision"] = new(FinanceTableValueType.Text),
            ["counterparty"] = new(FinanceTableValueType.Text, Sortable: false),
            ["source"] = new(FinanceTableValueType.Text, Sortable: false),
            ["categoryName"] = new(FinanceTableValueType.Text),
            ["bankLabel"] = new(FinanceTableValueType.Text),
            ["amount"] = new(FinanceTableValueType.Number),
            ["occurredAt"] = new(FinanceTableValueType.Date),
            ["sourceStatus"] = new(FinanceTableValueType.Text)
        };
}
