using System;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Domain.Finance.Policies;

public class RefundPolicyCalculationService : DomainService
{
	public RefundPolicyCalculationResult Calculate(RefundPolicySnapshot policy, RefundPolicyCalculationInput input)
	{
		if (input.SettledPaidAmount < 0 || input.PriorProcessedRefund < 0 ||
			input.ActiveReservation < 0 || input.StudentYearRefundedOrReserved < 0)
			throw new BusinessException(RefundPolicyErrorCodes.InvalidSimulationInput);

		var remainingEligible = Math.Max(0m,
			input.SettledPaidAmount - input.PriorProcessedRefund - input.ActiveReservation);
		decimal rawRefund = policy.AmountMode switch
		{
			RefundAmountMode.RemainingPaidAmount => remainingEligible,
			RefundAmountMode.ManualUpToEligible when input.RequestedAmount is > 0 => input.RequestedAmount.Value,
			RefundAmountMode.ManualUpToEligible => throw new BusinessException(RefundPolicyErrorCodes.InputNotAllowed),
			RefundAmountMode.FixedAmount => policy.FixedAmount ?? 0m,
			RefundAmountMode.Percentage => remainingEligible * (policy.PercentageValue ?? 0m) / 100m,
			_ => 0m
		};

		var boundedRaw = Math.Min(rawRefund, remainingEligible);
		var deduction = policy.DeductionType switch
		{
			RefundDeductionType.FixedAmount => policy.DeductionValue ?? 0m,
			RefundDeductionType.Percentage => boundedRaw * (policy.DeductionValue ?? 0m) / 100m,
			_ => 0m
		};
		if (policy.MaximumDeduction.HasValue)
			deduction = Math.Min(deduction, policy.MaximumDeduction.Value);
		deduction = Math.Min(deduction, boundedRaw);

		var afterDeduction = Math.Max(0m, boundedRaw - deduction);
		var requestCap = policy.MaximumPerRequest ?? decimal.MaxValue;
		var studentYearCap = policy.MaximumPerStudentYear.HasValue
			? Math.Max(0m, policy.MaximumPerStudentYear.Value - input.StudentYearRefundedOrReserved)
			: decimal.MaxValue;
		var hardCeiling = Math.Min(remainingEligible, Math.Min(requestCap, studentYearCap));
		var capped = Math.Min(afterDeduction, hardCeiling);
		var finalRefund = ShikshaMitra.Finance.WholeRupeeAmounts.RoundUpWithin(capped, hardCeiling);

		return new RefundPolicyCalculationResult(
			input.SettledPaidAmount,
			input.PriorProcessedRefund,
			input.ActiveReservation,
			remainingEligible,
			rawRefund,
			deduction,
			Math.Max(0m, afterDeduction - capped),
			finalRefund);
	}

	private static decimal ApplyRounding(decimal value, RoundingMethodType method) =>
		ShikshaMitra.Finance.WholeRupeeAmounts.RoundUp(value);
}

public sealed record RefundPolicyCalculationInput(
	decimal SettledPaidAmount,
	decimal PriorProcessedRefund,
	decimal ActiveReservation,
	decimal StudentYearRefundedOrReserved,
	decimal? RequestedAmount);

public sealed record RefundPolicyCalculationResult(
	decimal SettledPaidAmount,
	decimal PriorProcessedRefund,
	decimal ActiveReservation,
	decimal RemainingEligible,
	decimal RawRefund,
	decimal DeductionAmount,
	decimal CapAdjustment,
	decimal FinalRefund);
