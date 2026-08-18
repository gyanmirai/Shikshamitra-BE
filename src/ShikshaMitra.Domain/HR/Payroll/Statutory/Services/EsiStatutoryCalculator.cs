using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Payroll.Statutory;

public sealed record StatutoryWageLine(
	Guid ComponentId,
	string Name,
	decimal Amount,
	StatutoryWageTreatment Treatment);

public sealed record EsiStatutoryCalculationResult(
	bool IsCovered,
	bool CanCalculate,
	string CoverageDecision,
	decimal TotalRemuneration,
	decimal IncludedWages,
	decimal SpecifiedExclusions,
	decimal ExclusionAddBack,
	decimal InKindWages,
	decimal StatutoryWages,
	decimal WageCeiling,
	decimal EmployeeContribution,
	decimal EmployerContribution,
	DateTime ContributionPeriodStart,
	DateTime ContributionPeriodEnd,
	IReadOnlyList<string> IssueCodes);

public static class EsiStatutoryCalculator
{
	public const string EstablishmentInactiveIssue = "ESI_ESTABLISHMENT_INACTIVE";
	public const string WageClassificationIssue = "ESI_WAGE_CLASSIFICATION_REQUIRED";
	public const string EnrollmentReviewIssue = "ESI_ENROLLMENT_REVIEW_REQUIRED";
	public const string InsuranceNumberIssue = "ESI_INSURANCE_NUMBER_REQUIRED";

