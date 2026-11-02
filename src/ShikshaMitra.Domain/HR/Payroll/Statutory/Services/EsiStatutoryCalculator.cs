using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Payroll.Statutory;

public sealed record StatutoryWageLine(
	Guid ComponentId,
	string Name,
	decimal Amount,
	StatutoryWageTreatment Treatment, bool IsAdditional = false, bool IsBasic = false);

public sealed record UnclassifiedEsiEarning(Guid ComponentId, string Name);

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
	decimal GovernmentContribution,
	DateTime ContributionPeriodStart,
	DateTime ContributionPeriodEnd,
	IReadOnlyList<string> IssueCodes)
{
	public decimal? ReferenceStatutoryWages { get; init; }
	public PayrollSchemeApplication? Application { get; init; }
	public decimal EligibilityWages { get; init; }
	public decimal EmployeePercentage { get; init; }
	public decimal EmployerPercentage { get; init; }
	public EsiCalculationSettings CalculationSettings { get; init; } = new();
	public IReadOnlyList<UnclassifiedEsiEarning> UnclassifiedEarnings { get; init; } = [];
}

public static class EsiStatutoryCalculator
{
	public const string BasicComponentIssue = "ESI_COMPONENT_REQUIRED";
	public const string RateOverrideIssue = "ESI_RATE_OVERRIDE";
	public const string EstablishmentInactiveIssue = "ESI_ESTABLISHMENT_INACTIVE";
	public const string WageClassificationIssue = "ESI_WAGE_CLASSIFICATION_REQUIRED";
	public const string EnrollmentReviewIssue = "ESI_ENROLLMENT_REVIEW_REQUIRED";
	public const string InsuranceNumberIssue = "ESI_INSURANCE_NUMBER_REQUIRED";
	public const string DisabilityReliefEvidenceIssue = "ESI_PWD_RELIEF_EVIDENCE_REQUIRED";

	public static EsiStatutoryCalculationResult Calculate(
		DateTime payrollMonth, StatutoryRuleVersion rule, StatutoryEstablishment? establishment,
		EmployeeStatutoryEnrollment? enrollment, IReadOnlyList<StatutoryWageLine> wageLines,
		IReadOnlyList<StatutoryOverride>? overrides = null, bool assumeEstablishmentActive = false,
		IReadOnlyList<StatutoryWageLine>? coverageWageLines = null, EsiCalculationSettings? calculationSettings = null,
		PayrollStatutoryProfile? payrollProfile = null)
	{
		var result = CalculateAssessment(payrollMonth, rule, establishment, enrollment, wageLines, overrides,
			assumeEstablishmentActive, coverageWageLines, calculationSettings);
		if (payrollProfile == null) return result;
		var application = PayrollSchemeApplication.Decide("ESI", payrollProfile.ApplyEsic, payrollProfile.EsicInsuranceNumber,
			result.IsCovered, result.EmployeeContribution, result.EmployerContribution, payrollProfile.EsicDisabledReason) with { ContributionWages = result.StatutoryWages };
		var issues = result.IssueCodes.Where(x => x != InsuranceNumberIssue).ToList();
		if (application.WarningCode != null) issues.Add(application.WarningCode);
		return result with { Application = application, EmployeeContribution = application.AppliedEmployeeAmount,
			EmployerContribution = application.AppliedEmployerAmount,
			GovernmentContribution = application.Apply && application.Eligible ? result.GovernmentContribution : 0m,
			IssueCodes = issues };
	}

	private static EsiStatutoryCalculationResult CalculateAssessment(
		DateTime payrollMonth,
		StatutoryRuleVersion rule,
		StatutoryEstablishment? establishment,
		EmployeeStatutoryEnrollment? enrollment,
		IReadOnlyList<StatutoryWageLine> wageLines,
		IReadOnlyList<StatutoryOverride>? overrides = null,
		bool assumeEstablishmentActive = false,
		IReadOnlyList<StatutoryWageLine>? coverageWageLines = null, EsiCalculationSettings? calculationSettings = null)
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

