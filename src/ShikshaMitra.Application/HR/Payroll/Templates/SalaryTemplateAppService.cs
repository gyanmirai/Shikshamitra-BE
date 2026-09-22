using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
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
	SalaryTemplateCalculationService calculationService,
	SalaryTemplatePreviewProofService previewProofService,
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
			Items = templates
				.Select(template => Map(template, versions.Where(x => x.TemplateId == template.Id).ToList()))
				.ToList()
		};
	}

	public async Task<SalaryTemplateListDto> GetActiveListAsync()
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
			Items = templates
				.Select(template => Map(template, versions.Where(x => x.TemplateId == template.Id).ToList()))
				.Where(x => x.Status == "ACTIVE")
				.ToList()
		};
	}

	public async Task<SalaryTemplateComponentCatalogDto> GetComponentCatalogAsync()
	{
		var earnings = await asyncExecuter.ToListAsync(
			(await earningComponentRepository.GetQueryableAsync()).Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name));
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
					LineKind = "EARNING",
					CalculationType = x.CalculationType.ToString().ToUpperInvariant(),
					CalculationBase = x.DependOn == ComponentDependency.Gross ? "GROSS" : "COMPONENT",
					DefaultValue = x.CalculationType == CalculationType.Percentage ? x.Percentage : x.Amount,
					BasisComponentId = x.SelectedEarningComponentId
				})
				.Concat(deductions.Select(x => new SalaryTemplateComponentCatalogItemDto
				{
					ComponentId = x.Id,
					Name = x.Name,
					Description = x.Description,
					LineKind = "DEDUCTION",
					CalculationType = x.CalculationType.ToString().ToUpperInvariant(),
					CalculationBase = x.DependOn == ComponentDependency.Gross ? "GROSS" : "COMPONENT",
					DefaultValue = x.CalculationType == CalculationType.Percentage ? x.Percentage : x.Amount,
					BasisComponentId = x.SelectedDeductionComponentId
				}))
				.ToList()
		};
	}

	public async Task<SalaryTemplateDto> GetAsync(Guid id)
	{
		var template = await templateRepository.GetAsync(id);
		return Map(template, await LoadVersionsAsync(id));
	}

	public async Task<SalaryTemplateVersionListDto> GetVersionsAsync(Guid id)
	{
		var template = await templateRepository.GetAsync(id);
		var versions = await LoadVersionsAsync(id);
		return new SalaryTemplateVersionListDto
		{
			Items = versions.OrderByDescending(x => x.VersionNumber).Select(x => MapVersion(x, template.PublishedVersion)).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Add)]
	public async Task<SalaryTemplateDto> CreateAsync(SaveSalaryTemplateDto input)
	{
		var replay = await FindIdempotentSaveAsync(input.IdempotencyKey);
		if (replay != null)
		{
			previewProofService.Validate(input.PreviewToken, PreviewBinding(ToPreview(input)), replay: true);
			return await GetAsync(replay.TemplateId);
		}

		await EnsureNameAvailableAsync(input.Name);
		var rules = MapRules(input.Rules);
		await ValidateSaveAsync(input);

		var template = new SalaryStructureTemplate(GuidGenerator.Create(), CurrentTenant.Id, input.Name, input.Description);
		var version = NewVersion(template, 1, rules, input);
		version.Publish(clock.Now);
		template.MarkPublished(1);
		var hasDefault = await asyncExecuter.AnyAsync((await templateRepository.GetQueryableAsync()).Where(x => x.IsDefault));
		if (!hasDefault)
		{
			template.SetDefault(true);
		}

		await templateRepository.InsertAsync(template, autoSave: false);
		await versionRepository.InsertAsync(version, autoSave: false);
		await UnitOfWorkManager.Current!.SaveChangesAsync();
		return Map(template, [version]);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Edit)]
	public async Task<SalaryTemplateDto> UpdateAsync(Guid id, SaveSalaryTemplateDto input)
	{
		var template = await templateRepository.GetAsync(id);
		EnsureNotArchived(template);
		var replay = await FindIdempotentSaveAsync(input.IdempotencyKey, id);
		if (replay != null)
		{
			previewProofService.Validate(input.PreviewToken, PreviewBinding(ToPreview(input, id)), replay: true);
			return await GetAsync(id);
		}

		var versions = await LoadVersionsAsync(id);
		var active = FindActive(template, versions);
		if (input.ExpectedActiveVersionId != active?.Id ||
			string.IsNullOrWhiteSpace(input.ConcurrencyStamp) ||
			input.ConcurrencyStamp != template.ConcurrencyStamp)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateSaveConflict")
				.WithData("currentActiveVersionId", active?.Id.ToString() ?? "none")
				.WithData("currentActiveVersion", active?.VersionNumber.ToString() ?? "none")
				.WithData("currentConcurrencyStamp", template.ConcurrencyStamp);
		}

		await EnsureNameAvailableAsync(input.Name, template.Id);
		var rules = MapRules(input.Rules);
		await ValidateSaveAsync(input, id);
		template.SetDetails(input.Name, input.Description);

		SalaryStructureTemplateVersion version;
		if (active == null)
		{
			version = versions.Single(x => x.VersionNumber == template.LatestVersion);
			version.SetRules(rules);
			version.SetQuoteDefaults(ParseSalaryBasis(input.DefaultSalaryBasis), ParseProtectionPolicy(input.DefaultProtectionPolicy));
			version.SetPfDefaults(input.DefaultPfApplicable, input.DefaultPfRuleId);
			version.SetSaveOperationKey(input.IdempotencyKey);
			version.Publish(clock.Now);
		}
		else
		{
			version = NewVersion(template, template.ReserveNextVersion(), rules, input);
			version.Publish(clock.Now);
			await versionRepository.InsertAsync(version, autoSave: false);
		}

		template.MarkPublished(version.VersionNumber);
		await templateRepository.UpdateAsync(template, autoSave: false);
		await versionRepository.UpdateAsync(version, autoSave: false);
		await UnitOfWorkManager.Current!.SaveChangesAsync();
		return await GetAsync(id);
	}

	public async Task ArchiveAsync(Guid id, ArchiveSalaryTemplateDto input)
	{
		var template = await templateRepository.GetAsync(id);
		if (template.IsDefault)
		{
			if (!input.ReplacementDefaultTemplateId.HasValue || input.ReplacementDefaultTemplateId.Value == id)
			{
				throw new BusinessException("HR:Payroll:SalaryTemplateReplacementDefaultRequired");
			}

			var replacement = await templateRepository.GetAsync(input.ReplacementDefaultTemplateId.Value);
			var replacementVersions = await LoadVersionsAsync(replacement.Id);
			if (replacement.Status != SalaryTemplateStatus.Published ||
				!replacement.PublishedVersion.HasValue ||
				RequiresSetup(FindActive(replacement, replacementVersions)))
			{
				throw new BusinessException("HR:Payroll:SalaryTemplateReplacementDefaultInvalid");
			}

			template.SetDefault(false);
			// Release the filtered unique default key before assigning its replacement.
			await templateRepository.UpdateAsync(template, autoSave: true);
			replacement.SetDefault(true);
			await templateRepository.UpdateAsync(replacement, autoSave: false);
		}

		template.Archive();
		await templateRepository.UpdateAsync(template, autoSave: true);
	}

	public async Task<SalaryTemplateDto> SetDefaultAsync(Guid id)
	{
		var template = await templateRepository.GetAsync(id);
		var versions = await LoadVersionsAsync(id);
		if (template.Status != SalaryTemplateStatus.Published ||
			!template.PublishedVersion.HasValue ||
			RequiresSetup(FindActive(template, versions)))
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateMustBeActive");
		}

		var existingDefaults = await asyncExecuter.ToListAsync(
			(await templateRepository.GetQueryableAsync()).Where(x => x.IsDefault && x.Id != id));
		foreach (var currentDefault in existingDefaults)
		{
			currentDefault.SetDefault(false);
			await templateRepository.UpdateAsync(currentDefault, autoSave: false);
		}
		// Both saves remain in the request transaction; EF batch ordering cannot
		// assign the new default before the previous unique key has been released.
		await UnitOfWorkManager.Current!.SaveChangesAsync();
		template.SetDefault(true);
		await templateRepository.UpdateAsync(template, autoSave: true);
		return await GetAsync(id);
	}

	public async Task<SalaryTemplateCalculationDto> CalculateAsync(Guid templateVersionId, CalculateSalaryTemplateRequestDto input)
	{
		var version = await versionRepository.GetAsync(templateVersionId);
		var template = await templateRepository.GetAsync(version.TemplateId);
		EnsureNotArchived(template);
		if (version.Status != SalaryTemplateStatus.Published)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateVersionNotActive");
		}

		var sourceRules = version.GetRules();
		var rules = ApplyOverrides(sourceRules, input.Overrides);
		return await calculationService.CalculateAsync(rules, input, template.Id, version.Id, version.VersionNumber);
	}

	private SalaryStructureTemplateVersion NewVersion(
		SalaryStructureTemplate template,
		int versionNumber,
		IReadOnlyList<SalaryTemplateRule> rules,
		SaveSalaryTemplateDto input) => new(
		GuidGenerator.Create(), CurrentTenant.Id, template.Id, versionNumber, rules,
		ParseSalaryBasis(input.DefaultSalaryBasis), ParseProtectionPolicy(input.DefaultProtectionPolicy),
		input.DefaultPfApplicable, input.DefaultPfRuleId, input.IdempotencyKey);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View)]
	public async Task<SalaryTemplatePreviewDto> PreviewAsync(PreviewSalaryTemplateRequestDto input)
	{
		ValidatePreviewInput(input);
		if (input.TemplateId.HasValue)
		{
			var template = await templateRepository.GetAsync(input.TemplateId.Value);
			EnsureNotArchived(template);
			var active = FindActive(template, await LoadVersionsAsync(template.Id));
			if (input.ExpectedActiveVersionId != active?.Id || input.ConcurrencyStamp != template.ConcurrencyStamp)
				throw new BusinessException("HR:Payroll:SalaryTemplateSaveConflict");
		}
		else if (input.ExpectedActiveVersionId.HasValue || input.ConcurrencyStamp != null)
			throw new BusinessException("HR:Payroll:SalaryTemplateSaveConflict");
		var result = await CalculatePreviewAsync(input);
		EnsureSampleCanActivate(result);
		return previewProofService.Issue(PreviewBinding(input), result);
	}

	private async Task ValidateSaveAsync(SaveSalaryTemplateDto input, Guid? templateId = null)
	{
		var preview = ToPreview(input, templateId);
		ValidatePreviewInput(preview);
		SalaryTemplateCalculator.ValidateRules(MapRules(preview.Rules));
		previewProofService.Validate(input.PreviewToken, PreviewBinding(preview));
		var result = await CalculatePreviewAsync(preview);
		EnsureSampleCanActivate(result);
		previewProofService.Validate(input.PreviewToken, PreviewBinding(preview), result);
	}

	private static void EnsureSampleCanActivate(SalaryTemplateCalculationDto result)
	{
		if (result.EmployeeStateInsurance.IsConfigured && !result.EmployeeStateInsurance.CanCalculate)
			throw new BusinessException("HR:Payroll:SalaryTemplateSampleBlocked")
				.WithData("issues", result.EmployeeStateInsurance.IssueCodes)
				.WithData("message", "Resolve the statutory blockers before calculating this sample.");
	}

	private Task<SalaryTemplateCalculationDto> CalculatePreviewAsync(PreviewSalaryTemplateRequestDto input) =>
		calculationService.CalculateAsync(MapRules(input.Rules), new CalculateSalaryTemplateRequestDto
		{
			SalaryBasis = input.SalaryBasis, ProtectionPolicy = input.ProtectionPolicy,
			MonthlyAmount = GrossEarningAllocation.Money(input.MonthlyAmount), EffectiveMonth = input.EffectiveMonth.Date,
			PfApplicable = input.PfApplicable, PfRuleId = input.PfRuleId
		});

	private static void ValidatePreviewInput(PreviewSalaryTemplateRequestDto input)
	{
		var validation = new PreviewSalaryTemplateRequestDtoValidator().Validate(input);
		if (!validation.IsValid)
			throw new BusinessException("HR:Payroll:SalaryTemplateSampleInvalid")
				.WithData("message", string.Join(" ", validation.Errors.Select(x => x.ErrorMessage)));
	}

	private static PreviewSalaryTemplateRequestDto ToPreview(SaveSalaryTemplateDto input, Guid? templateId = null) => new()
	{
		TemplateId = templateId, ExpectedActiveVersionId = input.ExpectedActiveVersionId, ConcurrencyStamp = input.ConcurrencyStamp,
		Rules = input.Rules, SalaryBasis = input.DefaultSalaryBasis, ProtectionPolicy = input.DefaultProtectionPolicy,
		MonthlyAmount = input.SampleMonthlyAmount, EffectiveMonth = input.SampleEffectiveMonth,
		PfApplicable = input.DefaultPfApplicable, PfRuleId = input.DefaultPfRuleId
	};

	private static object PreviewBinding(PreviewSalaryTemplateRequestDto input) => new
	{
		input.TemplateId, input.ExpectedActiveVersionId, input.ConcurrencyStamp,
		Rules = MapRules(input.Rules).OrderBy(x => x.DisplayOrder).Select(x => x with
		{
			Value = x.FormulaType == SalaryTemplateFormulaType.FixedAmount && x.Value.HasValue
				? GrossEarningAllocation.Money(x.Value.Value) : x.Value
		}).ToArray(),
		SalaryBasis = ParseSalaryBasis(input.SalaryBasis), ProtectionPolicy = ParseProtectionPolicy(input.ProtectionPolicy),
		MonthlyAmount = GrossEarningAllocation.Money(input.MonthlyAmount), EffectiveMonth = input.EffectiveMonth.ToString("yyyy-MM"),
		input.PfApplicable, input.PfRuleId
	};

	private static List<SalaryTemplateRule> ApplyOverrides(
		IReadOnlyList<SalaryTemplateRule> source,
		IReadOnlyList<SalaryRuleOverrideDto> overrides)
	{
		if (overrides.Count == 0)
		{
			return source.ToList();
		}
		if (overrides.Select(x => x.ComponentId).Distinct().Count() != overrides.Count)
		{
			throw new BusinessException("HR:Payroll:CompensationOverrideDuplicate");
		}
		var overrideByComponent = overrides.ToDictionary(x => x.ComponentId);
		foreach (var componentId in overrideByComponent.Keys)
		{
			var original = source.SingleOrDefault(x => x.ComponentId == componentId)
				?? throw new BusinessException("HR:Payroll:CompensationOverrideComponentInvalid").WithData("componentId", componentId);
			if (original.SemanticType is not (PayrollComponentSemanticType.Basic or PayrollComponentSemanticType.DearnessAllowance or PayrollComponentSemanticType.Ordinary))
			{
				throw new BusinessException("HR:Payroll:CompensationOverrideLocked").WithData("componentId", componentId);
			}
		}
		var codesByComponent = source.ToDictionary(x => x.ComponentId, x => x.Code);
		return source.Select(rule =>
		{
			if (!overrideByComponent.TryGetValue(rule.ComponentId, out var value))
			{
				return rule;
			}
			var formula = ParseEnum<SalaryTemplateFormulaType>(value.FormulaType, nameof(value.FormulaType));
			if (formula is SalaryTemplateFormulaType.RemainderOfGross or SalaryTemplateFormulaType.EmployeeProvidentFund or SalaryTemplateFormulaType.ProfessionalTax)
			{
				throw new BusinessException("HR:Payroll:CompensationOverrideFormulaLocked").WithData("componentId", rule.ComponentId);
			}
			return rule with
			{
				FormulaType = formula,
				Value = value.Value,
				BasisCode = value.BasisComponentId.HasValue
					? codesByComponent.GetValueOrDefault(value.BasisComponentId.Value)
					: null
			};
		}).ToList();
	}

	private async Task EnsureNameAvailableAsync(string name, Guid? excludedId = null)
	{
		var normalized = name.Trim().ToLower();
		if (await asyncExecuter.AnyAsync((await templateRepository.GetQueryableAsync()).Where(x =>
			x.Id != excludedId && x.Name.ToLower() == normalized)))
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateNameExists").WithData("name", name);
		}
	}

	private async Task<SalaryStructureTemplateVersion?> FindIdempotentSaveAsync(string key, Guid? templateId = null) =>
		await asyncExecuter.FirstOrDefaultAsync((await versionRepository.GetQueryableAsync()).Where(x =>
			x.SaveOperationKey == key && (!templateId.HasValue || x.TemplateId == templateId.Value)));

	private async Task<IReadOnlyList<SalaryStructureTemplateVersion>> LoadVersionsAsync(Guid templateId) =>
		await asyncExecuter.ToListAsync((await versionRepository.GetQueryableAsync()).Where(x => x.TemplateId == templateId));

	private static SalaryStructureTemplateVersion? FindActive(
		SalaryStructureTemplate template,
		IReadOnlyList<SalaryStructureTemplateVersion> versions) =>
		template.PublishedVersion.HasValue
			? versions.SingleOrDefault(x => x.VersionNumber == template.PublishedVersion.Value && x.Status == SalaryTemplateStatus.Published)
			: null;

	private static SalaryTemplateDto Map(
		SalaryStructureTemplate template,
		IReadOnlyList<SalaryStructureTemplateVersion> versions)
	{
		var active = FindActive(template, versions);
		var editable = active ?? versions.Single(x => x.VersionNumber == template.LatestVersion);
		var requiresSetup = active == null || RequiresSetup(active);
		return new SalaryTemplateDto
		{
			Id = template.Id,
			Name = template.Name,
			Description = template.Description,
			Status = template.Status == SalaryTemplateStatus.Archived ? "ARCHIVED" : requiresSetup ? "NEEDS_SETUP" : "ACTIVE",
			IsDefault = template.IsDefault,
			ActiveVersion = active?.VersionNumber,
			VersionCount = versions.Count,
			UpdatedAt = template.LastModificationTime ?? template.CreationTime,
			ConcurrencyStamp = template.ConcurrencyStamp,
			EditableVersion = MapVersion(editable, template.PublishedVersion)
		};
	}

	private static SalaryTemplateVersionDto MapVersion(SalaryStructureTemplateVersion version, int? activeVersion) => new()
	{
		Id = version.Id,
		VersionNumber = version.VersionNumber,
		Status = version.Status == SalaryTemplateStatus.Draft ? "RECOVERED" : version.VersionNumber == activeVersion ? "ACTIVE" : "REPLACED",
		ActivatedAt = version.PublishedAt,
		DefaultSalaryBasis = ToContractEnum(version.DefaultSalaryBasis.ToString()),
		DefaultProtectionPolicy = ToContractEnum(version.DefaultProtectionPolicy.ToString()),
		DefaultPfApplicable = version.DefaultPfApplicable,
		DefaultPfRuleId = version.DefaultPfRuleId,
		Rules = MapRulesForContract(version.GetRules())
	};

	private static bool RequiresSetup(SalaryStructureTemplateVersion? version) =>
		version != null &&
		version.GetRules().Any(x => x.FormulaType == SalaryTemplateFormulaType.EmployeeProvidentFund) &&
		!version.DefaultPfRuleId.HasValue;

	private static List<SalaryTemplateRule> MapRules(IEnumerable<SalaryTemplateRuleDto> rules)
	{
		var inputs = rules.ToList();
		var codesByComponentId = inputs.Where(x => x.ComponentId != Guid.Empty).GroupBy(x => x.ComponentId)
			.ToDictionary(x => x.Key, x => BuildRuleCode(x.Key));
		return inputs.Select(x => new SalaryTemplateRule(
			codesByComponentId.GetValueOrDefault(x.ComponentId, BuildRuleCode(x.ComponentId)),
			x.ComponentId,
			ParseEnum<SalaryTemplateLineKind>(x.LineKind, nameof(x.LineKind)),
			ParseEnum<SalaryTemplateFormulaType>(x.FormulaType, nameof(x.FormulaType)),
			ParseEnum<PayrollComponentSemanticType>(x.SemanticType, nameof(x.SemanticType)),
			x.Value,
			x.BasisComponentId.HasValue ? codesByComponentId.GetValueOrDefault(x.BasisComponentId.Value) : null,
			x.DisplayOrder)).Select(rule => rule.FormulaType == SalaryTemplateFormulaType.FixedAmount && rule.Value.HasValue
			? rule with { Value = GrossEarningAllocation.Money(rule.Value.Value) } : rule).ToList();
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
			BasisComponentId = string.IsNullOrWhiteSpace(rule.BasisCode) ? null : componentIdsByCode.GetValueOrDefault(rule.BasisCode),
			DisplayOrder = rule.DisplayOrder
		}).ToList();
	}

	private static string BuildRuleCode(Guid componentId) => $"COMPONENT_{componentId:N}".ToUpperInvariant();
	private static SalaryInputBasis ParseSalaryBasis(string value) => ParseEnum<SalaryInputBasis>(value, nameof(value));
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

	private static void EnsureNotArchived(SalaryStructureTemplate template)
	{
		if (template.Status == SalaryTemplateStatus.Archived)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateArchived");
		}
	}
}
