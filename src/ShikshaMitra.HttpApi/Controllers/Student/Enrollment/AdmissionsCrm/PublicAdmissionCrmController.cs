using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/public/student-enrollment/admissions-crm")]
[AllowAnonymous]
[DisableAuditing]
[EnableRateLimiting(AdmissionCrmRateLimitPolicies.Public)]
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
	[EnableRateLimiting(AdmissionCrmRateLimitPolicies.Otp)]
	public async Task<AdmissionApplicantStatusOtpChallengeDto> IssueStatusOtpAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token)
	{
		return await accessService.IssueStatusOtpAsync(tenantId, token);
	}

	[HttpPost("{tenantId}/family/otp")]
	[EnableRateLimiting(AdmissionCrmRateLimitPolicies.Otp)]
	public Task<AdmissionApplicantStatusOtpChallengeDto> IssueFamilyAccessOtpAsync(
		[FromRoute] Guid tenantId,
		[FromBody] StartAdmissionFamilyAccessDto input)
	{
		return accessService.IssueFamilyAccessOtpAsync(tenantId, input);
	}

	[HttpPost("{tenantId}/family/verify")]
	[EnableRateLimiting(AdmissionCrmRateLimitPolicies.Otp)]
	public Task<AdmissionApplicantStatusDto> VerifyFamilyAccessOtpAsync(
		[FromRoute] Guid tenantId,
		[FromBody] VerifyAdmissionFamilyAccessDto input)
	{
		return accessService.VerifyFamilyAccessOtpAsync(tenantId, input);
	}

	[HttpPost("{tenantId}/status/{token}/verify")]
	[EnableRateLimiting(AdmissionCrmRateLimitPolicies.Otp)]
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
		[FromHeader(Name = "X-Admission-Access-Session"), Required] string accessSessionToken,
		[FromBody] UpdatePublicAdmissionCommunicationPreferencesDto input)
	{
		input.AccessSessionToken = accessSessionToken;
		return accessService.UpdateCommunicationPreferencesAsync(tenantId, token, input);
	}

	[HttpPost("{tenantId}/status/{token}/withdraw")]
	public Task<AdmissionApplicantStatusDto> WithdrawApplicationAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromHeader(Name = "X-Admission-Access-Session"), Required] string accessSessionToken,
		[FromBody] WithdrawPublicAdmissionApplicationDto input)
	{
		input.AccessSessionToken = accessSessionToken;
		return accessService.WithdrawApplicationAsync(tenantId, token, input);
	}

	[HttpPut("{tenantId}/status/{token}/registration")]
	public async Task<AdmissionCrmRegistrationApplicationDto> SaveRegistrationAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromHeader(Name = "X-Admission-Access-Session"), Required] string accessSessionToken,
		[FromBody] SavePublicAdmissionCrmRegistrationDto input)
	{
		input.AccessSessionToken = accessSessionToken;
		return await applicantService.SaveRegistrationAsync(tenantId, token, input);
	}

	[HttpPost("{tenantId}/status/{token}/registration/submit")]
	public async Task<AdmissionCrmRegistrationApplicationDto> SubmitRegistrationAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromHeader(Name = "X-Admission-Access-Session"), Required] string accessSessionToken,
		[FromBody] SavePublicAdmissionCrmRegistrationDto input)
	{
		input.AccessSessionToken = accessSessionToken;
		return await applicantService.SubmitRegistrationAsync(tenantId, token, input);
	}

	[HttpPost("{tenantId}/status/{token}/registration/documents")]
	[Consumes("multipart/form-data")]
	[EnableRateLimiting(AdmissionCrmRateLimitPolicies.Upload)]
	public async Task<AdmissionCrmApplicantDocumentChecklistDto> UploadRegistrationDocumentAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromHeader(Name = "X-Admission-Access-Session"), Required] string accessSessionToken,
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
			accessSessionToken,
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
		[FromHeader(Name = "X-Admission-Access-Session"), Required] string accessSessionToken,
		[FromBody] ScheduleAdmissionCrmPublicExamDto input)
	{
		input.AccessSessionToken = accessSessionToken;
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
		[FromHeader(Name = "X-Admission-Access-Session"), Required] string accessSessionToken,
		[FromBody] ReserveAdmissionCrmPublicEventDto input)
	{
		input.AccessSessionToken = accessSessionToken;
		return applicantService.ReserveEventAsync(tenantId, token, eventId, input);
	}

	[HttpPost("{tenantId}/status/{token}/events/{eventId}/cancel")]
	public Task<AdmissionCrmPublicEventDto> CancelEventRegistrationAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromRoute] Guid eventId,
		[FromHeader(Name = "X-Admission-Access-Session"), Required] string accessSessionToken,
		[FromBody] CancelAdmissionCrmPublicEventRegistrationDto input)
	{
		input.AccessSessionToken = accessSessionToken;
		return applicantService.CancelEventRegistrationAsync(tenantId, token, eventId, input);
	}

	[HttpPost("{tenantId}/status/{token}/offers/{offerId}/respond")]
	public Task<AdmissionApplicantStatusOfferDto> RespondToOfferAsync(
		[FromRoute] Guid tenantId,
		[FromRoute, Required] string token,
		[FromRoute] Guid offerId,
		[FromHeader(Name = "X-Admission-Access-Session"), Required] string accessSessionToken,
		[FromBody] RespondAdmissionApplicantOfferDto input)
	{
		input.AccessSessionToken = accessSessionToken;
		return applicantService.RespondToOfferAsync(tenantId, token, offerId, input);
	}

	[HttpPost("{tenantId}/communication/delivery-logs/{deliveryLogId}/callback")]
	[EnableRateLimiting(AdmissionCrmRateLimitPolicies.Callback)]
	public async Task<AdmissionCrmMessageDeliveryLogDto> ApplyDeliveryCallbackAsync(
		[FromRoute] Guid tenantId,
		[FromRoute] Guid deliveryLogId,
		[FromHeader(Name = "X-Admission-Callback-Signature"), Required] string callbackSignature,
		[FromHeader(Name = "X-Admission-Callback-Timestamp"), Required] string callbackTimestamp,
		[FromHeader(Name = "X-Admission-Callback-Nonce"), Required] string callbackNonce,
		[FromBody] JsonElement body)
	{
		var input = BuildSignedCallback(body, callbackSignature, callbackTimestamp, callbackNonce);
		return await callbackService.ApplyDeliveryCallbackAsync(
			tenantId,
			deliveryLogId,
			input);
	}

	[HttpPost("{tenantId}/communication/{academicYearId}/{channel}/delivery-callback")]
	[EnableRateLimiting(AdmissionCrmRateLimitPolicies.Callback)]
	public async Task<AdmissionCrmMessageDeliveryLogDto> ApplyDeliveryCallbackByProviderMessageAsync(
		[FromRoute] Guid tenantId,
		[FromRoute] Guid academicYearId,
		[FromRoute] AdmissionCrmMessageChannel channel,
		[FromHeader(Name = "X-Admission-Callback-Signature"), Required] string callbackSignature,
		[FromHeader(Name = "X-Admission-Callback-Timestamp"), Required] string callbackTimestamp,
		[FromHeader(Name = "X-Admission-Callback-Nonce"), Required] string callbackNonce,
		[FromBody] JsonElement body)
	{
		var input = BuildSignedCallback(body, callbackSignature, callbackTimestamp, callbackNonce);
		return await callbackService.ApplyDeliveryCallbackByProviderMessageAsync(
			tenantId,
			academicYearId,
			channel,
			input);
	}

	private static ApplyAdmissionCrmDeliveryCallbackDto BuildSignedCallback(
		JsonElement body, string signature, string timestamp, string nonce)
	{
		var rawBody = body.GetRawText();
		var input = JsonSerializer.Deserialize<ApplyAdmissionCrmDeliveryCallbackDto>(rawBody,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
			?? throw new BusinessException("AdmissionCrm:DeliveryCallbackPayloadInvalid");
		input.CallbackSignature = signature;
		input.CallbackTimestamp = timestamp;
		input.CallbackNonce = nonce;
		input.RawBody = rawBody;
		return input;
	}
}

public class PublicAdmissionCrmDocumentUploadRequest
{
	[Required]
	public string RequirementKey { get; set; } = string.Empty;
	public DateTime? ValidUntil { get; set; }
	[Required]
	public IFormFile? File { get; set; }
}
