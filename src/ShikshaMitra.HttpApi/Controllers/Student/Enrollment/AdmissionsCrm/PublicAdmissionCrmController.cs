using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/public/student-enrollment/admissions-crm")]
[AllowAnonymous]
public class PublicAdmissionCrmController(
	IAdmissionCrmPublicInquiryService inquiryService,
	IAdmissionCrmPublicAccessService accessService,
	IAdmissionCrmPublicApplicantService applicantService,
	IAdmissionCrmPublicDeliveryCallbackService callbackService) : AbpControllerBase
{
	[HttpGet("{tenantId}/portal")]
	public Task<PublicAdmissionPortalDto> GetPortalAsync([FromRoute] Guid tenantId) =>
		inquiryService.GetPublicPortalAsync(tenantId);

	[HttpGet("{tenantId}/forms/{templateId}")]
	public async Task<PublicAdmissionInquiryFormDto> GetFormAsync(
		[FromRoute] Guid tenantId,
		[FromRoute] Guid templateId,
		[FromQuery] string? language = null)
	{
		return await inquiryService.GetPublicFormAsync(tenantId, templateId, language);
	}

	[HttpPost("{tenantId}/forms/{templateId}/submit")]
	public async Task<PublicAdmissionInquirySubmitResultDto> SubmitFormAsync(
		[FromRoute] Guid tenantId,
		[FromRoute] Guid templateId,
		[FromBody] PublicAdmissionInquirySubmitDto input)
	{
		return await inquiryService.SubmitPublicFormAsync(
			tenantId,
			templateId,
			input,
			HttpContext.Connection.RemoteIpAddress?.ToString(),
			Request.Headers.UserAgent.ToString());
	}

	[HttpPost("{tenantId}/status/{token}/otp")]
	public async Task<AdmissionApplicantStatusOtpChallengeDto> IssueStatusOtpAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token)
	{
		return await accessService.IssueStatusOtpAsync(tenantId, token);
	}

	[HttpPost("{tenantId}/family/otp")]
	public Task<AdmissionApplicantStatusOtpChallengeDto> IssueFamilyAccessOtpAsync(
		[FromRoute] Guid tenantId,
		[FromBody] StartAdmissionFamilyAccessDto input)
	{
		return accessService.IssueFamilyAccessOtpAsync(tenantId, input);
	}

	[HttpPost("{tenantId}/family/verify")]
	public Task<AdmissionApplicantStatusDto> VerifyFamilyAccessOtpAsync(
		[FromRoute] Guid tenantId,
		[FromBody] VerifyAdmissionFamilyAccessDto input)
	{
		return accessService.VerifyFamilyAccessOtpAsync(tenantId, input);
	}

	[HttpPost("{tenantId}/status/{token}/verify")]
	public async Task<AdmissionApplicantStatusDto> VerifyStatusOtpAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromBody] VerifyAdmissionApplicantStatusOtpDto input)
	{
		return await accessService.VerifyStatusOtpAsync(tenantId, token, input);
	}

	[HttpGet("{tenantId}/status/{token}")]
	public Task<AdmissionApplicantStatusDto> GetVerifiedStatusAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromHeader(Name = "X-Admission-Access-Session"), Required] string accessSessionToken,
		[FromQuery] string? language = null)
	{
		return accessService.GetVerifiedStatusAsync(
			tenantId,
			token,
			accessSessionToken,
			language);
	}

	[HttpPut("{tenantId}/status/{token}/communication-preferences")]
	public Task<AdmissionApplicantStatusDto> UpdateCommunicationPreferencesAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromBody] UpdatePublicAdmissionCommunicationPreferencesDto input) =>
		accessService.UpdateCommunicationPreferencesAsync(tenantId, token, input);

	[HttpPost("{tenantId}/status/{token}/withdraw")]
	public Task<AdmissionApplicantStatusDto> WithdrawApplicationAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromBody] WithdrawPublicAdmissionApplicationDto input) =>
		accessService.WithdrawApplicationAsync(tenantId, token, input);

	[HttpPut("{tenantId}/status/{token}/registration")]
	public async Task<AdmissionCrmRegistrationApplicationDto> SaveRegistrationAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromBody] SavePublicAdmissionCrmRegistrationDto input)
	{
		return await applicantService.SaveRegistrationAsync(tenantId, token, input);
	}

	[HttpPost("{tenantId}/status/{token}/registration/submit")]
	public async Task<AdmissionCrmRegistrationApplicationDto> SubmitRegistrationAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromBody] SavePublicAdmissionCrmRegistrationDto input)
	{
		return await applicantService.SubmitRegistrationAsync(tenantId, token, input);
	}

	[HttpPost("{tenantId}/status/{token}/registration/documents")]
	[Consumes("multipart/form-data")]
	public async Task<AdmissionCrmApplicantDocumentChecklistDto> UploadRegistrationDocumentAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromForm] PublicAdmissionCrmDocumentUploadRequest request)
	{
		const long maximumBytes = 25L * 1024 * 1024;
		if (request.File == null || request.File.Length == 0 || request.File.Length > maximumBytes)
			throw new BusinessException("AdmissionCrm:InvalidDocumentContent");
		await using var stream = request.File.OpenReadStream();
		await using var memory = new MemoryStream((int)Math.Min(request.File.Length, 1024 * 1024));
		await stream.CopyToAsync(memory, HttpContext.RequestAborted);
		if (memory.Length > maximumBytes) throw new BusinessException("AdmissionCrm:DocumentFileSizeExceeded");
		return await applicantService.UploadRegistrationDocumentAsync(
			tenantId,
			token,
			request.AccessSessionToken,
			new ProcessedAdmissionCrmApplicantDocumentUploadDto
			{
				RequirementKey = request.RequirementKey,
				FileName = request.File.FileName,
				ContentType = request.File.ContentType,
				Size = request.File.Length,
				Content = memory.ToArray(),
				ValidUntil = request.ValidUntil
			});
	}

	[HttpGet("{tenantId}/status/{token}/exam/available-slots")]
	public Task<AdmissionCrmPublicExamSlotListDto> GetAvailableExamSlotsAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromHeader(Name = "X-Admission-Access-Session"), Required] string accessSessionToken)
	{
		return applicantService.GetAvailableExamSlotsAsync(tenantId, token, accessSessionToken);
	}

	[HttpPost("{tenantId}/status/{token}/exam/schedule")]
	public Task<AdmissionCrmEntranceExamDto> ScheduleExamAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromBody] ScheduleAdmissionCrmPublicExamDto input)
	{
		return applicantService.ScheduleExamAsync(tenantId, token, input);
	}

	[HttpGet("{tenantId}/status/{token}/events")]
	public Task<AdmissionCrmPublicEventListDto> GetAvailableEventsAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromHeader(Name = "X-Admission-Access-Session"), Required] string accessSessionToken)
	{
		return applicantService.GetAvailableEventsAsync(tenantId, token, accessSessionToken);
	}

	[HttpPost("{tenantId}/status/{token}/events/{eventId}/reserve")]
	public Task<AdmissionCrmPublicEventDto> ReserveEventAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromRoute] Guid eventId,
		[FromBody] ReserveAdmissionCrmPublicEventDto input)
	{
		return applicantService.ReserveEventAsync(tenantId, token, eventId, input);
	}

	[HttpPost("{tenantId}/status/{token}/events/{eventId}/cancel")]
	public Task<AdmissionCrmPublicEventDto> CancelEventRegistrationAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromRoute] Guid eventId,
		[FromBody] CancelAdmissionCrmPublicEventRegistrationDto input)
	{
		return applicantService.CancelEventRegistrationAsync(tenantId, token, eventId, input);
	}

	[HttpPost("{tenantId}/status/{token}/offers/{offerId}/respond")]
	public Task<AdmissionApplicantStatusOfferDto> RespondToOfferAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromRoute] Guid offerId,
		[FromBody] RespondAdmissionApplicantOfferDto input)
	{
		return applicantService.RespondToOfferAsync(tenantId, token, offerId, input);
	}

	[HttpPost("{tenantId}/communication/delivery-logs/{deliveryLogId}/callback")]
	public async Task<AdmissionCrmMessageDeliveryLogDto> ApplyDeliveryCallbackAsync(
		[FromRoute] Guid tenantId,
		[FromRoute] Guid deliveryLogId,
		[FromBody] ApplyAdmissionCrmDeliveryCallbackDto input)
	{
		return await callbackService.ApplyDeliveryCallbackAsync(
			tenantId,
			deliveryLogId,
			input);
	}

	[HttpPost("{tenantId}/communication/{academicYearId}/{channel}/delivery-callback")]
	public async Task<AdmissionCrmMessageDeliveryLogDto> ApplyDeliveryCallbackByProviderMessageAsync(
		[FromRoute] Guid tenantId,
		[FromRoute] Guid academicYearId,
		[FromRoute] AdmissionCrmMessageChannel channel,
		[FromBody] ApplyAdmissionCrmDeliveryCallbackDto input)
	{
		return await callbackService.ApplyDeliveryCallbackByProviderMessageAsync(
			tenantId,
			academicYearId,
			channel,
			input);
	}
}

public class PublicAdmissionCrmDocumentUploadRequest
{
	[Required]
	public string AccessSessionToken { get; set; } = string.Empty;
	[Required]
	public string RequirementKey { get; set; } = string.Empty;
	public DateTime? ValidUntil { get; set; }
	[Required]
	public IFormFile? File { get; set; }
}
