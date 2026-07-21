namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>
/// The step-by-step outcome of LateFeeCalculationService.Calculate — every field here is
/// surfaced by the /simulate endpoint (Phase 2) so "why was this amount charged" is always
/// answerable from the response alone, without re-reading the policy configuration.
/// </summary>
public sealed record LateFeeCalculationResult(
	decimal LateFee,
	bool IsExempt,
	int EffectiveDaysLate,
	int EffectiveGracePeriodDays,
	string? ExceptionWindowApplied,
	string FeeHeadOverrideApplied,
	decimal AmountBeforeCap,
	string? CappedBy,
	string RoundingApplied,
	string Explanation)
{
	public static LateFeeCalculationResult Exempt(string explanation) =>
		new(0, true, 0, 0, null, "Inherit", 0, null, "None", explanation);
}
