using System;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>
/// A late-fee waiver inherits the late-fee fee-head scope and is limited only by the
/// current remaining late fee. Policy caps, expiry, and approval thresholds are retired.
/// </summary>
public class WaiverCalculationService : ITransientDependency
{
	public WaiverCalculationResult Calculate(CollectionPolicy policy, WaiverCalculationInput input)
	{
		if (!policy.EnableWaiver)
		{
			return new WaiverCalculationResult(0, false, false, "Waiver is disabled on this policy.");
		}

		if (input.FeeHeadId is { } feeHeadId && !policy.AppliesWaiverTo(feeHeadId))
		{
			return new WaiverCalculationResult(0, false, false, "This fee head is outside the policy's waiver scope.");
		}

		var effective = Math.Max(0, Math.Min(input.RequestedAmount, input.LateFeeAmount));

		var requiresApproval = policy.RequiresApproval && !input.WaiverPreApproved;

		var explanation = effective < input.RequestedAmount
			? $"₹{effective} (requested ₹{input.RequestedAmount}, limited to the remaining late fee)"
			: $"₹{effective} (requested amount)";

		return new WaiverCalculationResult(effective, false, requiresApproval, explanation);
	}
}

public sealed record WaiverCalculationInput(
	decimal RequestedAmount,
	decimal LateFeeAmount,
	DateTime AsOfDate,
	bool WaiverPreApproved = false,
	Guid? FeeHeadId = null);

public sealed record WaiverCalculationResult(
	decimal EffectiveWaiverAmount,
	bool IsExpired,
	bool RequiresApproval,
	string Explanation);