		calculationSettings ??= new EsiCalculationSettings();
		calculationSettings.Validate();
		var fullMonthWageLines = coverageWageLines ?? wageLines;
		var assessmentLines = wageLines;
		var eligibilityComponentUnavailable = !calculationSettings.EligibilityComponentIsActive ||
			fullMonthWageLines.Count(x => !x.IsAdditional && x.ComponentId == calculationSettings.EligibilityComponentId) != 1;
		var calculationComponentUnavailable = !calculationSettings.CalculationComponentIsActive ||
			fullMonthWageLines.Count(x => !x.IsAdditional && x.ComponentId == calculationSettings.CalculationComponentId) != 1;
		if (calculationSettings.EligibilityBasis == "COMPONENT" && eligibilityComponentUnavailable)
			return Empty("COMPONENT_REQUIRED", wageCeiling, periodStart, periodEnd, [BasicComponentIssue]) with { CalculationSettings = calculationSettings };
		var eligibilityWageLines = ResolveWages(fullMonthWageLines, calculationSettings.EligibilityBasis, calculationSettings.EligibilityComponentId);
		if (calculationSettings.HasRateOverride) issueCodes.Add(RateOverrideIssue);
		var employeePercentage = calculationSettings.EmployeePercentage ?? rule.EmployeePercentage;
		var employerPercentage = calculationSettings.EmployerPercentage ?? rule.EmployerPercentage;
		if (eligibilityWageLines.Any(x => x.Treatment == StatutoryWageTreatment.Unclassified))
		{
			issueCodes.Add(WageClassificationIssue);
			return Empty("WAGE_CLASSIFICATION_REQUIRED", wageCeiling, periodStart, periodEnd, issueCodes) with
			{
				UnclassifiedEarnings = MissingClassifications(eligibilityWageLines)
			};
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

		// Monthly-rated coverage uses agreed monthly remuneration, not pay reduced by unpaid days.
		// Contributions use the separately selected payable basis. ESIC Revenue Manual, L.6.8.
		var coverageWages = CalculateWages(rule, eligibilityWageLines).StatutoryWages;

		var forcedCovered = coverageOverride?.OverrideType == StatutoryOverrideType.ForceCovered;
		var continuingCoverage = enrollment is not null &&
			enrollment.Status is EmployeeStatutoryEnrollmentStatus.Covered or EmployeeStatutoryEnrollmentStatus.RegistrationPending &&
			(!enrollment.CoveredFrom.HasValue || enrollment.CoveredFrom.Value.Date <= monthEnd) &&
			(!enrollment.CoveredTo.HasValue || enrollment.CoveredTo.Value.Date >= monthStart) &&
			enrollment.ContributionPeriodEnd.HasValue &&
			enrollment.ContributionPeriodEnd.Value.Date >= monthStart;
		var underCeiling = coverageWages <= wageCeiling;
		var isCovered = forcedCovered || continuingCoverage || underCeiling;
		var decision = forcedCovered
			? "FORCE_COVERED_OVERRIDE"
			: continuingCoverage && !underCeiling
				? "CONTRIBUTION_PERIOD_CONTINUATION"
				: underCeiling
					? "WITHIN_WAGE_CEILING"
					: "ABOVE_WAGE_CEILING";

		var fullMonthCalculationWageLines = ResolveWages(fullMonthWageLines, calculationSettings.CalculationBasis, calculationSettings.CalculationComponentId);
		wageLines = ResolveWages(wageLines, calculationSettings.CalculationBasis, calculationSettings.CalculationComponentId);
		var calculationIssue = calculationSettings.CalculationBasis == "COMPONENT" && calculationComponentUnavailable
			? BasicComponentIssue
			: wageLines.Concat(fullMonthCalculationWageLines).Any(x => x.Treatment == StatutoryWageTreatment.Unclassified)
				? WageClassificationIssue : null;
		if (calculationIssue != null)
		{
			// Employees outside coverage do not require a contribution component or calculation classification.
			if (!isCovered)
				return Empty(decision, wageCeiling, periodStart, periodEnd, issueCodes, canCalculate: true) with
				{
					EligibilityWages = coverageWages,
					EmployeePercentage = employeePercentage,
					EmployerPercentage = employerPercentage,
					CalculationSettings = calculationSettings
				};
			issueCodes.Add(calculationIssue);
			return Empty(calculationIssue == BasicComponentIssue ? "COMPONENT_REQUIRED" : "WAGE_CLASSIFICATION_REQUIRED",
				wageCeiling, periodStart, periodEnd, issueCodes) with
			{
				IsCovered = true,
				EligibilityWages = coverageWages,
				CalculationSettings = calculationSettings,
				UnclassifiedEarnings = calculationIssue == WageClassificationIssue
					? MissingClassifications(wageLines.Concat(fullMonthCalculationWageLines)) : []
			};
		}
		var (totalRemuneration, includedWages, specifiedExclusions, exclusionAddBack, inKindWages, statutoryWages)
			= CalculateWages(rule, wageLines);
		var fullMonthCalculationWages = CalculateWages(rule, fullMonthCalculationWageLines).StatutoryWages;
		decimal? referenceWages = assessmentLines.Any(x => x.Treatment == StatutoryWageTreatment.Unclassified)
			? null : CalculateWages(rule, assessmentLines).StatutoryWages;
		if (referenceWages == null) issueCodes.Add("ESI_STATUTORY_ASSESSMENT_UNAVAILABLE");
		else if (referenceWages != statutoryWages) issueCodes.Add("ESI_BASIS_RECONCILIATION_DIFFERENCE");

		if (!isCovered)
		{
			return new EsiStatutoryCalculationResult(
				false, true, decision, totalRemuneration, includedWages, specifiedExclusions,
				exclusionAddBack, inKindWages, statutoryWages, wageCeiling, 0m, 0m, 0m,
				periodStart, periodEnd, issueCodes)
			{ EligibilityWages = coverageWages, EmployeePercentage = employeePercentage, EmployerPercentage = employerPercentage, CalculationSettings = calculationSettings };
		}

		if (string.IsNullOrWhiteSpace(enrollment?.InsuranceNumber))
		{
			issueCodes.Add(InsuranceNumberIssue);
		}

		// Monthly-rated average daily wages use full contractual wages divided by 26,
		// so unpaid days cannot create an employee-share exemption.
		var employeeContribution = fullMonthCalculationWages / 26m <= rule.EmployeeShareDailyWageExemption
			? 0m : ApplyRounding(statutoryWages * employeePercentage / 100m, rule.RoundingPolicy);
		var employerContribution = ApplyRounding(statutoryWages * employerPercentage / 100m, rule.RoundingPolicy);
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
		var governmentContribution = 0m;
		var hasValidDisabilityRelief = enrollment is
		{
			WageCeilingCategory: StatutoryWageCeilingCategory.PersonWithDisability,
			EmployerShareReliefEligible: true
		} &&
			enrollment.EmployerShareReliefFrom.HasValue &&
			monthStart < enrollment.EmployerShareReliefFrom.Value.Date.AddYears(3) &&
			enrollment.EmployerShareReliefFrom.Value.Date <= monthEnd &&
			(!enrollment.EmployerShareReliefTo.HasValue || enrollment.EmployerShareReliefTo.Value.Date >= monthStart) &&
			!string.IsNullOrWhiteSpace(enrollment.DisabilityEvidenceReference);
		if (enrollment is { EmployerShareReliefEligible: true } && !hasValidDisabilityRelief)
		{
			issueCodes.Add(DisabilityReliefEvidenceIssue);
		}
		if (hasValidDisabilityRelief)
		{
			governmentContribution = employerContribution;
			employerContribution = 0m;
		}
		return new EsiStatutoryCalculationResult(
			true, true, decision, totalRemuneration, includedWages, specifiedExclusions,
			exclusionAddBack, inKindWages, statutoryWages, wageCeiling,
			employeeContribution, employerContribution, governmentContribution, periodStart, periodEnd, issueCodes)
		{
			EligibilityWages = coverageWages,
			EmployeePercentage = employeePercentage,
			EmployerPercentage = employerPercentage,
			CalculationSettings = calculationSettings,
			ReferenceStatutoryWages = referenceWages
		};
	}

