using System;
using ShikshaMitra.Finance.Fees.Concessions;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Domain.Finance.Policies;

public class ConcessionPolicyCalculationService : DomainService
{
	public decimal Calculate(
		ConcessionPolicySnapshot policy,
		decimal eligibleBase,
		DiscountBasis? requestedBasis = null,
		decimal? requestedValue = null)
	{
		if (eligibleBase <= 0)
			return 0m;
		if (policy.ValueSource == ConcessionValueSource.PolicyFixed &&
			policy.CalculationMethod == ConcessionCalculationMethod.FullWaiver)
		{
			// A full waiver must match the live base exactly, including minor units.
			return eligibleBase;
		}

		decimal raw;
		if (policy.ValueSource == ConcessionValueSource.PolicyFixed)
		{
			raw = policy.CalculationMethod switch
			{
				ConcessionCalculationMethod.Percentage => eligibleBase * (policy.PercentageValue ?? 0m) / 100m,
				ConcessionCalculationMethod.FixedAmount => policy.FixedAmount ?? 0m,
				ConcessionCalculationMethod.FullWaiver => eligibleBase,
				_ => 0m
			};
		}
		else
		{
			if (requestedBasis is null || requestedValue is null or <= 0)
				throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed);
			var expectedBasis = policy.CalculationMethod switch
			{
				ConcessionCalculationMethod.Percentage => DiscountBasis.Percentage,
				ConcessionCalculationMethod.FixedAmount => DiscountBasis.FlatAmount,
				_ => throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed)
			};
			if (requestedBasis != expectedBasis)
				throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed);
			if (requestedBasis == DiscountBasis.FlatAmount &&
				decimal.Truncate(requestedValue.Value) != requestedValue.Value)
				throw new BusinessException(FinanceConcessionErrorCodes.WholeRupeeAmountRequired);
			if (requestedBasis == DiscountBasis.Percentage &&
				decimal.Round(requestedValue.Value, 2) != requestedValue.Value)
				throw new BusinessException(ConcessionPolicyErrorCodes.PercentagePrecision);
			if (requestedBasis == DiscountBasis.Percentage && requestedValue > 100)
				throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed);
			if (requestedBasis == DiscountBasis.Percentage &&
				policy.MaximumPercentage.HasValue && requestedValue > policy.MaximumPercentage)
				throw new BusinessException(ConcessionPolicyErrorCodes.LimitExceeded);
			if (requestedBasis == DiscountBasis.FlatAmount &&
				policy.MaximumFixedAmount.HasValue && requestedValue > policy.MaximumFixedAmount)
				throw new BusinessException(ConcessionPolicyErrorCodes.LimitExceeded);

			raw = requestedBasis == DiscountBasis.Percentage
				? eligibleBase * requestedValue.Value / 100m
				: requestedValue.Value;
		}

		var capped = Math.Min(raw, eligibleBase);
		var maximum = policy.ValueSource == ConcessionValueSource.RequestEnteredWithinLimits &&
			policy.CalculationMethod == ConcessionCalculationMethod.FixedAmount
			? Math.Min(eligibleBase, policy.MaximumFixedAmount ?? eligibleBase) : eligibleBase;
		var rounded = ShikshaMitra.Finance.WholeRupeeAmounts.RoundUpWithin(capped, maximum);

		// Rounding is presentation policy, not authority to exceed an obligation.
		return Math.Min(eligibleBase, Math.Max(0m, rounded));
	}
}
