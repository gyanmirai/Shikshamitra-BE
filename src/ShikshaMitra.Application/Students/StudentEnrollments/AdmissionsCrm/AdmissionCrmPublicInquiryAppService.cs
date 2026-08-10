using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[AllowAnonymous]
[RemoteService(false)]
[ExposeServices(typeof(IAdmissionCrmPublicInquiryService), IncludeSelf = true)]
public class AdmissionCrmPublicInquiryAppService(
	AdmissionCrmPublicPortalWorkflow portalWorkflow,
	AdmissionCrmPublicFormWorkflow formWorkflow,
	AdmissionCrmPublicInquiryWorkflow inquiryWorkflow) : ApplicationService, IAdmissionCrmPublicInquiryService
{
	public Task<PublicAdmissionPortalDto> GetPublicPortalAsync(Guid tenantId) => portalWorkflow.GetAsync(tenantId);
	public Task<PublicAdmissionInquiryFormDto> GetPublicFormAsync(Guid tenantId, Guid templateId, string? language = null) =>
		formWorkflow.GetAsync(tenantId, templateId, language);
	public Task<PublicAdmissionInquirySubmitResultDto> SubmitPublicFormAsync(
		Guid tenantId, Guid templateId, PublicAdmissionInquirySubmitDto input, string? ipAddress, string? userAgent) =>
		inquiryWorkflow.SubmitAsync(tenantId, templateId, input, ipAddress, userAgent);
}
