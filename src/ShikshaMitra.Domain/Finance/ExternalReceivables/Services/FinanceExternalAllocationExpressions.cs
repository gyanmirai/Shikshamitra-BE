using System;
using System.Linq.Expressions;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.ExternalReceivables;

public static class FinanceExternalAllocationExpressions
{
	public static readonly Expression<Func<FinanceExternalAllocationInput, bool>> Identity = value =>
		value.HeaderId.HasValue && value.RowTenant == value.ChargeTenant && value.HeaderTenant == value.ChargeTenant &&
		value.HeaderYear == value.ChargeYear && value.RowStudent == value.ChargeStudent &&
		value.RowEmployee == value.ChargeEmployee && value.HeaderStudent == value.ChargeStudent &&
		value.HeaderEmployee == value.ChargeEmployee && !value.HeaderAccount.HasValue &&
		(value.HeaderCurrency == null || value.HeaderCurrency == "INR");
	public static readonly Expression<Func<FinanceExternalAllocationInput, bool>> KnownStatus = value =>
		value.Status == FeeCollectionStatus.Reversed || value.Status == FeeCollectionStatus.Paid ||
		value.Status == FeeCollectionStatus.Partial || value.Status == FeeCollectionStatus.InReconciliation;
	public static readonly Expression<Func<FinanceExternalAllocationInput, decimal>> Accepted = value =>
		value.Status == FeeCollectionStatus.Reversed ? 0m : value.Amount;
	public static readonly Expression<Func<FinanceExternalAllocationInput, decimal>> Settled = value =>
		value.Status == FeeCollectionStatus.Paid || value.Status == FeeCollectionStatus.Partial ? value.Amount : 0m;
	public static readonly Expression<Func<FinanceExternalAllocationInput, decimal>> Pending = value =>
		value.Status == FeeCollectionStatus.InReconciliation ? value.Amount : 0m;
	private static readonly Func<FinanceExternalAllocationInput, bool> IdentityValue = Identity.Compile();
	private static readonly Func<FinanceExternalAllocationInput, bool> StatusValue = KnownStatus.Compile();
	private static readonly Func<FinanceExternalAllocationInput, decimal> AcceptedValue = Accepted.Compile();
	private static readonly Func<FinanceExternalAllocationInput, decimal> SettledValue = Settled.Compile();
	private static readonly Func<FinanceExternalAllocationInput, decimal> PendingValue = Pending.Compile();
	public static bool MatchesIdentity(FinanceExternalAllocationInput value) => IdentityValue(value);
	public static bool HasKnownStatus(FinanceExternalAllocationInput value) => StatusValue(value);
	public static decimal AcceptedAmount(FinanceExternalAllocationInput value) => AcceptedValue(value);
	public static decimal SettledAmount(FinanceExternalAllocationInput value) => SettledValue(value);
	public static decimal PendingAmount(FinanceExternalAllocationInput value) => PendingValue(value);
}
