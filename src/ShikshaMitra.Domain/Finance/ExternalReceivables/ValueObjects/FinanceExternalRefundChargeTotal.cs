using System;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed class FinanceExternalRefundChargeTotal
{
	public Guid ChargeId { get; init; }
	public decimal Amount { get; init; }
	public int InvalidCount { get; init; }
}
