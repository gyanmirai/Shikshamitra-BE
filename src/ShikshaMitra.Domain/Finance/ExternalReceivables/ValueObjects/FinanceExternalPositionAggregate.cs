namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed class FinanceExternalPositionAggregate
{
	public FinanceExternalPositionSource Source { get; init; } = null!;
	public FinanceExternalAcceptedEvidence Accepted { get; init; } = null!;
	public FinanceExternalBalanceInput Balance { get; init; } = null!;
	public bool InvalidAllocation { get; init; }
	public bool InvalidWaiver { get; init; }
	public bool InvalidRefund { get; init; }
	public decimal Refunded { get; init; }
	public string? CurrentReason { get; init; }
}
