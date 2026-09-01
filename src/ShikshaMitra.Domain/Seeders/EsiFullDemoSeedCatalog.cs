using ShikshaMitra.Common;
using ShikshaMitra.HR.Payroll.Statutory;
using System;
using System.Collections.Generic;

namespace ShikshaMitra.Seeders;

internal static class EsiFullDemoSeedCatalog
{
	internal const string JurisdictionCode = "IN-GJ";
	internal const string SyntheticRegistrationNumber = "DEMO-ESIC-DDVS-GJ-001";
	internal const string SyntheticEvidenceReference = "FULLDEMO-SYNTHETIC-ESI-CONFIGURATION-NOT-FOR-FILING";
	internal const string AdmissionsReceptionEmail = "admissions-reception@ddvs.com";
	internal const string AdmissionsCounselorEmail = "admissions-counselor@ddvs.com";
	internal const string ClerkOfficeEmail = "clerk-office@ddvs.com";
	internal static readonly DateTime RuleEffectiveFrom = new(2025, 11, 21);

	private static readonly IReadOnlyDictionary<string, SalaryScenario> SalaryScenarios =
		new Dictionary<string, SalaryScenario>(StringComparer.OrdinalIgnoreCase)
		{
			[AdmissionsReceptionEmail] = new(18_000m, SuppressVariablePay: true),
			[AdmissionsCounselorEmail] = new(20_000m, SuppressVariablePay: true),
			[ClerkOfficeEmail] = new(30_000m, SuppressVariablePay: false)
		};

	internal static readonly IReadOnlyDictionary<string, StatutoryWageTreatment> ComponentTreatments =
		new Dictionary<string, StatutoryWageTreatment>(StringComparer.OrdinalIgnoreCase)
		{
			["Basic Salary"] = StatutoryWageTreatment.Included,
			["House Rent Allowance (HRA)"] = StatutoryWageTreatment.SpecifiedExclusion,
			["Dearness Allowance (DA)"] = StatutoryWageTreatment.Included,
			["Transport Allowance"] = StatutoryWageTreatment.SpecifiedExclusion,
			["Medical Allowance"] = StatutoryWageTreatment.Included,
			["Special Allowance"] = StatutoryWageTreatment.Included,
			["Performance Bonus"] = StatutoryWageTreatment.Included,
			["Incentive"] = StatutoryWageTreatment.Included
		};

	internal static readonly IReadOnlyDictionary<string, string> ContractDescriptions =
		new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["Medical Allowance"] = "Fixed cash medical allowance paid as monthly contractual remuneration",
			["Performance Bonus"] = "Contractual monthly performance pay",
			["Incentive"] = "Contractual monthly incentive pay"
		};

	internal static SalaryScenario? FindSalaryScenario(string? email) =>
		!string.IsNullOrWhiteSpace(email) && SalaryScenarios.TryGetValue(email, out var scenario)
			? scenario
			: null;

	internal static bool IsSyntheticEstablishment(string? registrationNumber, string? evidenceReference) =>
		string.Equals(registrationNumber, SyntheticRegistrationNumber, StringComparison.Ordinal) &&
		string.Equals(evidenceReference, SyntheticEvidenceReference, StringComparison.Ordinal);

	internal static EnrollmentScenario ResolveEnrollmentScenario(
		string? email,
		EmployeeStatusType? employeeStatus,
		decimal statutoryWages,
		decimal standardWageCeiling,
		DateTime effectiveOn)
	{
		if (employeeStatus != EmployeeStatusType.Active)
		{
			return new EnrollmentScenario(
				EmployeeStatutoryEnrollmentStatus.Exited,
				null,
				null,
				null,
				"Synthetic FullDemo employee is inactive and excluded from current payroll.");
		}

		if (string.Equals(email, AdmissionsReceptionEmail, StringComparison.OrdinalIgnoreCase))
		{
			return new EnrollmentScenario(
				EmployeeStatutoryEnrollmentStatus.Covered,
				"DEMO-ESI-DDVS-RECEPTION-001",
				RuleEffectiveFrom,
				null,
				"Synthetic FullDemo covered employee below the standard wage ceiling.");
		}

		if (string.Equals(email, AdmissionsCounselorEmail, StringComparison.OrdinalIgnoreCase))
		{
			return new EnrollmentScenario(
				EmployeeStatutoryEnrollmentStatus.RegistrationPending,
				null,
				RuleEffectiveFrom,
				null,
				"Synthetic FullDemo employee awaiting an insurance number.");
		}

		if (string.Equals(email, ClerkOfficeEmail, StringComparison.OrdinalIgnoreCase))
		{
			var contributionPeriod = ResolveContributionPeriod(effectiveOn);
			return new EnrollmentScenario(
				EmployeeStatutoryEnrollmentStatus.Covered,
				"DEMO-ESI-DDVS-CLERK-001",
				contributionPeriod.Start < RuleEffectiveFrom ? RuleEffectiveFrom : contributionPeriod.Start,
				contributionPeriod.End,
				"Synthetic FullDemo coverage continues through the current contribution period.");
		}

		return statutoryWages <= standardWageCeiling
			? new EnrollmentScenario(
				EmployeeStatutoryEnrollmentStatus.RegistrationPending,
				null,
				RuleEffectiveFrom,
				null,
				"Synthetic FullDemo employee is within the wage ceiling and awaiting registration review.")
			: new EnrollmentScenario(
				EmployeeStatutoryEnrollmentStatus.NotCovered,
				null,
				null,
				null,
				"Synthetic FullDemo statutory wages exceed the standard wage ceiling.");
	}

	internal static ContributionPeriod ResolveContributionPeriod(DateTime effectiveOn)
	{
		var date = effectiveOn.Date;
		return date.Month is >= 4 and <= 9
			? new ContributionPeriod(new DateTime(date.Year, 4, 1), new DateTime(date.Year, 9, 30))
			: date.Month is >= 10 and <= 12
				? new ContributionPeriod(new DateTime(date.Year, 10, 1), new DateTime(date.Year + 1, 3, 31))
				: new ContributionPeriod(new DateTime(date.Year - 1, 10, 1), new DateTime(date.Year, 3, 31));
	}

	internal sealed record SalaryScenario(decimal GrossSalary, bool SuppressVariablePay);

	internal sealed record EnrollmentScenario(
		EmployeeStatutoryEnrollmentStatus Status,
		string? InsuranceNumber,
		DateTime? CoveredFrom,
		DateTime? ContributionPeriodEnd,
		string StatusReason);

	internal sealed record ContributionPeriod(DateTime Start, DateTime End);
}
