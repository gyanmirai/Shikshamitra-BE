using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDocumentTemplateWorkflow(
	IRepository<AdmissionCrmDocumentChecklistTemplate, Guid> templateRepository,
	IRepository<AdmissionCrmDocumentType, Guid> documentTypeRepository,
	StudentScopeAccessService studentScopeAccessService)
	: ApplicationService, ITransientDependency
{
	private const int MaxRejectionReasonCount = 8;
	private const int MaxRejectionReasonLength = 160;

	public async Task<PagedResultDto<AdmissionCrmDocumentChecklistTemplateDto>> GetTemplatesAsync(
		Guid academicYearId,
		GetAdmissionCrmDocumentChecklistTemplateListInput input)
	{
		await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetTemplatesAsync));
		var queryable = await templateRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId &&
			(!input.Status.HasValue || x.Status == input.Status.Value) &&
			(string.IsNullOrWhiteSpace(input.Filter) || x.Name.Contains(input.Filter))));
		var ordered = items.OrderBy(x => x.Name).ThenByDescending(x => x.Version).ToList();
		return new PagedResultDto<AdmissionCrmDocumentChecklistTemplateDto>(
			ordered.Count,
			ordered.Skip(input.SkipCount).Take(input.MaxResultCount).Select(AdmissionCrmDocumentModelMapper.ToTemplateDto).ToList());
	}

	public async Task<AdmissionCrmDocumentChecklistTemplateDto> CreateTemplateAsync(
		Guid academicYearId,
		AdmissionCrmDocumentChecklistTemplateUpsertDto input)
	{
		await studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(academicYearId, nameof(CreateTemplateAsync));
		await NormalizeAndValidateAsync(input);
		var queryable = await templateRepository.GetQueryableAsync();
		var versions = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId && x.Name == input.Name));
		var template = CreateEntity(academicYearId, versions.Count == 0 ? 1 : versions.Max(x => x.Version) + 1, input);
		await templateRepository.InsertAsync(template, autoSave: true);
		return AdmissionCrmDocumentModelMapper.ToTemplateDto(template);
	}

	public async Task<AdmissionCrmDocumentChecklistTemplateDto> UpdateTemplateAsync(
		Guid id,
		AdmissionCrmDocumentChecklistTemplateUpsertDto input)
	{
		var template = await GetAsync(id, nameof(UpdateTemplateAsync));
		await studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(template.AcademicYearId, nameof(UpdateTemplateAsync));
		await NormalizeAndValidateAsync(input);
		template.Update(input.Name, input.IsFallback, AdmissionCrmDocumentModelMapper.Serialize(input.Applicability),
			AdmissionCrmDocumentModelMapper.Serialize(input.Requirements), input.ReadinessApprovalCount, input.Notes);
		await templateRepository.UpdateAsync(template, autoSave: true);
		return AdmissionCrmDocumentModelMapper.ToTemplateDto(template);
	}

	public async Task<AdmissionCrmDocumentChecklistTemplateDto> PublishTemplateAsync(Guid id)
	{
		var template = await GetAsync(id, nameof(PublishTemplateAsync));
		await studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(template.AcademicYearId, nameof(PublishTemplateAsync));
		await NormalizeAndValidateAsync(AdmissionCrmDocumentModelMapper.ToTemplateUpsert(template));
		await EnsureNoPublishedConflictAsync(template);
		template.Publish(Clock.Now);
		await templateRepository.UpdateAsync(template, autoSave: true);
		return AdmissionCrmDocumentModelMapper.ToTemplateDto(template);
	}

	public async Task<AdmissionCrmDocumentChecklistTemplateDto> CreateTemplateVersionAsync(Guid id)
	{
		var source = await GetAsync(id, nameof(CreateTemplateVersionAsync));
		await studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(source.AcademicYearId, nameof(CreateTemplateVersionAsync));
		var queryable = await templateRepository.GetQueryableAsync();
		var versions = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == source.AcademicYearId && x.Name == source.Name));
		var draft = CreateEntity(source.AcademicYearId, versions.Max(x => x.Version) + 1, AdmissionCrmDocumentModelMapper.ToTemplateUpsert(source));
		await templateRepository.InsertAsync(draft, autoSave: true);
		return AdmissionCrmDocumentModelMapper.ToTemplateDto(draft);
	}

	public async Task<AdmissionCrmDocumentChecklistTemplateDto> RetireTemplateAsync(Guid id)
	{
		var template = await GetAsync(id, nameof(RetireTemplateAsync));
		await studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(template.AcademicYearId, nameof(RetireTemplateAsync));
		template.Retire(Clock.Now);
		await templateRepository.UpdateAsync(template, autoSave: true);
		return AdmissionCrmDocumentModelMapper.ToTemplateDto(template);
	}

	private async Task NormalizeAndValidateAsync(AdmissionCrmDocumentChecklistTemplateUpsertDto input)
	{
		input.Name = Check.NotNullOrWhiteSpace(input.Name, nameof(input.Name), AdmissionCrmConsts.MaxTemplateNameLength).Trim();
		if (input.IsFallback && AdmissionCrmDocumentModelMapper.HasApplicability(input.Applicability))
			throw new BusinessException("AdmissionCrm:FallbackDocumentChecklistCannotHaveApplicability");
		if (input.Requirements.Count == 0) throw new BusinessException("AdmissionCrm:DocumentChecklistRequiresItem");
		var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var requirement in input.Requirements)
		{
			requirement.Key = Check.NotNullOrWhiteSpace(requirement.Key, nameof(requirement.Key), 80).Trim();
			requirement.Name = Check.NotNullOrWhiteSpace(requirement.Name, nameof(requirement.Name), 120).Trim();
			if (!keys.Add(requirement.Key)) throw new BusinessException("AdmissionCrm:DuplicateDocumentRequirementKey");
			if (requirement.RequirementLevel == AdmissionCrmDocumentRequirementLevel.Conditional && string.IsNullOrWhiteSpace(requirement.ConditionalFieldKey))
				throw new BusinessException("AdmissionCrm:ConditionalDocumentRequirementNeedsField");
			if (requirement.ExpiryReminderDaysBefore.HasValue && !requirement.ValidityDateRequired)
				throw new BusinessException("AdmissionCrm:DocumentExpiryReminderRequiresValidityDate").WithData("RequirementKey", requirement.Key);
			var extensions = requirement.AcceptedExtensions.Select(x => x.Trim().TrimStart('.').ToLowerInvariant()).Where(x => x.Length > 0).Distinct().ToList();
			if (extensions.Count == 0 || extensions.Any(x => x.Any(character => !char.IsLetterOrDigit(character)) || !FileValidationService.IsSupportedDocumentExtension(x)))
				throw new BusinessException("AdmissionCrm:InvalidDocumentExtensions");
			requirement.AcceptedExtensions = extensions;
			requirement.UploadInstructions = string.IsNullOrWhiteSpace(requirement.UploadInstructions) ? null : requirement.UploadInstructions.Trim();
			var rejectionReasons = (requirement.RejectionReasons ?? []).Select(x => x.Trim()).Where(x => x.Length > 0)
				.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			if (rejectionReasons.Count > MaxRejectionReasonCount || rejectionReasons.Any(x => x.Length > MaxRejectionReasonLength))
				throw new BusinessException("AdmissionCrm:InvalidDocumentRejectionReasons")
					.WithData("RequirementKey", requirement.Key).WithData("MaximumCount", MaxRejectionReasonCount).WithData("MaximumLength", MaxRejectionReasonLength);
			requirement.RejectionReasons = rejectionReasons;
			if (!requirement.DocumentTypeId.HasValue) continue;
			var type = await documentTypeRepository.GetAsync(requirement.DocumentTypeId.Value);
			if (type.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:DocumentTypeNotFound");
		}
	}

	private async Task<AdmissionCrmDocumentChecklistTemplate> GetAsync(Guid id, string operation)
	{
		var template = await templateRepository.GetAsync(id);
		if (template.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:DocumentChecklistTemplateNotFound");
		await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(template.AcademicYearId, operation);
		return template;
	}

	private async Task EnsureNoPublishedConflictAsync(AdmissionCrmDocumentChecklistTemplate candidate)
	{
		var queryable = await templateRepository.GetQueryableAsync();
		var published = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == candidate.AcademicYearId && x.Status == AdmissionCrmDocumentChecklistTemplateStatus.Published && x.Id != candidate.Id));
		if (published.Any(x => x.ApplicabilityJson == candidate.ApplicabilityJson))
			throw new BusinessException("AdmissionCrm:AmbiguousPublishedDocumentChecklist");
	}

	private AdmissionCrmDocumentChecklistTemplate CreateEntity(Guid yearId, int version, AdmissionCrmDocumentChecklistTemplateUpsertDto input) =>
		new(GuidGenerator.Create(), CurrentTenant.Id, yearId, input.Name, version, input.IsFallback,
			AdmissionCrmDocumentModelMapper.Serialize(input.Applicability), AdmissionCrmDocumentModelMapper.Serialize(input.Requirements),
			input.ReadinessApprovalCount, input.Notes);
}
