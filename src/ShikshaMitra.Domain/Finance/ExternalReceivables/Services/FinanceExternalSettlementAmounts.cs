using System;
using System.Collections.Generic;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Library;
using Volo.Abp;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed record FinanceExternalBalanceAmounts(decimal Original, decimal Accepted, decimal Settled,
	decimal PendingVerification, decimal NetWaived, decimal Collectible, decimal Unsettled);

public static class FinanceExternalSettlementAmounts
{
	public static decimal PaymentTotal(IEnumerable<decimal> amounts)
	{
		decimal total = 0m;
		var count = 0;
		foreach (var amount in amounts)
		{
			EnsureMoney(amount, positive: true);
			if (++count > 5) throw new BusinessException(LibraryFinanceErrorCodes.InvalidAmount);
			total += amount;
			EnsureMoney(total, positive: true);
		}
		if (count == 0) throw new BusinessException(LibraryFinanceErrorCodes.InvalidAmount);
		return total;
	}

	public static FinanceExternalBalanceAmounts Balance(decimal original, decimal accepted, decimal settled,
		decimal pendingVerification, decimal appliedWaiver, decimal reversedWaiver, ChargeStatus status)
	{
		EnsureMoney(original, positive: true);
		foreach (var amount in new[] { accepted, settled, pendingVerification, appliedWaiver, reversedWaiver })
			EnsureMoney(amount);
		var input = new FinanceExternalBalanceInput { Original = original, Accepted = accepted, Settled = settled,
			Pending = pendingVerification, Applied = appliedWaiver, Reversed = reversedWaiver, Status = status };
		if (!FinanceExternalBalanceExpressions.HasValidStructure(input))
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidBalance,
				"The recorded payment and waiver evidence requires Finance review.");
		return new(original, accepted, settled, pendingVerification, FinanceExternalBalanceExpressions.Waived(input),
			FinanceExternalBalanceExpressions.Available(input), FinanceExternalBalanceExpressions.Unsettled(input));
	}

	public static void EnsureMoney(decimal amount, bool positive = false)
	{
		if (amount < 0m || positive && amount == 0m || amount > FinanceChargeAmountRules.MaximumAmount ||
			!FinanceChargeAmountRules.HasSupportedScale(amount))
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidAmount,
				"Use a supported amount in whole paise.");
	}
}
