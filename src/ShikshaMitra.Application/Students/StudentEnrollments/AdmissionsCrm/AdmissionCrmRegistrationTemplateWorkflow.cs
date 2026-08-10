using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationTemplateWorkflow(
	IRepository<AdmissionCrmRegistrationTemplate, Guid> repository,
	StudentScopeAccessService scopeAccess,
	AdmissionCrmRegistrationAccess access) : ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AdmissionCrmRegistrationTemplateDto>> GetListAsync(Guid yearId, GetAdmissionCrmRegistrationTemplateListInput input)
	{
		await access.EnsureYearAsync(yearId, "GetTemplatesAsync");
		var queryable = await repository.GetQueryableAsync();
		var templates = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId &&
			(!input.Status.HasValue || x.Status == input.Status.Value) && (string.IsNullOrWhiteSpace(input.Filter) || x.Name.Contains(input.Filter))));
		var ordered = templates.OrderBy(x => x.Name).ThenByDescending(x => x.Version).ToList();
		return new PagedResultDto<AdmissionCrmRegistrationTemplateDto>(ordered.Count,
			ordered.Skip(input.SkipCount).Take(input.MaxResultCount).Select(AdmissionCrmRegistrationModelMapper.ToTemplateDto).ToList());
	}

	public async Task<AdmissionCrmRegistrationTemplateDto> GetAsync(Guid id) =>
		AdmissionCrmRegistrationModelMapper.ToTemplateDto(await access.GetTemplateAsync(id, "GetTemplateAsync"));

	public async Task<AdmissionCrmRegistrationTemplateDto> CreateAsync(Guid yearId, CreateAdmissionCrmRegistrationTemplateDto input)
	{
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(yearId, "CreateTemplateAsync");
		AdmissionCrmRegistrationTemplateValidator.NormalizeAndValidate(input);
		var queryable = await repository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && x.Name == input.Name.Trim()));
		var template = CreateEntity(yearId, existing.Count == 0 ? 1 : existing.Max(x => x.Version) + 1, input);
		await repository.InsertAsync(template, autoSave: true);
		return AdmissionCrmRegistrationModelMapper.ToTemplateDto(template);
	}

	public async Task<AdmissionCrmRegistrationTemplateDto> UpdateAsync(Guid id, UpdateAdmissionCrmRegistrationTemplateDto input)
	{
		var template = await access.GetTemplateAsync(id, "UpdateTemplateAsync");
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(template.AcademicYearId, "UpdateTemplateAsync");
		AdmissionCrmRegistrationTemplateValidator.NormalizeAndValidate(input);
		template.Update(input.Name, input.IsFallback, AdmissionCrmRegistrationModelMapper.Serialize(input.Applicability),
			AdmissionCrmRegistrationModelMapper.Serialize(input.Sections), input.LinkExpiryDays, input.AllowPartialSave,
			input.FeeRequired, input.FeeAmount, input.AutoIssueAtStage, input.Notes);
		await repository.UpdateAsync(template, autoSave: true);
		return AdmissionCrmRegistrationModelMapper.ToTemplateDto(template);
	}

	public async Task<AdmissionCrmRegistrationTemplateDto> PublishAsync(Guid id)
	{
		var template = await access.GetTemplateAsync(id, "PublishTemplateAsync");
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(template.AcademicYearId, "PublishTemplateAsync");
		AdmissionCrmRegistrationTemplateValidator.NormalizeAndValidate(AdmissionCrmRegistrationModelMapper.ToTemplateUpsert(template));
		await EnsureUnambiguousAsync(template);
		template.Publish(Clock.Now);
		await repository.UpdateAsync(template, autoSave: true);
		return AdmissionCrmRegistrationModelMapper.ToTemplateDto(template);
	}

	public async Task<AdmissionCrmRegistrationTemplateDto> CreateVersionAsync(Guid id)
	{
		var source = await access.GetTemplateAsync(id, "CreateTemplateVersionAsync");
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(source.AcademicYearId, "CreateTemplateVersionAsync");
		var queryable = await repository.GetQueryableAsync();
		var versions = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == source.AcademicYearId && x.Name == source.Name));
		var draft = CreateEntity(source.AcademicYearId, versions.Max(x => x.Version) + 1, AdmissionCrmRegistrationModelMapper.ToTemplateUpsert(source));
		await repository.InsertAsync(draft, autoSave: true);
		return AdmissionCrmRegistrationModelMapper.ToTemplateDto(draft);
	}

	public async Task<AdmissionCrmRegistrationTemplateDto> RetireAsync(Guid id)
	{
		var template = await access.GetTemplateAsync(id, "RetireTemplateAsync");
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(template.AcademicYearId, "RetireTemplateAsync");
		template.Retire(Clock.Now);
		await repository.UpdateAsync(template, autoSave: true);
		return AdmissionCrmRegistrationModelMapper.ToTemplateDto(template);
	}

	private AdmissionCrmRegistrationTemplate CreateEntity(Guid yearId, int version, AdmissionCrmRegistrationTemplateUpsertDto input) =>
		new(GuidGenerator.Create(), CurrentTenant.Id, yearId, input.Name, version, input.IsFallback,
			AdmissionCrmRegistrationModelMapper.Serialize(input.Applicability), AdmissionCrmRegistrationModelMapper.Serialize(input.Sections),
			input.LinkExpiryDays, input.AllowPartialSave, input.FeeRequired, input.FeeAmount, input.AutoIssueAtStage, input.Notes);

	private async Task EnsureUnambiguousAsync(AdmissionCrmRegistrationTemplate candidate)
	{
		var queryable = await repository.GetQueryableAsync();
		var published = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == candidate.AcademicYearId && x.Status == AdmissionCrmRegistrationTemplateStatus.Published && x.Id != candidate.Id));
		var scope = AdmissionCrmRegistrationModelMapper.Deserialize<AdmissionCrmPlaybookApplicabilityDto>(candidate.ApplicabilityJson);
		if (published.Any(x => AdmissionCrmRegistrationModelMapper.SameApplicability(scope,
			AdmissionCrmRegistrationModelMapper.Deserialize<AdmissionCrmPlaybookApplicabilityDto>(x.ApplicabilityJson))))
			throw new Volo.Abp.BusinessException("AdmissionCrm:AmbiguousPublishedRegistrationTemplate");
	}
}
