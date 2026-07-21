using System;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>
/// effective waiver = min(requested, maxWaiverAmount, lateFee × maxWaiverPercentage / 100),
/// with expiry check — per collection-policy-flow.md §6 and implementation plan Phase 2.
/// </summary>
public class WaiverCalculationService : ITransientDependency
{
	public WaiverCalculationResult Calculate(CollectionPolicy policy, WaiverCalculationInput input)
	{
		if (!policy.EnableWaiver)
		{
			return new WaiverCalculationResult(0, false, false, "Waiver is disabled on this policy.");
		}

		var isExpired = policy.WaiverExpiry is { } expiry && input.AsOfDate.Date > expiry.Date;
		if (isExpired)
		{
			return new WaiverCalculationResult(0, true, false, $"Waiver expired on {policy.WaiverExpiry:yyyy-MM-dd}.");
		}

		var effective = input.RequestedAmount;
		string? cappedBy = null;

		if (policy.MaximumWaiverAmount is { } maxAmount && maxAmount < effective)
		{
			effective = maxAmount;
			cappedBy = "maximumWaiverAmount";
		}

		if (policy.MaximumWaiverPercentage is { } maxPercentage)
		{
			var percentageCap = input.LateFeeAmount * maxPercentage / 100m;
			if (percentageCap < effective)
			{
				effective = percentageCap;
				cappedBy = "maximumWaiverPercentage";
			}
		}

		effective = Math.Max(0, effective);

		var requiresApproval = policy.RequiresApproval &&
			!input.WaiverPreApproved &&
			(policy.ApprovalThresholdAmount is null || effective > policy.ApprovalThresholdAmount);

		var explanation = cappedBy is not null
			? $"₹{effective} (requested ₹{input.RequestedAmount}, capped by {cappedBy})"
			: $"₹{effective} (full requested amount)";

		return new WaiverCalculationResult(effective, false, requiresApproval, explanation);
	}
}

public sealed record WaiverCalculationInput(
	decimal RequestedAmount,
	decimal LateFeeAmount,
	DateTime AsOfDate,
	bool WaiverPreApproved = false);

public sealed record WaiverCalculationResult(
	decimal EffectiveWaiverAmount,
	bool IsExpired,
	bool RequiresApproval,
	string Explanation);
