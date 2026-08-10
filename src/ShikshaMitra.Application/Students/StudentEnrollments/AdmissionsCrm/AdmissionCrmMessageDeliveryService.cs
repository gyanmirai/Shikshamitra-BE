using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmMessageDeliveryService(AdmissionCrmMessageDeliveryWorkflow workflow)
	: ApplicationService, IAdmissionCrmMessageDeliveryService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public Task<AdmissionCrmMessageDeliveryLogDto> SendTemplateAsync(Guid leadId, SendAdmissionCrmMessageTemplateDto input) => workflow.SendAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public Task<AdmissionCrmMessageTemplatePreviewDto> PreviewTemplateAsync(Guid academicYearId, PreviewAdmissionCrmMessageTemplateDto input) => workflow.PreviewAsync(academicYearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public Task<PagedResultDto<AdmissionCrmMessageDeliveryLogDto>> GetDeliveryLogsAsync(Guid academicYearId, GetAdmissionCrmMessageDeliveryLogListInput input) => workflow.GetLogsAsync(academicYearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public Task<AdmissionCrmMessageDeliveryLogDto> RetryDeliveryAsync(Guid deliveryLogId, RetryAdmissionCrmMessageDeliveryDto input) => workflow.RetryAsync(deliveryLogId, input);
}
