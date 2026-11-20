using System;
using System.Linq.Expressions;

namespace ShikshaMitra.Finance.ExternalReceivables;

public static class FinanceExternalWaiverExpressions
{
	public static readonly Expression<Func<FinanceExternalWaiverBalanceInput, bool>> Valid = value =>
		value.Year == value.ChargeYear && value.Student == value.ChargeStudent &&
		value.Employee == value.ChargeEmployee && value.Reversed <= value.Effective;
	private static readonly Func<FinanceExternalWaiverBalanceInput, bool> ValidValue = Valid.Compile();
	public static bool Matches(FinanceExternalWaiverBalanceInput value) => ValidValue(value);
}
