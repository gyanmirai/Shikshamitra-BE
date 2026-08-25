using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.Payroll.Templates;

[RemoteService(false)]
public class SalaryTemplateAppService(
	IRepository<SalaryStructureTemplate, Guid> templateRepository,
	IRepository<SalaryStructureTemplateVersion, Guid> versionRepository,
	IEarningComponentRepository earningComponentRepository,
	IDeductionComponentRepository deductionComponentRepository,
	IProvidentFundRepository providentFundRepository,
	IProfessionalTaxRepository professionalTaxRepository,
	SalaryTemplateEsiContextResolver esiContextResolver,
	IAsyncQueryableExecuter asyncExecuter,
	IClock clock) : ApplicationService, ISalaryTemplateAppService
{
	private const string CanonicalEsiComponentName = "Employee State Insurance (ESI) - Auto";

	public async Task<SalaryTemplateListDto> GetListAsync()
	{
		var templates = await asyncExecuter.ToListAsync(
			(await templateRepository.GetQueryableAsync()).OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name));
		var versions = await asyncExecuter.ToListAsync(await versionRepository.GetQueryableAsync());
		return new SalaryTemplateListDto
		{
			Items = templates.Select(x => Map(x, FindLatest(x, versions), FindPublished(x, versions))).ToList()
		};
	}

	public async Task<SalaryTemplateListDto> GetPublishedListAsync()
	{
		var templates = await asyncExecuter.ToListAsync(
			(await templateRepository.GetQueryableAsync())
				.Where(x => x.Status == SalaryTemplateStatus.Published && x.PublishedVersion.HasValue)
				.OrderByDescending(x => x.IsDefault)
				.ThenBy(x => x.Name));
		var templateIds = templates.Select(x => x.Id).ToList();
		var versions = await asyncExecuter.ToListAsync(
			(await versionRepository.GetQueryableAsync()).Where(x => templateIds.Contains(x.TemplateId)));
		return new SalaryTemplateListDto
		{
			Items = templates.Select(x => Map(x, FindLatest(x, versions), FindPublished(x, versions))).ToList()
		};
	}

	public async Task<SalaryTemplateComponentCatalogDto> GetComponentCatalogAsync()
	{
		var earnings = await asyncExecuter.ToListAsync(
			(await earningComponentRepository.GetQueryableAsync())
				.Where(x => x.IsActive)
				.OrderBy(x => x.DisplayOrder)
				.ThenBy(x => x.Name));
		var deductions = await asyncExecuter.ToListAsync(
			(await deductionComponentRepository.GetQueryableAsync())
				.Where(x => x.IsActive && x.Name != CanonicalEsiComponentName)
				.OrderBy(x => x.DisplayOrder)
				.ThenBy(x => x.Name));

		return new SalaryTemplateComponentCatalogDto
		{
			Items = earnings.Select(x => new SalaryTemplateComponentCatalogItemDto
				{
					ComponentId = x.Id,
					Name = x.Name,
					Description = x.Description,
					LineKind = "EARNING"
				})
				.Concat(deductions.Select(x => new SalaryTemplateComponentCatalogItemDto
				{
					ComponentId = x.Id,
					Name = x.Name,
					Description = x.Description,
					LineKind = "DEDUCTION"
				}))
				.ToList()
		};
	}

	public async Task<SalaryTemplateDto> GetAsync(Guid id)
	{
		var template = await templateRepository.GetAsync(id);
		var versions = await GetVersionsAsync(template.Id);
		return Map(template, FindLatest(template, versions), FindPublished(template, versions));
	}

	public async Task<SalaryTemplateDto> CreateAsync(CreateSalaryTemplateDto input)
	{
		return await CreateTemplateAsync(input.Name, input.Description, []);
	}

	public async Task<SalaryTemplateDto> CreateNextVersionAsync(Guid id)
	{
		var template = await templateRepository.GetAsync(id);
		EnsureNotArchived(template);
		var latest = await GetLatestVersionAsync(template);
		if (latest.Status == SalaryTemplateStatus.Draft)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateDraftAlreadyExists");
		}

		var version = new SalaryStructureTemplateVersion(
			GuidGenerator.Create(), CurrentTenant.Id, template.Id, template.ReserveNextVersion(), latest.GetRules(),
			latest.DefaultSalaryBasis, latest.DefaultProtectionPolicy);
		await templateRepository.UpdateAsync(template, autoSave: true);
		await versionRepository.InsertAsync(version, autoSave: true);
		return Map(template, version, latest);
	}

	public async Task<SalaryTemplateDto> UpdateDraftAsync(Guid id, Guid versionId, UpdateSalaryTemplateDraftDto input)
	{
		var template = await templateRepository.GetAsync(id);
		EnsureNotArchived(template);
		var version = await versionRepository.GetAsync(versionId);
		EnsureVersionBelongsToTemplate(version, template.Id);
		EnsureLatestVersion(template, version);
		await EnsureNameAvailableAsync(input.Name, template.Id);
		var rules = MapRules(input.Rules);
		await ValidateDraftRulesAndComponentsAsync(rules);
		template.SetDetails(input.Name, input.Description);
		version.SetRules(rules);
		version.SetQuoteDefaults(
			ParseSalaryBasis(input.DefaultSalaryBasis),
			ParseProtectionPolicy(input.DefaultProtectionPolicy));
		await templateRepository.UpdateAsync(template, autoSave: true);
		await versionRepository.UpdateAsync(version, autoSave: true);
		return Map(template, version, await GetPublishedVersionAsync(template));
	}

	public async Task<SalaryTemplateDto> PublishAsync(Guid id, Guid versionId)
	{
		var template = await templateRepository.GetAsync(id);
		EnsureNotArchived(template);
		var version = await versionRepository.GetAsync(versionId);
		EnsureVersionBelongsToTemplate(version, template.Id);
		EnsureLatestVersion(template, version);
		await ValidatePublishableRulesAndComponentsAsync(version.GetRules());
		version.Publish(clock.Now);
		template.MarkPublished(version.VersionNumber);
		await versionRepository.UpdateAsync(version, autoSave: true);
		await templateRepository.UpdateAsync(template, autoSave: true);
		return Map(template, version, version);
	}

	public async Task<SalaryTemplateDto> CloneAsync(Guid id, string name)
	{
		var source = await templateRepository.GetAsync(id);
		EnsureNotArchived(source);
		var sourceVersion = await GetPublishedVersionAsync(source) ?? await GetLatestVersionAsync(source);
		return await CreateTemplateAsync(
			name,
			source.Description,
			sourceVersion.GetRules(),
			sourceVersion.DefaultSalaryBasis,
			sourceVersion.DefaultProtectionPolicy);
	}

	public async Task ArchiveAsync(Guid id)
	{
		var template = await templateRepository.GetAsync(id);
		template.Archive();
		await templateRepository.UpdateAsync(template, autoSave: true);
	}

	public async Task<SalaryTemplateDto> SetDefaultAsync(Guid id)
	{
		var template = await templateRepository.GetAsync(id);
		if (template.Status != SalaryTemplateStatus.Published || !template.PublishedVersion.HasValue)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateMustBePublished");
		}

		var existingDefaults = await asyncExecuter.ToListAsync(
			(await templateRepository.GetQueryableAsync()).Where(x => x.IsDefault && x.Id != id));
		foreach (var currentDefault in existingDefaults)
		{
			currentDefault.SetDefault(false);
			await templateRepository.UpdateAsync(currentDefault, autoSave: false);
		}

		await UnitOfWorkManager.Current!.SaveChangesAsync();
		template.SetDefault(true);
		await templateRepository.UpdateAsync(template, autoSave: true);
		var versions = await GetVersionsAsync(template.Id);
		return Map(template, FindLatest(template, versions), FindPublished(template, versions));
	}

	public async Task<SalaryTemplateCalculationDto> CalculateAsync(
		Guid templateVersionId,
		CalculateSalaryTemplateRequestDto input)
	{
		var version = await versionRepository.GetAsync(templateVersionId);
		var template = await templateRepository.GetAsync(version.TemplateId);
		EnsureNotArchived(template);
		if (version.Status != SalaryTemplateStatus.Published)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateVersionNotPublished");
		}

		var rules = version.GetRules();
		var components = await LoadComponentsAsync(rules);
		var effectiveMonth = new DateTime(input.EffectiveMonth.Year, input.EffectiveMonth.Month, 1);
		var effectiveMonthEnd = effectiveMonth.AddMonths(1).AddDays(-1);
		var pfRule = input.PfRuleId.HasValue
			? await providentFundRepository.FindAsync(input.PfRuleId.Value)
			: null;
		if (input.PfRuleId.HasValue && pfRule == null)
		{
			throw new BusinessException("HR:Payroll:PfRuleNotFound").WithData("pfRuleId", input.PfRuleId.Value);
		}

		if (pfRule != null && (!pfRule.IsActive || pfRule.EffectiveFrom > effectiveMonthEnd || pfRule.EffectiveTo < effectiveMonth))
		{
			throw new BusinessException("HR:Payroll:PfRuleNotEffective").WithData("pfRuleId", pfRule.Id);
		}

		var professionalTaxSlabs = await asyncExecuter.ToListAsync(
			(await professionalTaxRepository.GetQueryableAsync()).Where(x =>
				x.IsActive &&
				x.JurisdictionCode == input.StatutoryProfile &&
				x.EffectiveFrom <= effectiveMonthEnd &&
				(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= effectiveMonth)));
		var basis = ParseSalaryBasis(input.SalaryBasis);
		var protectionPolicy = ParseProtectionPolicy(input.ProtectionPolicy);
		var esiResolution = await esiContextResolver.ResolveAsync(
			effectiveMonth,
			input.StatutoryProfile,
			input.EmployeeId);
		var evaluation = SalaryTemplateCalculator.Calculate(
			basis, input.MonthlyAmount, rules, components, pfRule, professionalTaxSlabs, esiResolution.Context);
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
		var esiResult = evaluation.EmployeeStateInsurance;

		return new SalaryTemplateCalculationDto
		{
			TemplateId = template.Id,
			TemplateVersionId = version.Id,
			TemplateVersion = version.VersionNumber,
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
				IsExact = Math.Abs(achievedTarget - input.MonthlyAmount) <= 0.01m
			},
			EmployeeStateInsurance = new SalaryTemplateEsiPreviewDto
			{
				IsConfigured = esiResolution.Context != null,
				IsCovered = esiResult?.IsCovered ?? false,
				CanCalculate = esiResult?.CanCalculate ?? false,
				CoverageDecision = esiResult?.CoverageDecision,
				StatutoryWages = esiResult?.StatutoryWages ?? 0m,
				WageCeiling = esiResult?.WageCeiling ?? 0m,
				EmployeeContribution = esiResult?.EmployeeContribution ?? 0m,
				EmployerContribution = esiResult?.EmployerContribution ?? 0m,
				GovernmentContribution = esiResult?.GovernmentContribution ?? 0m,
				RuleId = esiResolution.Context?.Rule.Id,
				RuleVersion = esiResolution.Context?.Rule.Version,
				IssueCodes = esiResult?.IssueCodes.ToList() ?? []
			},
			Earnings = evaluation.Earnings,
			Deductions = evaluation.Deductions,
			EmployerContributions = evaluation.EmployerContributions,
			FormulaTrace = evaluation.Trace,
			Warnings = BuildWarnings(rules, pfRule, professionalTaxSlabs)
				.Concat(esiResolution.Warnings)
				.Concat(esiResult?.IssueCodes ?? [])
				.Distinct()
				.ToList(),
			PfRuleId = pfRule?.Id,
			PfRuleVersion = pfRule?.Version,
			ProfessionalTaxProfile = input.StatutoryProfile,
			GeneratedAt = clock.Now
		};
	}

	private async Task<Dictionary<Guid, SalaryTemplateComponent>> LoadComponentsAsync(IReadOnlyList<SalaryTemplateRule> rules)
	{
		var earningIds = rules.Where(x => x.LineKind == SalaryTemplateLineKind.Earning).Select(x => x.ComponentId).Distinct().ToList();
		var deductionIds = rules.Where(x => x.LineKind == SalaryTemplateLineKind.Deduction).Select(x => x.ComponentId).Distinct().ToList();
		var earnings = earningIds.Count == 0
			? []
			: await asyncExecuter.ToListAsync((await earningComponentRepository.GetQueryableAsync()).Where(x => earningIds.Contains(x.Id) && x.IsActive));
		var deductions = deductionIds.Count == 0
			? []
			: await asyncExecuter.ToListAsync((await deductionComponentRepository.GetQueryableAsync()).Where(x => deductionIds.Contains(x.Id) && x.IsActive));
		var components = earnings.Select(x => new SalaryTemplateComponent(x.Id, x.Name))
			.Concat(deductions.Select(x => new SalaryTemplateComponent(x.Id, x.Name)))
			.ToDictionary(x => x.Id);
		if (components.Count != earningIds.Count + deductionIds.Count)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateComponentMissing");
		}

		return components;
	}

	private async Task ValidateDraftRulesAndComponentsAsync(IReadOnlyList<SalaryTemplateRule> rules)
	{
		SalaryTemplateCalculator.ValidateDraftRules(rules);
		await LoadComponentsAsync(rules);
	}

	private async Task ValidatePublishableRulesAndComponentsAsync(IReadOnlyList<SalaryTemplateRule> rules)
	{
		SalaryTemplateCalculator.ValidateRules(rules);
		await LoadComponentsAsync(rules);
	}

	private async Task<SalaryTemplateDto> CreateTemplateAsync(
		string name,
		string? description,
		IReadOnlyList<SalaryTemplateRule> rules,
		SalaryInputBasis defaultSalaryBasis = SalaryInputBasis.Gross,
		CompensationProtectionPolicy defaultProtectionPolicy = CompensationProtectionPolicy.ProtectGross)
	{
		await EnsureNameAvailableAsync(name);
		await ValidateDraftRulesAndComponentsAsync(rules);
		var template = new SalaryStructureTemplate(GuidGenerator.Create(), CurrentTenant.Id, name, description);
		var version = new SalaryStructureTemplateVersion(
			GuidGenerator.Create(), CurrentTenant.Id, template.Id, 1, rules,
			defaultSalaryBasis, defaultProtectionPolicy);
		await templateRepository.InsertAsync(template, autoSave: true);
		await versionRepository.InsertAsync(version, autoSave: true);
		return Map(template, version, null);
	}

	private async Task EnsureNameAvailableAsync(string name, Guid? excludedId = null)
	{
		var normalized = name.Trim().ToLower();
		var exists = await asyncExecuter.AnyAsync((await templateRepository.GetQueryableAsync()).Where(x =>
			x.Id != excludedId && x.Name.ToLower() == normalized));
		if (exists)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateNameExists").WithData("name", name);
		}
	}

	private async Task<SalaryStructureTemplateVersion> GetLatestVersionAsync(SalaryStructureTemplate template)
	{
		var version = await asyncExecuter.FirstOrDefaultAsync(
			(await versionRepository.GetQueryableAsync()).Where(x =>
				x.TemplateId == template.Id && x.VersionNumber == template.LatestVersion));
		return version ?? throw new EntityNotFoundException(typeof(SalaryStructureTemplateVersion), template.Id);
	}

	private async Task<IReadOnlyList<SalaryStructureTemplateVersion>> GetVersionsAsync(Guid templateId) =>
		await asyncExecuter.ToListAsync(
			(await versionRepository.GetQueryableAsync()).Where(x => x.TemplateId == templateId));

	private async Task<SalaryStructureTemplateVersion?> GetPublishedVersionAsync(SalaryStructureTemplate template)
	{
		if (!template.PublishedVersion.HasValue)
		{
			return null;
		}

		return await asyncExecuter.FirstOrDefaultAsync(
			(await versionRepository.GetQueryableAsync()).Where(x =>
				x.TemplateId == template.Id &&
				x.VersionNumber == template.PublishedVersion.Value &&
				x.Status == SalaryTemplateStatus.Published));
	}

	private static SalaryStructureTemplateVersion FindLatest(
		SalaryStructureTemplate template,
		IReadOnlyList<SalaryStructureTemplateVersion> versions) =>
		versions.Single(x => x.TemplateId == template.Id && x.VersionNumber == template.LatestVersion);

	private static SalaryStructureTemplateVersion? FindPublished(
		SalaryStructureTemplate template,
		IReadOnlyList<SalaryStructureTemplateVersion> versions) =>
		template.PublishedVersion.HasValue
			? versions.SingleOrDefault(x =>
				x.TemplateId == template.Id &&
				x.VersionNumber == template.PublishedVersion.Value &&
				x.Status == SalaryTemplateStatus.Published)
			: null;

	private static SalaryTemplateDto Map(
		SalaryStructureTemplate template,
		SalaryStructureTemplateVersion latest,
		SalaryStructureTemplateVersion? published) => new()
	{
		Id = template.Id,
		Name = template.Name,
		Description = template.Description,
		Status = template.Status.ToString().ToUpperInvariant(),
		IsDefault = template.IsDefault,
		LatestVersion = template.LatestVersion,
		PublishedVersion = template.PublishedVersion,
		Latest = MapVersion(latest),
		Published = published == null ? null : MapVersion(published)
	};

	private static SalaryTemplateVersionDto MapVersion(SalaryStructureTemplateVersion version) => new()
	{
		Id = version.Id,
		VersionNumber = version.VersionNumber,
		Status = version.Status.ToString().ToUpperInvariant(),
		PublishedAt = version.PublishedAt,
		DefaultSalaryBasis = ToContractEnum(version.DefaultSalaryBasis.ToString()),
		DefaultProtectionPolicy = ToContractEnum(version.DefaultProtectionPolicy.ToString()),
		Rules = MapRulesForContract(version.GetRules())
	};

	private static List<SalaryTemplateRule> MapRules(IEnumerable<SalaryTemplateRuleDto> rules)
	{
		var inputs = rules.ToList();
		var codesByComponentId = inputs
			.Where(x => x.ComponentId != Guid.Empty)
			.GroupBy(x => x.ComponentId)
			.ToDictionary(x => x.Key, x => BuildRuleCode(x.Key));

		return inputs.Select(x => new SalaryTemplateRule(
			codesByComponentId.GetValueOrDefault(x.ComponentId, BuildRuleCode(x.ComponentId)),
			x.ComponentId,
			ParseEnum<SalaryTemplateLineKind>(x.LineKind, nameof(x.LineKind)),
			ParseEnum<SalaryTemplateFormulaType>(x.FormulaType, nameof(x.FormulaType)),
			ParseEnum<PayrollComponentSemanticType>(x.SemanticType, nameof(x.SemanticType)),
			x.Value,
			ResolveBasisCode(x, codesByComponentId),
			x.DisplayOrder)).ToList();
	}

	private static List<SalaryTemplateRuleDto> MapRulesForContract(IReadOnlyList<SalaryTemplateRule> rules)
	{
		var componentIdsByCode = rules.ToDictionary(x => x.Code, x => x.ComponentId, StringComparer.OrdinalIgnoreCase);
		return rules.OrderBy(x => x.DisplayOrder).Select(rule => new SalaryTemplateRuleDto
		{
			Code = rule.Code,
			ComponentId = rule.ComponentId,
			LineKind = ToContractEnum(rule.LineKind.ToString()),
			FormulaType = ToContractEnum(rule.FormulaType.ToString()),
			SemanticType = ToContractEnum(rule.SemanticType.ToString()),
			Value = rule.Value,
			BasisCode = rule.BasisCode,
			BasisComponentId = string.IsNullOrWhiteSpace(rule.BasisCode)
				? null
				: componentIdsByCode.GetValueOrDefault(rule.BasisCode),
			DisplayOrder = rule.DisplayOrder
		}).ToList();
	}

	private static string BuildRuleCode(Guid componentId) => $"COMPONENT_{componentId:N}".ToUpperInvariant();

	private static string? ResolveBasisCode(
		SalaryTemplateRuleDto rule,
		IReadOnlyDictionary<Guid, string> codesByComponentId)
	{
		if (rule.BasisComponentId.HasValue)
		{
			return codesByComponentId.GetValueOrDefault(
				rule.BasisComponentId.Value,
				BuildRuleCode(rule.BasisComponentId.Value));
		}

		return null;
	}

	private static SalaryInputBasis ParseSalaryBasis(string value) =>
		ParseEnum<SalaryInputBasis>(value, nameof(value));

	private static CompensationProtectionPolicy ParseProtectionPolicy(string value) =>
		ParseEnum<CompensationProtectionPolicy>(value, nameof(value));

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

	private static List<string> BuildWarnings(
		IReadOnlyList<SalaryTemplateRule> rules,
		ProvidentFund? pfRule,
		IReadOnlyList<ProfessionalTax> slabs)
	{
		var warnings = new List<string>();
		if (rules.Any(x => x.FormulaType == SalaryTemplateFormulaType.EmployeeProvidentFund) && pfRule == null)
		{
			warnings.Add("PF is present in the template but no PF rule was selected, so no PF amount was generated.");
		}
		if (rules.Any(x => x.FormulaType == SalaryTemplateFormulaType.ProfessionalTax) && slabs.Count == 0)
		{
			warnings.Add("No effective Gujarat professional-tax slabs were found.");
		}
		return warnings;
	}

	private static void EnsureNotArchived(SalaryStructureTemplate template)
	{
		if (template.Status == SalaryTemplateStatus.Archived)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateArchived");
		}
	}

	private static void EnsureVersionBelongsToTemplate(SalaryStructureTemplateVersion version, Guid templateId)
	{
		if (version.TemplateId != templateId)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateVersionMismatch");
		}
	}

	private static void EnsureLatestVersion(SalaryStructureTemplate template, SalaryStructureTemplateVersion version)
	{
		if (version.VersionNumber != template.LatestVersion)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateVersionNotLatest");
		}
	}
}