	private static (decimal TotalRemuneration, decimal IncludedWages, decimal SpecifiedExclusions,
		decimal ExclusionAddBack, decimal InKindWages, decimal StatutoryWages) CalculateWages(
		StatutoryRuleVersion rule, IReadOnlyList<StatutoryWageLine> wageLines)
	{
		var totalRemuneration = RoundMoney(wageLines.Sum(x => x.Amount));
		var includedWages = RoundMoney(wageLines.Where(x => x.Treatment == StatutoryWageTreatment.Included).Sum(x => x.Amount));
		var specifiedExclusions = RoundMoney(wageLines.Where(x => x.Treatment == StatutoryWageTreatment.SpecifiedExclusion).Sum(x => x.Amount));
		var rawInKind = wageLines.Where(x => x.Treatment == StatutoryWageTreatment.InKind).Sum(x => x.Amount);
		var inKindWages = RoundMoney(Math.Min(rawInKind, totalRemuneration * rule.InKindCapPercentage / 100m));
		var exclusionAddBack = RoundMoney(Math.Max(0m, specifiedExclusions - totalRemuneration * rule.ExclusionCapPercentage / 100m));
		return (totalRemuneration, includedWages, specifiedExclusions, exclusionAddBack, inKindWages,
			RoundMoney(includedWages + exclusionAddBack + inKindWages));
	}

