using System;

namespace ShikshaMitra.HR.Payroll.Statutory;

public static class PfEligibilityCalculator
{
	public const decimal GrossEnrollmentCeiling = 25_000m;

	public static PfEligibilityDecision Calculate(decimal grossSalary, decimal contributionWages, bool? establishmentCovered,
		PfEnrollmentDeclaration declaration, bool wasMember, bool enrolled)
	{
		PfEligibilityDecision Result(string status, bool required, string? issue = null) =>
			new(status, contributionWages, GrossEnrollmentCeiling, required, issue == null, issue) { GrossSalary = grossSalary };
		if (enrolled && contributionWages <= 0m) return Result("NEEDS_REVIEW", false, "PF_WAGE_BASIS_REQUIRED");
		if (wasMember || declaration.Membership == "EXISTING_MEMBER")
			return Result("CONTINUING_MEMBER", true, enrolled ? null : "PF_ENROLLMENT_REQUIRED");
		if (establishmentCovered == false)
			return Result("NOT_APPLICABLE", false, enrolled ? "PF_ESTABLISHMENT_NOT_COVERED" : null);
		if (establishmentCovered == null || declaration.Membership != "NOT_MEMBER" || grossSalary <= 0m)
			return Result("NEEDS_REVIEW", false, "PF_MEMBERSHIP_REVIEW_REQUIRED");
		if (grossSalary <= GrossEnrollmentCeiling)
			return Result("MANDATORY", true, enrolled ? null : "PF_ENROLLMENT_REQUIRED");
		return Result("VOLUNTARY", false, enrolled && string.IsNullOrWhiteSpace(declaration.ConsentReference)
			? "PF_CONSENT_REQUIRED" : null);
	}
}