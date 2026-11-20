using System;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed class FinanceExternalWaiverBalanceInput
{
	public Guid ChargeYear { get; init; }
	public Guid? ChargeStudent { get; init; }
	public Guid? ChargeEmployee { get; init; }
	public Guid Year { get; init; }
	public Guid? Student { get; init; }
	public Guid? Employee { get; init; }
	public decimal Effective { get; init; }
	public decimal Reversed { get; init; }
}