	private static IReadOnlyList<UnclassifiedEsiEarning> MissingClassifications(IEnumerable<StatutoryWageLine> lines) =>
		lines.Where(x => x.Treatment == StatutoryWageTreatment.Unclassified)
			.DistinctBy(x => x.ComponentId).Select(x => new UnclassifiedEsiEarning(x.ComponentId, x.Name)).ToList();

	private static EsiStatutoryCalculationResult Empty(
		string decision,
		decimal wageCeiling,
		DateTime periodStart,
		DateTime periodEnd,
		IReadOnlyList<string> issueCodes,
		bool canCalculate = false) =>
		new(false, canCalculate, decision, 0m, 0m, 0m, 0m, 0m, 0m, wageCeiling, 0m, 0m, 0m,
			periodStart, periodEnd, issueCodes);

	public static (DateTime Start, DateTime End) GetContributionPeriod(DateTime date) =>
		date.Month is >= 4 and <= 9
			? (new DateTime(date.Year, 4, 1), new DateTime(date.Year, 9, 30))
			: date.Month is >= 10 and <= 12
				? (new DateTime(date.Year, 10, 1), new DateTime(date.Year + 1, 3, 31))
				: (new DateTime(date.Year - 1, 10, 1), new DateTime(date.Year, 3, 31));

	private static IReadOnlyList<StatutoryWageLine> ResolveWages(IReadOnlyList<StatutoryWageLine> lines, string basis, Guid? componentId) =>
		basis switch
		{
			"COMPONENT" => lines.Where(x => !x.IsAdditional && x.ComponentId == componentId).Select(x => x with { Treatment = StatutoryWageTreatment.Included }).ToList(),
			"GROSS" => lines.Where(x => !x.IsAdditional).Select(x => x with { Treatment = StatutoryWageTreatment.Included }).ToList(),
			_ => lines
		};

	private static decimal ApplyRounding(decimal value, StatutoryRoundingPolicy policy) => policy switch
	{
		StatutoryRoundingPolicy.NextHigherRupee => Math.Ceiling(value),
		_ => RoundMoney(value)
	};

	private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
