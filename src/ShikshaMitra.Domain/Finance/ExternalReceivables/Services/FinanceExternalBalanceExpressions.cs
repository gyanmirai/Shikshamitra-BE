using System;
using System.Linq.Expressions;
using ShikshaMitra.Finance.Fees.Charges;

namespace ShikshaMitra.Finance.ExternalReceivables;

// Expressions are shared by provider queries and the existing calculator.
public static class FinanceExternalBalanceExpressions
{
	public static readonly Expression<Func<FinanceExternalBalanceInput, bool>> Structure = value =>
		value.Accepted == value.Settled + value.Pending && value.Applied >= value.Reversed &&
		value.Accepted + value.Applied - value.Reversed <= value.Original;
	public static readonly Expression<Func<FinanceExternalBalanceInput, bool>> StoredRange = value =>
		value.Original > 0m && value.Original <= FinanceChargeAmountRules.MaximumAmount &&
		value.Accepted >= 0m && value.Accepted <= FinanceChargeAmountRules.MaximumAmount &&
		value.Settled >= 0m && value.Settled <= FinanceChargeAmountRules.MaximumAmount &&
		value.Pending >= 0m && value.Pending <= FinanceChargeAmountRules.MaximumAmount &&
		value.Applied >= 0m && value.Applied <= FinanceChargeAmountRules.MaximumAmount &&
		value.Reversed >= 0m && value.Reversed <= FinanceChargeAmountRules.MaximumAmount;
	public static readonly Expression<Func<FinanceExternalBalanceInput, decimal>> NetWaived = value => value.Applied - value.Reversed;
	public static readonly Expression<Func<FinanceExternalBalanceInput, decimal>> Collectible = value =>
		value.Status == ChargeStatus.Approved ? value.Original - value.Accepted - (value.Applied - value.Reversed) : 0m;
	public static readonly Expression<Func<FinanceExternalBalanceInput, decimal>> FinancialOutstanding = value =>
		value.Status == ChargeStatus.Approved ? value.Original - value.Settled - (value.Applied - value.Reversed) : 0m;

	private static readonly Func<FinanceExternalBalanceInput, bool> StructureValue = Structure.Compile();
	private static readonly Func<FinanceExternalBalanceInput, bool> RangeValue = StoredRange.Compile();
	private static readonly Func<FinanceExternalBalanceInput, decimal> NetWaivedValue = NetWaived.Compile();
	private static readonly Func<FinanceExternalBalanceInput, decimal> CollectibleValue = Collectible.Compile();
	private static readonly Func<FinanceExternalBalanceInput, decimal> OutstandingValue = FinancialOutstanding.Compile();
	public static bool HasValidStructure(FinanceExternalBalanceInput value) => StructureValue(value);
	public static bool HasStoredRange(FinanceExternalBalanceInput value) => RangeValue(value);
	public static decimal Waived(FinanceExternalBalanceInput value) => NetWaivedValue(value);
	public static decimal Available(FinanceExternalBalanceInput value) => CollectibleValue(value);
	public static decimal Unsettled(FinanceExternalBalanceInput value) => OutstandingValue(value);
}
