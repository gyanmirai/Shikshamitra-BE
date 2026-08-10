using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPolicyAdoptionWorkflow(
	IRepository<AdmissionCrmPolicyPack, Guid> packRepository,
	IRepository<AdmissionCrmPolicyAdoption, Guid> adoptionRepository,
	StudentScopeAccessService studentScopeAccessService) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmPolicyAdoptionDto> AdoptAsync(
		Guid academicYearId,
		Guid policyPackId,
		AdoptAdmissionCrmPolicyPackDto input)
	{
		await studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(academicYearId, nameof(AdoptAsync));
		var pack = await GetPackAsync(policyPackId);
		if (pack.Status != AdmissionCrmPolicyPackStatus.Published)
			throw new BusinessException("AdmissionCrm:PublishedPolicyPackRequired");
		if (pack.ReviewDueAt < Clock.Now && string.IsNullOrWhiteSpace(input.OverrideReason))
			throw new BusinessException("AdmissionCrm:OverduePolicySourceReviewRequiresReason");
		var rules = AdmissionCrmPolicyMapper.Deserialize<IReadOnlyList<AdmissionCrmPolicyRuleDto>>(pack.RulesJson) ?? [];
		var unknownOverrides = input.Overrides.Where(overrideItem =>
			rules.All(rule => !rule.Code.Equals(overrideItem.RuleCode, StringComparison.OrdinalIgnoreCase))).ToList();
		if (unknownOverrides.Count > 0)
			throw new BusinessException("AdmissionCrm:UnknownPolicyRuleOverride")
				.WithData("RuleCode", unknownOverrides[0].RuleCode);
		var warnings = input.Overrides.Select(overrideItem => string.IsNullOrWhiteSpace(overrideItem.Warning)
			? $"School override differs from sourced rule {overrideItem.RuleCode}."
			: overrideItem.Warning).ToList();
		if (pack.ReviewDueAt < Clock.Now)
			warnings.Add("Official source review is overdue.");
		var adoption = new AdmissionCrmPolicyAdoption(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			pack.Id,
			pack.Version,
			AdmissionCrmPolicyMapper.Serialize(input.Applicability),
			AdmissionCrmPolicyMapper.Serialize(input.Overrides),
			AdmissionCrmPolicyMapper.Serialize(new Dictionary<string, IReadOnlyList<string>> { ["warnings"] = warnings }),
			input.OverrideReason,
			input.EffectiveFrom,
			input.EffectiveUntil,
			GetCurrentUserId(),
			Clock.Now);
		await adoptionRepository.InsertAsync(adoption, autoSave: true);
		return AdmissionCrmPolicyMapper.Map(adoption);
	}

	public async Task<IReadOnlyList<AdmissionCrmPolicyAdoptionDto>> GetListAsync(Guid academicYearId)
	{
		await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetListAsync));
		var queryable = await adoptionRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(entity =>
			entity.TenantId == CurrentTenant.Id && entity.AcademicYearId == academicYearId));
		return entities.OrderByDescending(entity => entity.AdoptedAt)
			.Select(AdmissionCrmPolicyMapper.Map)
			.ToList();
	}

	private async Task<AdmissionCrmPolicyPack> GetPackAsync(Guid id)
	{
		var pack = await packRepository.GetAsync(id);
		if (pack.TenantId != CurrentTenant.Id)
			throw new AbpAuthorizationException("Policy pack belongs to another tenant.");
		return pack;
	}

	private Guid GetCurrentUserId() =>
		CurrentUser.Id ?? throw new AbpAuthorizationException("An authenticated user is required.");
}
