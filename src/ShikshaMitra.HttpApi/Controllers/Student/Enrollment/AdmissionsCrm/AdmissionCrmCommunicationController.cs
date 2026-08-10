using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmCommunicationController(
	IAdmissionCrmCommunicationProviderService providerService,
	IAdmissionCrmCommunicationOptOutService optOutService,
	IAdmissionCrmMessageDeliveryService deliveryService) : AbpControllerBase
{
	[HttpGet("{academicYearId}/communication/providers")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<PagedResultDto<AdmissionCrmCommunicationProviderDto>> GetProvidersAsync(
		[FromRoute] Guid academicYearId)
	{
		return await providerService.GetProvidersAsync(academicYearId);
	}

	[HttpPut("{academicYearId}/communication/providers/{channel}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmCommunicationProviderDto> UpdateProviderAsync(
		[FromRoute] Guid academicYearId,
		[FromRoute] AdmissionCrmMessageChannel channel,
		[FromBody] UpdateAdmissionCrmCommunicationProviderDto input)
	{
		return await providerService.UpdateProviderAsync(academicYearId, channel, input);
	}

	[HttpGet("{academicYearId}/communication/opt-outs")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<PagedResultDto<AdmissionCrmCommunicationOptOutDto>> GetOptOutsAsync(
		[FromRoute] Guid academicYearId,
		[FromQuery] GetAdmissionCrmCommunicationOptOutListInput input)
	{
		return await optOutService.GetOptOutsAsync(academicYearId, input);
	}

	[HttpPost("{academicYearId}/communication/opt-outs")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmCommunicationOptOutDto> CreateOptOutAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] CreateAdmissionCrmCommunicationOptOutDto input)
	{
		return await optOutService.CreateOptOutAsync(academicYearId, input);
	}

	[HttpPost("{academicYearId}/communication/opt-outs/clear")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmCommunicationOptOutDto> ClearOptOutAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] ClearAdmissionCrmCommunicationOptOutDto input)
	{
		return await optOutService.ClearOptOutAsync(academicYearId, input);
	}

	[HttpPost("leads/{leadId}/messages/send-template")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageDeliveryLogDto> SendTemplateAsync(
		[FromRoute] Guid leadId,
		[FromBody] SendAdmissionCrmMessageTemplateDto input)
	{
		return await deliveryService.SendTemplateAsync(leadId, input);
	}

	[HttpPost("{academicYearId}/communication/message-templates/preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageTemplatePreviewDto> PreviewTemplateAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] PreviewAdmissionCrmMessageTemplateDto input)
	{
		return await deliveryService.PreviewTemplateAsync(academicYearId, input);
	}

	[HttpGet("{academicYearId}/communication/delivery-logs")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<PagedResultDto<AdmissionCrmMessageDeliveryLogDto>> GetDeliveryLogsAsync(
		[FromRoute] Guid academicYearId,
		[FromQuery] GetAdmissionCrmMessageDeliveryLogListInput input)
	{
		return await deliveryService.GetDeliveryLogsAsync(academicYearId, input);
	}

	[HttpPost("communication/delivery-logs/{deliveryLogId}/retry")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageDeliveryLogDto> RetryDeliveryAsync(
		[FromRoute] Guid deliveryLogId,
		[FromBody] RetryAdmissionCrmMessageDeliveryDto input)
	{
		return await deliveryService.RetryDeliveryAsync(deliveryLogId, input);
	}
}
