using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.Payroll.Statutory;

public sealed class EsiStatutoryPreviewReader(
	IRepository<StatutoryRuleVersion, Guid> ruleRepository,
	EsiPayrollConfigurationResolver configurationResolver,
	EsiEnrollmentResolver enrollmentResolver,
	IRepository<PayrollComponentStatutoryTreatment, Guid> treatmentRepository,
	IRepository<StatutoryOverride, Guid> overrideRepository,
	IEmployeePayrollComponentRepository payrollComponentRepository,
	IEarningComponentRepository earningComponentRepository,
	PayrollCompensationRevisionResolver compensationRevisionResolver,
	PayrollScopedEmployeeResolver scopedEmployeeResolver,
	IClock clock, EmployeeStatutoryProfileResolver profileResolver) : ITransientDependency
{
	private static string CreateSeverity(string code) => EsiStatutoryPresentation.CreateIssue(code).Severity;
	private const string JurisdictionCode = "IN-GJ";

	public async Task<EsiStatutoryPreviewCalculation> CalculateAsync(string monthYear, EsiConfigurationDraftDto? draft = null)
	{
		var payrollMonth = DateTime.ParseExact(monthYear, "MM-yyyy", CultureInfo.InvariantCulture);
		var savedConfiguration = await configurationResolver.ResolveAsync(payrollMonth);
		var configuration = draft == null ? savedConfiguration
			: new EsiPayrollConfiguration(Guid.Empty, null, payrollMonth, draft.Enabled, draft.RegistrationNumber,
				draft.CoveredSince, "PREVIEW", calculationSettings: draft.CalculationSettings);
		var establishment = configuration?.ToCalculationEstablishment();
		if (establishment == null)
			return new EsiStatutoryPreviewCalculation(monthYear, [], new StatutoryImpactSummaryDto { CanActivate = true },
				clock.Now.ToUniversalTime(), "DISABLED");
		var monthEnd = new DateTime(payrollMonth.Year, payrollMonth.Month, 1).AddMonths(1).AddDays(-1);
		var rules = await ruleRepository.GetListAsync(x =>
			x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance &&
			x.JurisdictionCode == JurisdictionCode &&
			x.IsActive &&
			x.EffectiveFrom <= monthEnd &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= monthEnd));
		var rule = rules.OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Version).FirstOrDefault()
			?? throw new Volo.Abp.BusinessException(HRErrorCodes.StatutoryRuleNotFound);
		var employees = await scopedEmployeeResolver.GetScopedPayrollEmployeesAsync(
			payrollMonth.Year, payrollMonth.Month, includeUnknownStageEmployees: false);
		var employeeIds = employees.Select(x => x.Id).ToList();
		var existingComponents = await payrollComponentRepository.GetByEmployeeIdsAsync(employeeIds);
		var existingComponentsByEmployee = existingComponents.SelectMany(x => x).ToLookup(x => x.EmployeeId);
		var compensation = await compensationRevisionResolver.ResolveAsync(
			employeeIds, payrollMonth.Year, payrollMonth.Month, existingComponentsByEmployee);
		var effectiveTreatments = await treatmentRepository.GetListAsync(x =>
			x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance &&
			x.EffectiveFrom <= payrollMonth &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= payrollMonth));
		var treatments = effectiveTreatments
			.OrderByDescending(x => x.EffectiveFrom)
			.GroupBy(x => x.ComponentId)
			.ToDictionary(x => x.Key, x => x.First().Treatment);
		foreach (var earning in await earningComponentRepository.GetListAsync(x => x.IsActive))
			treatments.TryAdd(earning.Id, EsiWageDefaults.ByName.GetValueOrDefault(earning.Name, StatutoryWageTreatment.Unclassified));
		foreach (var treatment in savedConfiguration?.GetTreatments() ?? []) treatments[treatment.Key] = treatment.Value;
		foreach (var treatment in draft?.Treatments ?? [])
			treatments[treatment.ComponentId] = Enum.Parse<StatutoryWageTreatment>(treatment.Treatment);
		var profiles = await profileResolver.ResolveAsync(payrollMonth, employeeIds);
		var enrollments = await enrollmentResolver.ResolveAsync(payrollMonth, employeeIds);
		var overrides = (await overrideRepository.GetListAsync(x =>
			x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance && employeeIds.Contains(x.EmployeeId) &&
			x.EffectiveFrom <= payrollMonth && x.EffectiveTo >= payrollMonth))
			.ToLookup(x => x.EmployeeId);
		var activeEarnings = await earningComponentRepository.GetListAsync(x => x.IsActive);
		var calculationSettings = configuration!.GetCalculationSettings();
		calculationSettings = calculationSettings with
		{
			EligibilityComponentIsActive = calculationSettings.EligibilityComponentId == null ||
			activeEarnings.Any(x => x.Id == calculationSettings.EligibilityComponentId),
			CalculationComponentIsActive = calculationSettings.CalculationComponentId == null ||
			activeEarnings.Any(x => x.Id == calculationSettings.CalculationComponentId)
		};
		var items = new List<EmployeeStatutoryImpactDto>();

		foreach (var employee in employees.OrderBy(x => x.EmployeeName).ThenBy(x => x.EmployeeCode))
		{
			var enrollment = enrollments.GetValueOrDefault(employee.Id);
			if (!compensation.TryGetValue(employee.Id, out var compensationResolution) || !compensationResolution.Components.Any(x => x.IsEarning))
			{
				items.Add(new EmployeeStatutoryImpactDto
				{
					EmployeeId = employee.Id,
					EmployeeIdentifier = employee.EmployeeCode ?? string.Empty,
					EmployeeName = employee.EmployeeName,
					CoverageDecisionCode = IssueCodes.CompensationRevisionRequired,
					CoverageDecisionLabel = EsiStatutoryPresentation.GetCoverageDecisionLabel(
						IssueCodes.CompensationRevisionRequired),
					EnrollmentStatus = (enrollment?.Status ?? EmployeeStatutoryEnrollmentStatus.PendingReview).ToString(),
					WageCeilingCategory = (enrollment?.WageCeilingCategory ?? StatutoryWageCeilingCategory.Standard).ToString(),
					InsuranceNumber = enrollment?.InsuranceNumber,
					ContributionPeriodEnd = enrollment?.ContributionPeriodEnd,
					Issues = [EsiStatutoryPresentation.CreateIssue(IssueCodes.CompensationRevisionRequired)]
				});
				continue;
			}

			var wageLines = compensationResolution.Components
				.Where(x => x.IsEarning)
				.Select(x => new StatutoryWageLine(
					x.ComponentId ?? x.Id,
					x.Name,
					x.Amount,
					treatments.GetValueOrDefault(x.ComponentId ?? x.Id, StatutoryWageTreatment.Unclassified), x.IsTemp, x.SemanticType == PayrollComponentSemanticType.Basic))
				.ToList();
			var calculation = EsiStatutoryCalculator.Calculate(
				payrollMonth, rule, establishment, enrollment, wageLines,
				overrides[employee.Id].ToList(), calculationSettings: calculationSettings, payrollProfile: profiles.GetValueOrDefault(employee.Id));
			items.Add(new EmployeeStatutoryImpactDto
			{
				EmployeeId = employee.Id,
				EmployeeIdentifier = employee.EmployeeCode ?? string.Empty,
				EmployeeName = employee.EmployeeName,
				IsCovered = calculation.IsCovered,
				CanCalculate = calculation.CanCalculate,
				CoverageDecisionCode = calculation.CoverageDecision,
				CoverageDecisionLabel = EsiStatutoryPresentation.GetCoverageDecisionLabel(calculation.CoverageDecision),
				StatutoryWages = calculation.StatutoryWages,
				EligibilityWages = calculation.EligibilityWages,
				EmployeePercentage = calculation.EmployeePercentage,
				EmployerPercentage = calculation.EmployerPercentage,
				CalculationSettings = calculationSettings,
				WageCeiling = calculation.WageCeiling,
				EmployeeContribution = calculation.EmployeeContribution,
				EmployerContribution = calculation.EmployerContribution,
				GovernmentContribution = calculation.GovernmentContribution,
				EnrollmentStatus = (enrollment?.Status ?? EmployeeStatutoryEnrollmentStatus.PendingReview).ToString(),
				WageCeilingCategory = (enrollment?.WageCeilingCategory ?? StatutoryWageCeilingCategory.Standard).ToString(),
				InsuranceNumber = enrollment?.InsuranceNumber,
				ContributionPeriodEnd = calculation.ContributionPeriodEnd,
				Issues = calculation.IssueCodes.Select(EsiStatutoryPresentation.CreateIssue).ToList()
			});
		}

		var issueCodes = items.SelectMany(x => x.Issues).Select(x => x.Code).Distinct().ToList();
		if ((calculationSettings.EligibilityBasis == "CLASSIFIED_EARNINGS" || calculationSettings.CalculationBasis == "CLASSIFIED_EARNINGS") &&
			activeEarnings.Any(x => treatments.GetValueOrDefault(x.Id, StatutoryWageTreatment.Unclassified) == StatutoryWageTreatment.Unclassified))
			issueCodes.Add(EsiStatutoryCalculator.WageClassificationIssue);
		if (establishment is null || !establishment.CanActivate())
		{
			issueCodes.Add(EsiStatutoryCalculator.EstablishmentInactiveIssue);
		}
		issueCodes = issueCodes.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
		var blockingIssues = issueCodes.Where(code => CreateSeverity(code) != "warning").Select(code => EsiStatutoryPresentation.CreateIssue(
			code, items.Count(item => item.Issues.Any(issue => issue.Code == code)))).ToList();
		var summary = new StatutoryImpactSummaryDto
		{
			EmployeesNeedingAttention = items.Count(x => x.Issues.Count > 0),
			EmployeeCount = items.Count,
			CoveredEmployeeCount = items.Count(x => x.IsCovered),
			EmployeeContributionTotal = items.Sum(x => x.EmployeeContribution),
			EmployerContributionTotal = items.Sum(x => x.EmployerContribution),
			GovernmentContributionTotal = items.Sum(x => x.GovernmentContribution),
			CanActivate = blockingIssues.Count == 0,
			BlockingIssues = blockingIssues
		};
		var inputEvidence = new
		{
			CalculationSettings = calculationSettings,
			Employees = employees.OrderBy(x => x.Id).Select(x => new { x.Id, x.ConcurrencyStamp }).ToList(),
			Earnings = activeEarnings.OrderBy(x => x.Id).Select(x => new { x.Id, x.ConcurrencyStamp }).ToList(),
			Compensation = compensation.OrderBy(x => x.Key).Select(x => new
			{
				EmployeeId = x.Key,
				x.Value.Revision.Id,
				x.Value.Revision.ConcurrencyStamp,
				// Statutory deduction source IDs are generated during projection and are not ESI inputs.
				Components = x.Value.Components.Where(component => component.IsEarning).OrderBy(component => component.Id).Select(component => new
				{ component.Id, component.ComponentId, component.IsEarning, component.Amount }).ToList()
			}).ToList(),
			Enrollments = enrollments.Values.OrderBy(x => x.EmployeeId).Select(x => new
			{
				x.EmployeeId,
				x.ConcurrencyStamp,
				x.Status,
				x.WageCeilingCategory,
				x.InsuranceNumber,
				x.CoveredFrom,
				x.CoveredTo,
				x.ContributionPeriodEnd,
				x.StatusReason,
				x.EmployerShareReliefEligible,
				x.EmployerShareReliefFrom,
				x.EmployerShareReliefTo,
				x.DisabilityEvidenceReference
			}).ToList(),
			Overrides = overrides.SelectMany(x => x).OrderBy(x => x.Id).Select(x => new
			{
				x.Id,
				x.EmployeeId,
				x.ConcurrencyStamp,
				x.OverrideType,
				x.Amount,
				x.Reason,
				x.EvidenceReference,
				x.EffectiveFrom,
				x.EffectiveTo,
				x.ApprovedByUserId
			}).ToList()
		};
		var previewFingerprint = BuildFingerprint(monthYear, rule, establishment, effectiveTreatments, items, summary, inputEvidence);
		return new EsiStatutoryPreviewCalculation(
			monthYear, items, summary, clock.Now.ToUniversalTime(), previewFingerprint);
	}

	public static StatutoryImpactPreviewDto Project(
		EsiStatutoryPreviewCalculation calculation,
		StatutoryImpactPreviewRequestDto input)
	{
		IEnumerable<EmployeeStatutoryImpactDto> filtered = calculation.Items;
		var search = input.Search?.Trim();
		if (!string.IsNullOrWhiteSpace(search))
		{
			filtered = filtered.Where(x =>
				x.EmployeeName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
				x.EmployeeIdentifier.Contains(search, StringComparison.OrdinalIgnoreCase));
		}
		if (input.Coverage == "covered")
		{
			filtered = filtered.Where(x => x.IsCovered);
		}
		else if (input.Coverage == "notCovered")
		{
			filtered = filtered.Where(x => !x.IsCovered);
		}
		if (input.Readiness == "ready")
		{
			filtered = filtered.Where(x => x.Issues.Count == 0);
		}
		else if (input.Readiness == "needsAttention")
		{
			filtered = filtered.Where(x => x.Issues.Count > 0);
		}
		if (input.EnrollmentStatuses.Count > 0)
		{
			var statuses = input.EnrollmentStatuses.ToHashSet(StringComparer.OrdinalIgnoreCase);
			filtered = filtered.Where(x => statuses.Contains(x.EnrollmentStatus));
		}

		filtered = ApplySorting(filtered, input.SortBy, input.SortDirection == "desc");
		var totalCount = filtered.Count();
		return new StatutoryImpactPreviewDto
		{
			MonthYear = calculation.MonthYear,
			Summary = calculation.Summary,
			TotalCount = totalCount,
			Page = input.Page,
			PageSize = input.PageSize,
			Items = filtered.Skip((int) Math.Min((long) input.Page * input.PageSize, int.MaxValue)).Take(input.PageSize).ToList(),
			GeneratedAtUtc = calculation.GeneratedAtUtc,
			PreviewFingerprint = calculation.PreviewFingerprint
		};
	}

	private static IOrderedEnumerable<EmployeeStatutoryImpactDto> ApplySorting(
		IEnumerable<EmployeeStatutoryImpactDto> source,
		string sortBy,
		bool descending)
	{
		Func<EmployeeStatutoryImpactDto, object> selector = sortBy switch
		{
			"employeeIdentifier" => item => item.EmployeeIdentifier,
			"statutoryWages" => item => item.StatutoryWages,
			"employeeContribution" => item => item.EmployeeContribution,
			"employerContribution" => item => item.EmployerContribution,
			"readiness" => item => item.Issues.Count,
			_ => item => item.EmployeeName
		};
		var ordered = descending
			? source.OrderByDescending(selector)
			: source.OrderBy(selector);
		return ordered.ThenBy(x => x.EmployeeName, StringComparer.OrdinalIgnoreCase)
			.ThenBy(x => x.EmployeeIdentifier, StringComparer.OrdinalIgnoreCase)
			.ThenBy(x => x.EmployeeId);
	}

	private static string BuildFingerprint(
		string monthYear,
		StatutoryRuleVersion rule,
		StatutoryEstablishment? establishment,
		IEnumerable<PayrollComponentStatutoryTreatment> treatments,
		IEnumerable<EmployeeStatutoryImpactDto> items,
		StatutoryImpactSummaryDto summary,
		object inputEvidence)
	{
		var payload = new
		{
			MonthYear = monthYear,
			InputEvidence = inputEvidence,
			Rule = new
			{
				rule.Id,
				rule.Version,
				rule.EffectiveFrom,
				rule.EffectiveTo,
				rule.JurisdictionCode,
				rule.EstablishmentThreshold,
				rule.StandardWageCeiling,
				rule.DisabilityWageCeiling,
				rule.EmployeePercentage,
				rule.EmployeeShareDailyWageExemption,
				rule.EmployerPercentage,
				rule.ExclusionCapPercentage,
				rule.InKindCapPercentage,
				rule.RoundingPolicy,
				rule.SourceUrl,
				rule.SourceVerifiedOn
			},
			Establishment = establishment is null ? null : new
			{
				establishment.Id,
				establishment.Status,
				establishment.RegistrationNumber,
				establishment.EmployeeCount,
				establishment.CoveredSince,
				establishment.BenefitsActiveFrom,
				establishment.EvidenceReference
			},
			Treatments = treatments
				.Select(x => new { x.ComponentId, x.Treatment, x.EffectiveFrom, x.EffectiveTo })
				.OrderBy(x => x.ComponentId)
				.ThenBy(x => x.EffectiveFrom)
				.ToList(),
			Employees = items
				.Select(x => new
				{
					x.EmployeeId,
					x.IsCovered,
					x.CanCalculate,
					x.CoverageDecisionCode,
					x.StatutoryWages,
					x.WageCeiling,
					x.EmployeeContribution,
					x.EmployerContribution,
					x.GovernmentContribution,
					x.EnrollmentStatus,
					x.WageCeilingCategory,
					x.InsuranceNumber,
					x.ContributionPeriodEnd,
					Issues = x.Issues.Select(issue => issue.Code).OrderBy(code => code).ToList()
				})
				.OrderBy(x => x.EmployeeId)
				.ToList(),
			Summary = new
			{
				summary.EmployeeCount,
				summary.CoveredEmployeeCount,
				summary.EmployeeContributionTotal,
				summary.EmployerContributionTotal,
				summary.GovernmentContributionTotal,
				summary.CanActivate
			}
		};
		var json = JsonSerializer.Serialize(payload);
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
	}
}

