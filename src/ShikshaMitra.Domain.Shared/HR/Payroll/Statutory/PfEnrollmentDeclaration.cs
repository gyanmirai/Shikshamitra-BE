using System;

namespace ShikshaMitra.HR.Payroll.Statutory;

public sealed record PfEnrollmentDeclaration
{
	public string Membership { get; init; } = "UNKNOWN";
	public string? ConsentReference { get; init; }
}

public sealed record PfEligibilityDecision(string Status, decimal Wages, decimal CoverageCeiling,
	bool EnrollmentRequired, bool CanSave, string? IssueCode);
