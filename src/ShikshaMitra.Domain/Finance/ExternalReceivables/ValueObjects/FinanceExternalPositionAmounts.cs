namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed record FinanceExternalPositionAmounts(decimal Original, decimal Accepted, decimal Settled,
	decimal Pending, decimal NetWaived, decimal FinancialOutstanding, decimal Collectible, decimal Refunded);
