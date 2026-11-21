using ShikshaMitra.Finance.Fees.Charges;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed class FinanceExternalBalanceInput
{
	public decimal Original { get; init; }
	public decimal Accepted { get; init; }
	public decimal Settled { get; init; }
	public decimal Pending { get; init; }
	public decimal Applied { get; init; }
	public decimal Reversed { get; init; }
	public ChargeStatus Status { get; init; }
}
