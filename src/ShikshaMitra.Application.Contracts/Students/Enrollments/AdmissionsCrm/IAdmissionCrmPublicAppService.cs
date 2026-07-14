using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmPublicAppService : IApplicationService
{
	Task<PublicAdmissionInquiryFormDto> GetPublicFormAsync(Guid tenantId, Guid templateId, string? language = null);
	Task<PublicAdmissionInquirySubmitResultDto> SubmitPublicFormAsync(
		Guid tenantId,
		Guid templateId,
		PublicAdmissionInquirySubmitDto input,
		string? ipAddress,
		string? userAgent);
	Task<AdmissionApplicantStatusOtpChallengeDto> IssueStatusOtpAsync(Guid tenantId, string token);
	Task<AdmissionApplicantStatusDto> VerifyStatusOtpAsync(
		Guid tenantId,
		string token,
		VerifyAdmissionApplicantStatusOtpDto input);
	Task<AdmissionCrmRegistrationApplicationDto> SaveRegistrationAsync(
		Guid tenantId,
		string token,
		SavePublicAdmissionCrmRegistrationDto input);
	Task<AdmissionCrmRegistrationApplicationDto> SubmitRegistrationAsync(
		Guid tenantId,
		string token,
		SavePublicAdmissionCrmRegistrationDto input);
	Task<AdmissionCrmApplicantDocumentChecklistDto> UploadRegistrationDocumentAsync(
		Guid tenantId,
		string token,
		string accessSessionToken,
		ProcessedAdmissionCrmApplicantDocumentUploadDto input);
	Task<AdmissionCrmPublicExamSlotListDto> GetAvailableExamSlotsAsync(
		Guid tenantId,
		string token,
		string accessSessionToken);
	Task<AdmissionCrmEntranceExamDto> ScheduleExamAsync(
		Guid tenantId,
		string token,
		ScheduleAdmissionCrmPublicExamDto input);
	Task<AdmissionApplicantStatusOfferDto> RespondToOfferAsync(
		Guid tenantId,
		string token,
		Guid offerId,
		RespondAdmissionApplicantOfferDto input);
	Task<AdmissionCrmMessageDeliveryLogDto> ApplyDeliveryCallbackAsync(
		Guid tenantId,
		Guid deliveryLogId,
		ApplyAdmissionCrmDeliveryCallbackDto input);
	Task<AdmissionCrmMessageDeliveryLogDto> ApplyDeliveryCallbackByProviderMessageAsync(
		Guid tenantId,
		Guid academicYearId,
		AdmissionCrmMessageChannel channel,
		ApplyAdmissionCrmDeliveryCallbackDto input);
}
