using System;

namespace ShikshaMitra.HR.Payroll.Statutory;

public static class PfEligibilityCalculator
{
	public static decimal CoverageCeiling(DateTime date) => date.Date >= new DateTime(2026, 9, 17) ? 25_000m : 15_000m;

	public static PfEligibilityDecision Calculate(DateTime effectiveOn, decimal wages, bool? establishmentCovered,
		PfEnrollmentDeclaration declaration, bool wasMember, bool enrolled)
	{
		var ceiling = CoverageCeiling(effectiveOn);
		PfEligibilityDecision Result(string status, bool required, string? issue = null) =>
			new(status, wages, ceiling, required, issue == null, issue);
		if (enrolled && wages <= 0m) return Result("NEEDS_REVIEW", false, "PF_WAGE_BASIS_REQUIRED");
		if (wasMember || declaration.Membership == "EXISTING_MEMBER")
			return Result("CONTINUING_MEMBER", true, enrolled ? null : "PF_ENROLLMENT_REQUIRED");
		if (establishmentCovered == false)
			return Result("NOT_APPLICABLE", false, enrolled ? "PF_ESTABLISHMENT_NOT_COVERED" : null);
		if (establishmentCovered == null || declaration.Membership != "NOT_MEMBER" || wages <= 0m)
			return Result("NEEDS_REVIEW", false, "PF_MEMBERSHIP_REVIEW_REQUIRED");
		if (wages <= ceiling)
			return Result("MANDATORY", true, enrolled ? null : "PF_ENROLLMENT_REQUIRED");
		return Result("VOLUNTARY", false, enrolled && string.IsNullOrWhiteSpace(declaration.ConsentReference)
			? "PF_CONSENT_REQUIRED" : null);
	}
}