public sealed record EsiStatutoryPreviewCalculation(
	string MonthYear,
	List<EmployeeStatutoryImpactDto> Items,
	StatutoryImpactSummaryDto Summary,
	DateTime GeneratedAtUtc,
	string PreviewFingerprint);

internal static class EsiStatutoryPresentation
{
	public static StatutoryIssueDto CreateIssue(string code, int affectedEmployeeCount = 0) => code switch
	{
		"ESI_APPLY_DISABLED" or "ESI_NUMBER_MISSING" or "ESI_BASIS_RECONCILIATION_DIFFERENCE" or "ESI_STATUTORY_ASSESSMENT_UNAVAILABLE" => new StatutoryIssueDto { Code = code, Severity = "warning", Title = code == "ESI_APPLY_DISABLED" ? "ESIC turned off" : code == "ESI_NUMBER_MISSING" ? "ESIC number missing" : "Statutory reconciliation", Message = code == "ESI_NUMBER_MISSING" ? "No ESIC deduction will apply until registration is completed." : code == "ESI_APPLY_DISABLED" ? "No ESIC contribution will apply. Review the recorded reason." : "The selected deduction basis differs from the statutory wage assessment. Review the saved evidence.", ResolutionTarget = "employeeEnrollment", AffectedEmployeeCount = affectedEmployeeCount },
		EsiStatutoryCalculator.RateOverrideIssue => new StatutoryIssueDto { Code = code, Severity = "warning", Title = "Administrator rate override", Message = "The documented school rates differ from the statutory reference. Review the recorded reason and evidence.", ResolutionTarget = "general", AffectedEmployeeCount = affectedEmployeeCount },
		EsiStatutoryCalculator.BasicComponentIssue => Create(code, "Review salary component", "Select an active recurring earning component present exactly once in the salary structure.", "earningClassifications", affectedEmployeeCount),
		EsiStatutoryCalculator.EstablishmentInactiveIssue => Create(code, "Complete establishment details",
			"Add the ESI registration and coverage dates before activation.", "establishment", affectedEmployeeCount),
		EsiStatutoryCalculator.WageClassificationIssue => Create(code, "Classify earning components",
			"Every active earning must have an ESI wage treatment.", "earningClassifications", affectedEmployeeCount),
		EsiStatutoryCalculator.EnrollmentReviewIssue => Create(code, "Review staff coverage",
			"Confirm whether the affected staff are covered by ESI.", "employeeEnrollment", affectedEmployeeCount),
		EsiStatutoryCalculator.InsuranceNumberIssue => Create(code, "Add ESI insurance numbers",
			"Add an insurance number or mark registration as pending.", "employeeEnrollment", affectedEmployeeCount),
		EsiStatutoryCalculator.DisabilityReliefEvidenceIssue => Create(code, "Complete PwD relief evidence",
			"Add valid relief dates and disability evidence, or turn off employer-share relief.",
			"employeeEnrollment", affectedEmployeeCount),
		IssueCodes.CompensationRevisionRequired => Create(code, "Set up staff salaries",
			"The affected staff need an effective salary setup for this payroll month.",
			"compensationRevision", affectedEmployeeCount),
		_ => Create(code, "Review payroll setup",
			"Resolve this payroll setup issue before activating ESI.", "general", affectedEmployeeCount)
	};

	public static string GetCoverageDecisionLabel(string code) => code switch
	{
		"ESTABLISHMENT_INACTIVE" => "Establishment setup incomplete",
		"WAGE_CLASSIFICATION_REQUIRED" => "Earning classification required",
		"FORCE_NOT_COVERED_OVERRIDE" => "Not covered by approved exception",
		"FORCE_COVERED_OVERRIDE" => "Covered by approved exception",
		"CONTRIBUTION_PERIOD_CONTINUATION" => "Covered until the contribution period ends",
		"WITHIN_WAGE_CEILING" => "Within the ESI wage ceiling",
		"ABOVE_WAGE_CEILING" => "Above the ESI wage ceiling",
		IssueCodes.CompensationRevisionRequired => "Compensation revision required",
		_ => "Coverage needs review"
	};

	private static StatutoryIssueDto Create(
		string code,
		string title,
		string message,
		string resolutionTarget,
		int affectedEmployeeCount) => new()
		{
			Code = code,
			Title = title,
			Message = message,
			ResolutionTarget = resolutionTarget,
			AffectedEmployeeCount = affectedEmployeeCount
		};
}