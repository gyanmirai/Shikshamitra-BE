using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationAccess(
	IRepository<AdmissionCrmRegistrationTemplate, Guid> templateRepository,
	IRepository<AdmissionCrmFormNumberSeries, Guid> seriesRepository,
	IRepository<AdmissionCrmRegistrationApplication, Guid> applicationRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	public async Task EnsureYearAsync(Guid academicYearId, string operation) =>
		await scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, operation);

	public async Task<AdmissionCrmRegistrationTemplate> GetTemplateAsync(Guid id, string operation)
	{
		var template = await templateRepository.GetAsync(id);
		if (template.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:RegistrationTemplateNotFound");
		await EnsureYearAsync(template.AcademicYearId, operation);
		return template;
	}

	public async Task<AdmissionCrmFormNumberSeries> GetSeriesAsync(Guid id, string operation)
	{
		var series = await seriesRepository.GetAsync(id);
		if (series.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:FormNumberSeriesNotFound");
		await EnsureYearAsync(series.AcademicYearId, operation);
		return series;
	}

	public async Task<AdmissionInquiryLead> GetLeadAsync(Guid id, string operation)
	{
		var lead = await leadRepository.GetAsync(id);
		if (lead.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:LeadNotFound");
		await scopeAccess.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return lead;
	}

	public async Task<AdmissionCrmRegistrationApplication> GetApplicationAsync(Guid id, string operation)
	{
		var application = await applicationRepository.GetAsync(id);
		if (application.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:RegistrationApplicationNotFound");
		var lead = await leadRepository.GetAsync(application.LeadId);
		await scopeAccess.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return application;
	}
}
