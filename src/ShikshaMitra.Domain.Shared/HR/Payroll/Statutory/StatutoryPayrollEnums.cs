using System;

namespace ShikshaMitra.HR.Payroll.Statutory;

public static class StatutorySchemeCodes
{
	public const string EmployeeStateInsurance = "ESI";
	public const string ProvidentFund = "PF";
	public const string ProfessionalTax = "PT";

	public static bool IsSupported(string? value) =>
		string.Equals(value, EmployeeStateInsurance, StringComparison.OrdinalIgnoreCase) ||
		string.Equals(value, ProvidentFund, StringComparison.OrdinalIgnoreCase) ||
		string.Equals(value, ProfessionalTax, StringComparison.OrdinalIgnoreCase);
}

public enum StatutoryRoundingPolicy
{
	TwoDecimalsAwayFromZero = 0,
	NextHigherRupee = 1
}

public enum StatutoryEstablishmentStatus
{
	Draft = 0,
	Ready = 1,
	Active = 2,
	Suspended = 3
}

public enum StatutoryWageTreatment
{
	Unclassified = 0,
	Included = 1,
	SpecifiedExclusion = 2,
	OtherExclusion = 3,
	InKind = 4
}

public enum EmployeeStatutoryEnrollmentStatus
{
	PendingReview = 0,
	Covered = 1,
	NotCovered = 2,
	Exited = 3,
	RegistrationPending = 4
}

public enum StatutoryWageCeilingCategory
{
	Standard = 0,
	PersonWithDisability = 1
}

public enum StatutoryOverrideType
{
	ForceCovered = 0,
	ForceNotCovered = 1,
	EmployeeAmount = 2,
	EmployerAmount = 3
}
