using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Statutory;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.Payroll.Templates;

public class SalaryTemplateCalculationService(
	IEarningComponentRepository earningComponentRepository,
	IDeductionComponentRepository deductionComponentRepository,
	IProvidentFundRepository providentFundRepository,
	IProfessionalTaxRepository professionalTaxRepository,
	SalaryTemplateEsiContextResolver esiContextResolver,
	IAsyncQueryableExecuter asyncExecuter, IClock clock, PfEligibilityResolver pfEligibilityResolver, EmployeeStatutoryProfileResolver profileResolver, EmployeeStatutoryProfileAppService profileService, Volo.Abp.Domain.Repositories.IRepository<ShikshaMitra.HR.Payroll.Compensation.EmployeeCompensationRevision, Guid> revisions) : ITransientDependency
{
	internal async Task<SalaryTemplateCalculationDto> CalculateAsync(
		IReadOnlyList<SalaryTemplateRule> rules, CalculateSalaryTemplateRequestDto input,
		Guid templateId = default, Guid versionId = default, int versionNumber = 0)
	{
		if (input.PfEnrollment == null || input.PfEnrollment.Membership is not ("UNKNOWN" or "EXISTING_MEMBER" or "NOT_MEMBER") || input.PfEnrollment.ConsentReference?.Length > 1000)
			throw new BusinessException("HR:Payroll:PfDeclarationInvalid");
		SalaryTemplateCalculator.ValidateRules(rules);
		if (input.PfApplicable && !rules.Any(x => x.FormulaType == SalaryTemplateFormulaType.EmployeeProvidentFund))
			throw new BusinessException("HR:Payroll:SalaryTemplatePfFormulaRequired");
		var components = await LoadComponentsAsync(rules);
		var effectiveMonth = new DateTime(input.EffectiveMonth.Year, input.EffectiveMonth.Month, 1);
		var effectiveMonthEnd = effectiveMonth.AddMonths(1).AddDays(-1);
		var savedRevision = input.EmployeeId.HasValue ? (await revisions.GetListAsync(x => x.EmployeeId == input.EmployeeId && x.EffectiveMonth <= effectiveMonth && x.Status == ShikshaMitra.HR.Payroll.Compensation.CompensationRevisionStatus.Approved))
			.OrderByDescending(x => x.EffectiveMonth).ThenByDescending(x => x.RevisionNumber).FirstOrDefault() : null;
		var preserveGross = savedRevision != null && savedRevision.SalaryBasis == ParseSalaryBasis(input.SalaryBasis) &&
			savedRevision.QuotedMonthlyAmount == input.MonthlyAmount && savedRevision.SourceSalaryTemplateVersionId == versionId &&
			System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(savedRevision.OverridesJson), System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(input.Overrides)));
		var effectivePfRuleId = input.PfRuleId ?? (preserveGross ? savedRevision?.PfRuleId : null);
		var pfRule = (input.PfApplicable || preserveGross) && effectivePfRuleId.HasValue
			? await providentFundRepository.FindAsync(effectivePfRuleId.Value)
			: null;
		if (input.PfApplicable && pfRule == null)
		{
			throw new BusinessException("HR:Payroll:PfRuleNotFound")
				.WithData("pfRuleId", input.PfRuleId?.ToString() ?? "missing");
		}
		if (pfRule != null && (!pfRule.IsActive || pfRule.EffectiveFrom > effectiveMonthEnd || pfRule.EffectiveTo < effectiveMonth))
		{
			throw new BusinessException("HR:Payroll:PfRuleNotEffective").WithData("pfRuleId", pfRule.Id);
		}

		var professionalTaxSlabs = await asyncExecuter.ToListAsync(
			(await professionalTaxRepository.GetQueryableAsync()).Where(x =>
				x.IsActive && x.JurisdictionCode == input.StatutoryProfile && x.EffectiveFrom <= effectiveMonthEnd &&
				(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= effectiveMonth)));
		if (rules.Any(x => x.FormulaType == SalaryTemplateFormulaType.ProfessionalTax) && professionalTaxSlabs.Count == 0)
		{
			throw new BusinessException("HR:Payroll:ProfessionalTaxRuleNotEffective")
				.WithData("effectiveMonth", effectiveMonth.ToString("yyyy-MM"));
		}

		var basis = ParseSalaryBasis(input.SalaryBasis);
		var protectionPolicy = ParseProtectionPolicy(input.ProtectionPolicy);
		var esiResolution = await esiContextResolver.ResolveAsync(effectiveMonth, input.StatutoryProfile, input.EmployeeId);
		PayrollStatutoryProfile? applicationProfile = input.EmployeeId.HasValue ? await profileResolver.ResolveAsync(input.EmployeeId.Value, effectiveMonth) : null;
		if (input.StatutoryProfileOverride != null)
		{
			if (!input.EmployeeId.HasValue) throw new BusinessException("HR:Payroll:StatutoryProfileEmployeeRequired");
			applicationProfile = await profileService.ValidateDraftAsync(input.EmployeeId.Value, effectiveMonth, input.StatutoryProfileOverride,
				input.StatutoryProfileConcurrencyStamp, input.StatutoryProfileUpdatePf, input.StatutoryProfileUpdateEsic);
		}
		var resolvedGross = preserveGross ? savedRevision!.GrossSalary : basis == SalaryInputBasis.Gross ? input.MonthlyAmount :
			SalaryTemplateCalculator.Calculate(basis, input.MonthlyAmount, rules, components,
				applicationProfile == null || applicationProfile.ApplyPf ? pfRule : null,
				professionalTaxSlabs,
				applicationProfile == null || applicationProfile.ApplyEsic ? esiResolution.Context : null).Gross;
		var evaluation = SalaryTemplateCalculator.Calculate(SalaryInputBasis.Gross, resolvedGross, rules, components, pfRule, professionalTaxSlabs, esiResolution.Context);
		if (applicationProfile != null) evaluation = SalaryTemplateStatutoryApplication.Apply(evaluation, applicationProfile, pfRule != null);
		var pfWages = evaluation.Earnings.Where(x => x.SemanticType is "BASIC" or "DEARNESS_ALLOWANCE").Sum(x => x.Amount);
		var pfEligibility = await pfEligibilityResolver.ResolveAsync(input.EmployeeId, effectiveMonthEnd, evaluation.Gross, pfWages,
			input.PfEnrollment, input.PfApplicable);
		if (applicationProfile is { ApplyPf: false } && pfEligibility.IssueCode is "PF_ENROLLMENT_REQUIRED" or "PF_MEMBERSHIP_REVIEW_REQUIRED")
			pfEligibility = pfEligibility with { CanSave = true };
		if (evaluation.StatutoryApplication is { } application)
			evaluation = evaluation with { StatutoryApplication = application with { ProvidentFund = application.ProvidentFund with
				{ Eligible = pfEligibility.Status is "MANDATORY" or "CONTINUING_MEMBER" || application.ProvidentFund.Eligible, ContributionWages = pfRule == null ? 0m : Math.Min(pfRule.ContributionBasis == PfContributionBasis.BasicAndDearnessAllowance ? pfWages : evaluation.Basic, pfRule.WageCeiling ?? decimal.MaxValue) } } };
		if (applicationProfile is { ApplyPf: true } && pfEligibility.Status != "NOT_APPLICABLE")
		{
			if (pfRule == null) throw new BusinessException("HR:Payroll:PfRuleNotFound");
			if (!rules.Any(x => x.FormulaType == SalaryTemplateFormulaType.EmployeeProvidentFund))
				throw new BusinessException("HR:Payroll:SalaryTemplatePfFormulaRequired");
		}
		MarkCustomizedLines(evaluation.Earnings, input.Overrides);
		MarkCustomizedLines(evaluation.Deductions, input.Overrides);
		var protectedAmount = protectionPolicy switch
		{
			CompensationProtectionPolicy.ProtectTakeHome => evaluation.Net,
			CompensationProtectionPolicy.ProtectEmployerCost => evaluation.EmployerCost,
			_ => evaluation.Gross
		};
		var achievedTarget = basis switch
		{
			SalaryInputBasis.TakeHome => evaluation.Net,
			SalaryInputBasis.EmployerCost => evaluation.EmployerCost,
			_ => evaluation.Gross
		};
		var applicationVariance = false;
		if (preserveGross && basis == SalaryInputBasis.EmployerCost && evaluation.StatutoryApplication != null)
		{
			var previous = System.Text.Json.JsonSerializer.Deserialize<SalaryTemplateCalculationDto>(savedRevision!.CalculationSnapshotJson)!;
			applicationVariance = CompensationStatutoryVariance.IsExplained(previous, evaluation.StatutoryApplication,
				CompensationProtectionPolicy.ProtectEmployerCost, evaluation.EmployerCost - input.MonthlyAmount);
			if (applicationVariance) protectedAmount = input.MonthlyAmount;
		}
		var esiResult = evaluation.EmployeeStateInsurance;
		var issueCodes = esiResult?.IssueCodes.ToList() ?? [];

		var response = new SalaryTemplateCalculationDto
		{
			StatutoryApplication = evaluation.StatutoryApplication,
			PfEnrollment = input.PfEnrollment,
			PfEligibility = pfEligibility,
			TemplateId = templateId,
			TemplateVersionId = versionId,
			TemplateVersion = versionNumber,
			SalaryBasis = ToContractEnum(basis.ToString()),
			ProtectionPolicy = ToContractEnum(protectionPolicy.ToString()),
			QuotedMonthlyAmount = input.MonthlyAmount,
			EffectiveMonth = effectiveMonth,
			GrossSalary = evaluation.Gross,
			BasicSalary = evaluation.Basic,
			TotalDeductions = evaluation.TotalDeductions,
			NetSalary = evaluation.Net,
			EmployerContributionTotal = evaluation.EmployerContributionTotal,
			EmployerCost = evaluation.EmployerCost,
			ProtectedMonthlyAmount = protectedAmount,
			TargetResult = new SalaryTemplateTargetResultDto
			{
				Requested = input.MonthlyAmount,
				Achieved = achievedTarget,
				Difference = achievedTarget - input.MonthlyAmount,
				IsExplainedByEnrollment = applicationVariance,
				IsExact = Math.Abs(achievedTarget - input.MonthlyAmount) <= 0.01m
			},
			EmployeeStateInsurance = new SalaryTemplateEsiPreviewDto
			{
				ConfigurationId = esiResolution.ConfigurationId,
				EligibilityWages = esiResult?.EligibilityWages ?? 0m,
				EmployeePercentage = esiResult?.EmployeePercentage ?? 0m,
				EmployerPercentage = esiResult?.EmployerPercentage ?? 0m,
				CalculationSettings = esiResolution.Context?.CalculationSettings ?? new(),
				IsConfigured = esiResolution.Context != null,
				IsCovered = esiResult?.IsCovered ?? false,
				CanCalculate = esiResult?.CanCalculate ?? esiResolution.Warnings.Count == 0,
				RegistrationPending = esiResolution.RegistrationPending,
				CoverageDecision = esiResult?.CoverageDecision,
				StatutoryWages = esiResult?.StatutoryWages ?? 0m,
				WageCeiling = esiResult?.WageCeiling ?? 0m,
				EmployeeContribution = esiResult?.EmployeeContribution ?? 0m,
				EmployerContribution = esiResult?.EmployerContribution ?? 0m,
				GovernmentContribution = esiResult?.GovernmentContribution ?? 0m,
				RuleId = esiResolution.Context?.Rule.Id,
				RuleVersion = esiResolution.Context?.Rule.Version,
				IssueCodes = issueCodes.Concat(esiResolution.Warnings).Distinct().ToList()
			},
			Earnings = evaluation.Earnings,
			Deductions = evaluation.Deductions,
			EmployerContributions = evaluation.EmployerContributions,
			FormulaTrace = evaluation.Trace,
			Warnings = esiResolution.Warnings.Concat(applicationVariance ? new[] { "STATUTORY_APPLICATION_COST_VARIANCE" } : []).Concat(issueCodes).Concat(evaluation.StatutoryApplication?.Warnings ?? []).Distinct().ToList(),
			PfRuleId = pfRule?.Id,
			PfRuleVersion = pfRule?.Version,
			ProfessionalTaxProfile = input.StatutoryProfile,
			GeneratedAt = clock.Now
		};
		var ptIncluded = rules.Any(x => x.FormulaType == SalaryTemplateFormulaType.ProfessionalTax);
		var ptSlab = professionalTaxSlabs.Where(x => evaluation.Gross >= x.FromSalary && (!x.ToSalary.HasValue || evaluation.Gross <= x.ToSalary)).OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Version).FirstOrDefault();
		response.StatutorySummary = [SalaryStatutorySummary.Pf(evaluation.StatutoryApplication?.ProvidentFund, pfEligibility, pfRule),
			SalaryStatutorySummary.Esi(esiResult, evaluation.StatutoryApplication?.EmployeeStateInsurance, esiResolution.IsEnabled),
			SalaryStatutorySummary.ProfessionalTax(ptIncluded, ptSlab)];
		response.StatutorySummary[1].RuleId = response.EmployeeStateInsurance.RuleId;
		response.StatutorySummary[1].RuleVersion = response.EmployeeStateInsurance.RuleVersion;
		return response;
	}

	private async Task<Dictionary<Guid, SalaryTemplateComponent>> LoadComponentsAsync(IReadOnlyList<SalaryTemplateRule> rules)
	{
		var earningIds = rules.Where(x => x.LineKind == SalaryTemplateLineKind.Earning).Select(x => x.ComponentId).Distinct().ToList();
		var deductionIds = rules.Where(x => x.LineKind == SalaryTemplateLineKind.Deduction).Select(x => x.ComponentId).Distinct().ToList();
		var earnings = earningIds.Count == 0 ? [] : await asyncExecuter.ToListAsync(
			(await earningComponentRepository.GetQueryableAsync()).Where(x => earningIds.Contains(x.Id) && x.IsActive));
		var deductions = deductionIds.Count == 0 ? [] : await asyncExecuter.ToListAsync(
			(await deductionComponentRepository.GetQueryableAsync()).Where(x => deductionIds.Contains(x.Id) && x.IsActive));
		var components = earnings.Select(x => new SalaryTemplateComponent(x.Id, x.Name))
			.Concat(deductions.Select(x => new SalaryTemplateComponent(x.Id, x.Name))).ToDictionary(x => x.Id);
		if (components.Count != earningIds.Count + deductionIds.Count)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateComponentMissing");
		}
		return components;
	}

	private static void MarkCustomizedLines(List<CalculatedSalaryLineDto> lines, IReadOnlyList<SalaryRuleOverrideDto> overrides)
	{
		var ids = overrides.Select(x => x.ComponentId).ToHashSet();
		foreach (var line in lines.Where(x => ids.Contains(x.ComponentId)))
		{
			line.LineSource = "CUSTOMIZED";
		}
	}

	private static SalaryInputBasis ParseSalaryBasis(string value) => value switch
	{
		"GROSS" => SalaryInputBasis.Gross,
		"EMPLOYER_COST" => SalaryInputBasis.EmployerCost,
		_ => throw new BusinessException("HR:Payroll:SalaryTemplateRuleInvalidEnum").WithData("field", "salaryBasis")
	};
	private static CompensationProtectionPolicy ParseProtectionPolicy(string value) => ParseEnum<CompensationProtectionPolicy>(value, nameof(value));
	private static string NormalizeEnum(string? value) => value?.Replace("_", string.Empty).Replace("-", string.Empty) ?? string.Empty;

	private static TEnum ParseEnum<TEnum>(string value, string fieldName) where TEnum : struct, Enum
	{
		if (Enum.TryParse<TEnum>(NormalizeEnum(value), true, out var result))
		{
			return result;
		}
		throw new BusinessException("HR:Payroll:SalaryTemplateRuleInvalidEnum").WithData("field", fieldName);
	}

	private static string ToContractEnum(string value) =>
		string.Concat(value.Select((c, index) => index > 0 && char.IsUpper(c) ? "_" + c : c.ToString())).ToUpperInvariant();

}