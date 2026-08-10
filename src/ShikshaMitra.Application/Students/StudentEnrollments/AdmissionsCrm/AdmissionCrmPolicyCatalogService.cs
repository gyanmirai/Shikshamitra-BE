using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPolicyCatalogWorkflow(
	IRepository<AdmissionCrmPolicyPack, Guid> packRepository)
	: ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePolicyPacks)]
	public async Task<AdmissionCrmPolicyPackDto> CreatePolicyPackAsync(CreateAdmissionCrmPolicyPackDto input)
	{
		AdmissionCrmOfficialPolicyCatalog.Validate(input);
		var queryable = await packRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(queryable.Where(entity =>
			entity.TenantId == CurrentTenant.Id && entity.Code == input.Code.ToUpper()));
		var version = existing.Count == 0 ? 1 : existing.Max(entity => entity.Version) + 1;
		var pack = CreatePack(input, version);
		await packRepository.InsertAsync(pack, autoSave: true);
		return AdmissionCrmPolicyMapper.Map(pack, Clock);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewPolicyPacks)]
	public async Task<PagedResultDto<AdmissionCrmPolicyPackDto>> GetPolicyPacksAsync(
		GetAdmissionCrmPolicyPackListInput input)
	{
		var queryable = await packRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(entity => entity.TenantId == CurrentTenant.Id));
		var filtered = entities
			.Where(entity => !input.Status.HasValue || entity.Status == input.Status)
			.Where(entity => string.IsNullOrWhiteSpace(input.Board) || entity.Board.Equals(input.Board.Trim(), StringComparison.OrdinalIgnoreCase))
			.Where(entity => string.IsNullOrWhiteSpace(input.Jurisdiction) || entity.Jurisdiction.Equals(input.Jurisdiction.Trim(), StringComparison.OrdinalIgnoreCase))
			.OrderBy(entity => entity.Code)
			.ThenByDescending(entity => entity.Version)
			.ToList();
		return new PagedResultDto<AdmissionCrmPolicyPackDto>(
			filtered.Count,
			filtered.Skip(input.SkipCount).Take(input.MaxResultCount)
				.Select(entity => AdmissionCrmPolicyMapper.Map(entity, Clock)).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePolicyPacks)]
	public async Task<AdmissionCrmPolicyBaselineInstallResultDto> InstallOfficialBaselineDraftsAsync()
	{
		var queryable = await packRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(queryable.Where(entity => entity.TenantId == CurrentTenant.Id));
		var created = new List<AdmissionCrmPolicyPackDto>();
		var current = new List<string>();
		foreach (var baseline in AdmissionCrmOfficialPolicyCatalog.Build())
		{
			var matching = existing.Where(entity => entity.Code == baseline.Code)
				.OrderByDescending(entity => entity.Version).FirstOrDefault();
			if (matching != null && AdmissionCrmOfficialPolicyCatalog.IsCurrent(matching, baseline))
			{
				current.Add(baseline.Code);
				continue;
			}

			AdmissionCrmOfficialPolicyCatalog.Validate(baseline);
			var version = matching == null ? 1 : existing.Where(entity => entity.Code == baseline.Code).Max(entity => entity.Version) + 1;
			var pack = CreatePack(baseline, version);
			await packRepository.InsertAsync(pack, autoSave: true);
			existing.Add(pack);
			created.Add(AdmissionCrmPolicyMapper.Map(pack, Clock));
		}

		return new AdmissionCrmPolicyBaselineInstallResultDto
		{
			CatalogVerifiedAt = AdmissionCrmOfficialPolicyCatalog.VerifiedAt,
			CreatedDrafts = created,
			AlreadyCurrentCodes = current
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePolicyPacks)]
	public async Task<AdmissionCrmPolicyPackDto> SubmitPolicyPackAsync(Guid id)
	{
		var pack = await GetPackAsync(id);
		pack.Submit(GetCurrentUserId(), Clock.Now);
		await packRepository.UpdateAsync(pack, autoSave: true);
		return AdmissionCrmPolicyMapper.Map(pack, Clock);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApprovePolicyPacks)]
	public async Task<AdmissionCrmPolicyPackDto> ApprovePolicyPackAsync(Guid id)
	{
		var pack = await GetPackAsync(id);
		pack.Approve(GetCurrentUserId(), Clock.Now);
		await packRepository.UpdateAsync(pack, autoSave: true);
		return AdmissionCrmPolicyMapper.Map(pack, Clock);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApprovePolicyPacks)]
	public async Task<AdmissionCrmPolicyPackDto> PublishPolicyPackAsync(Guid id)
	{
		var pack = await GetPackAsync(id);
		pack.Publish(Clock.Now);
		var queryable = await packRepository.GetQueryableAsync();
		var prior = await AsyncExecuter.ToListAsync(queryable.Where(entity =>
			entity.TenantId == CurrentTenant.Id && entity.Code == pack.Code && entity.Id != pack.Id &&
			entity.Status == AdmissionCrmPolicyPackStatus.Published));
		foreach (var previous in prior)
		{
			previous.Supersede(pack.Id);
			await packRepository.UpdateAsync(previous);
		}
		await packRepository.UpdateAsync(pack, autoSave: true);
		return AdmissionCrmPolicyMapper.Map(pack, Clock);
	}

	private AdmissionCrmPolicyPack CreatePack(CreateAdmissionCrmPolicyPackDto input, int version) => new(
		GuidGenerator.Create(), CurrentTenant.Id, input.Code, input.Name, input.Jurisdiction, input.Board, version,
		AdmissionCrmPolicyMapper.Serialize(input.Sources), AdmissionCrmPolicyMapper.Serialize(input.Rules),
		input.EffectiveFrom, input.EffectiveUntil, input.ReviewDueAt);

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