	public static EsiStatutoryCalculationResult Calculate(
		DateTime payrollMonth,
		StatutoryRuleVersion rule,
		StatutoryEstablishment? establishment,
		EmployeeStatutoryEnrollment? enrollment,
		IReadOnlyList<StatutoryWageLine> wageLines,
		IReadOnlyList<StatutoryOverride>? overrides = null,
		bool assumeEstablishmentActive = false)
	{
		var monthStart = new DateTime(payrollMonth.Year, payrollMonth.Month, 1);
		var monthEnd = monthStart.AddMonths(1).AddDays(-1);
		var (periodStart, periodEnd) = GetContributionPeriod(monthStart);
		var issueCodes = new List<string>();
		var wageCeiling = enrollment?.WageCeilingCategory == StatutoryWageCeilingCategory.PersonWithDisability
			? rule.DisabilityWageCeiling
			: rule.StandardWageCeiling;

		if ((!assumeEstablishmentActive && establishment is not { Status: StatutoryEstablishmentStatus.Active }) ||
			establishment is null ||
			!establishment.BenefitsActiveFrom.HasValue ||
			establishment.BenefitsActiveFrom.Value.Date > monthEnd)
		{
			issueCodes.Add(EstablishmentInactiveIssue);
			return Empty("ESTABLISHMENT_INACTIVE", wageCeiling, periodStart, periodEnd, issueCodes);
		}

		var unclassified = wageLines.Where(x => x.Treatment == StatutoryWageTreatment.Unclassified).ToList();
		if (unclassified.Count > 0)
		{
			issueCodes.Add(WageClassificationIssue);
			return Empty("WAGE_CLASSIFICATION_REQUIRED", wageCeiling, periodStart, periodEnd, issueCodes);
		}

		var activeOverrides = (overrides ?? [])
			.Where(x => x.AppliesOn(monthStart))
			.OrderByDescending(x => x.CreationTime)
			.ToList();
		var coverageOverride = activeOverrides.FirstOrDefault(x =>
			x.OverrideType is StatutoryOverrideType.ForceCovered or StatutoryOverrideType.ForceNotCovered);
		if (coverageOverride?.OverrideType == StatutoryOverrideType.ForceNotCovered)
		{
			return Empty("FORCE_NOT_COVERED_OVERRIDE", wageCeiling, periodStart, periodEnd, issueCodes, canCalculate: true);
		}

		var totalRemuneration = RoundMoney(wageLines.Sum(x => x.Amount));
		var includedWages = RoundMoney(wageLines
			.Where(x => x.Treatment == StatutoryWageTreatment.Included)
			.Sum(x => x.Amount));
		var specifiedExclusions = RoundMoney(wageLines
			.Where(x => x.Treatment == StatutoryWageTreatment.SpecifiedExclusion)
			.Sum(x => x.Amount));
		var rawInKind = wageLines
			.Where(x => x.Treatment == StatutoryWageTreatment.InKind)
			.Sum(x => x.Amount);
		var inKindWages = RoundMoney(Math.Min(rawInKind, totalRemuneration * rule.InKindCapPercentage / 100m));
		var exclusionAddBack = RoundMoney(Math.Max(
			0m,
			specifiedExclusions - totalRemuneration * rule.ExclusionCapPercentage / 100m));
		var statutoryWages = RoundMoney(includedWages + exclusionAddBack + inKindWages);

		var forcedCovered = coverageOverride?.OverrideType == StatutoryOverrideType.ForceCovered;
		var continuingCoverage = enrollment is not null &&
			enrollment.Status is EmployeeStatutoryEnrollmentStatus.Covered or EmployeeStatutoryEnrollmentStatus.RegistrationPending &&
			(!enrollment.CoveredFrom.HasValue || enrollment.CoveredFrom.Value.Date <= monthEnd) &&
			(!enrollment.CoveredTo.HasValue || enrollment.CoveredTo.Value.Date >= monthStart) &&
			enrollment.ContributionPeriodEnd.HasValue &&
			enrollment.ContributionPeriodEnd.Value.Date >= monthStart;
		var underCeiling = statutoryWages <= wageCeiling;
		var isCovered = forcedCovered || continuingCoverage || underCeiling;
		var decision = forcedCovered
			? "FORCE_COVERED_OVERRIDE"
			: continuingCoverage && !underCeiling
				? "CONTRIBUTION_PERIOD_CONTINUATION"
				: underCeiling
					? "WITHIN_WAGE_CEILING"
					: "ABOVE_WAGE_CEILING";

		if (!isCovered)
		{
			return new EsiStatutoryCalculationResult(
				false, true, decision, totalRemuneration, includedWages, specifiedExclusions,
				exclusionAddBack, inKindWages, statutoryWages, wageCeiling, 0m, 0m,
				periodStart, periodEnd, issueCodes);
		}

		if (enrollment is null || enrollment.Status == EmployeeStatutoryEnrollmentStatus.PendingReview)
		{
			issueCodes.Add(EnrollmentReviewIssue);
		}
		if (enrollment is not { Status: EmployeeStatutoryEnrollmentStatus.RegistrationPending } &&
			string.IsNullOrWhiteSpace(enrollment?.InsuranceNumber))
		{
			issueCodes.Add(InsuranceNumberIssue);
		}

		var employeeContribution = ApplyRounding(statutoryWages * rule.EmployeePercentage / 100m, rule.RoundingPolicy);
		var employerContribution = ApplyRounding(statutoryWages * rule.EmployerPercentage / 100m, rule.RoundingPolicy);
		var employeeAmountOverride = activeOverrides.FirstOrDefault(x => x.OverrideType == StatutoryOverrideType.EmployeeAmount);
		var employerAmountOverride = activeOverrides.FirstOrDefault(x => x.OverrideType == StatutoryOverrideType.EmployerAmount);
		if (employeeAmountOverride?.Amount is { } employeeOverrideAmount)
		{
			employeeContribution = employeeOverrideAmount;
		}
		if (employerAmountOverride?.Amount is { } employerOverrideAmount)
		{
			employerContribution = employerOverrideAmount;
		}

		return new EsiStatutoryCalculationResult(
			true, true, decision, totalRemuneration, includedWages, specifiedExclusions,
			exclusionAddBack, inKindWages, statutoryWages, wageCeiling,
			employeeContribution, employerContribution, periodStart, periodEnd, issueCodes);
	}

	private static EsiStatutoryCalculationResult Empty(
		string decision,
		decimal wageCeiling,
		DateTime periodStart,
		DateTime periodEnd,
		IReadOnlyList<string> issueCodes,
		bool canCalculate = false) =>
		new(false, canCalculate, decision, 0m, 0m, 0m, 0m, 0m, 0m, wageCeiling, 0m, 0m,
			periodStart, periodEnd, issueCodes);

	private static (DateTime Start, DateTime End) GetContributionPeriod(DateTime date) =>
		date.Month is >= 4 and <= 9
			? (new DateTime(date.Year, 4, 1), new DateTime(date.Year, 9, 30))
			: date.Month is >= 10 and <= 12
				? (new DateTime(date.Year, 10, 1), new DateTime(date.Year + 1, 3, 31))
				: (new DateTime(date.Year - 1, 10, 1), new DateTime(date.Year, 3, 31));

	private static decimal ApplyRounding(decimal value, StatutoryRoundingPolicy policy) => policy switch
	{
		StatutoryRoundingPolicy.NextHigherRupee => Math.Ceiling(value),
		_ => RoundMoney(value)
	};

	private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
