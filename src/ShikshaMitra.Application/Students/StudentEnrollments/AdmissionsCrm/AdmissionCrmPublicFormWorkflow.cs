using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicFormWorkflow(
	IRepository<AdmissionInquiryFormTemplate, Guid> templateRepository,
	AdmissionCrmPublicCaptureConfiguration captureConfiguration,
	ITenantRepository tenantRepository) : ApplicationService, ITransientDependency
{
	public async Task<PublicAdmissionInquiryFormDto> GetAsync(
		Guid tenantId,
		Guid templateId,
		string? language = null)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var template = await GetActiveTemplateAsync(templateId);
			await captureConfiguration.EnsurePublicFormChannelEnabledAsync(template);
			return AdmissionCrmPublicFormMapper.ToDto(
				tenantId, template, language, await GetSupportedLanguagesAsync(tenantId));
		}
	}

	public async Task<AdmissionInquiryFormTemplate> GetActiveTemplateAsync(Guid templateId)
	{
		var template = await templateRepository.GetAsync(templateId);
		if (template.TenantId != CurrentTenant.Id || !template.IsPublic || !template.IsActive)
		{
			throw new BusinessException("AdmissionCrm:PublicFormNotFound").WithData("TemplateId", templateId);
		}

		return template;
	}

	public async Task<IReadOnlyList<string>> GetSupportedLanguagesAsync(Guid tenantId)
	{
		using (CurrentTenant.Change(null))
		{
			var tenant = await tenantRepository.FindAsync(tenantId);
			return tenant == null
				? ["en", "gu", "hi"]
				: AdmissionCrmPublicBranding.ParseSupportedLanguages(
					tenant.GetProperty<string>("AdmissionsSupportedLanguages"));
		}
	}
}
