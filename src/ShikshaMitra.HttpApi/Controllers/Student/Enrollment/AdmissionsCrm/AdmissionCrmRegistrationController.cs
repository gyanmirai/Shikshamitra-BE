using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmRegistrationController(
	IAdmissionCrmRegistrationTemplateService templateService,
	IAdmissionCrmRegistrationNumberSeriesService numberSeriesService,
	IAdmissionCrmRegistrationIssuanceService issuanceService,
	IAdmissionCrmRegistrationQueryService queryService,
	IAdmissionCrmRegistrationResponseService responseService,
	IAdmissionCrmRegistrationLinkService linkService,
	IAdmissionCrmRegistrationFeeService feeService,
	IAdmissionCrmLeadCaptureService leadCaptureService) : AbpControllerBase
{
	[HttpPost("{academicYearId}/registrations/direct")]
	public Task<AdmissionCrmDirectRegistrationResultDto> CreateDirectRegistrationAsync(
		Guid academicYearId,
		[FromBody] CreateDirectAdmissionCrmRegistrationDto input) =>
		leadCaptureService.CreateDirectRegistrationAsync(academicYearId, input);

	[HttpPost("{academicYearId}/registrations/from-paper")]
	public Task<AdmissionCrmDirectRegistrationResultDto> CreatePaperRegistrationAsync(
		Guid academicYearId,
		[FromBody] CreatePaperAdmissionCrmRegistrationDto input) =>
		leadCaptureService.CreatePaperRegistrationAsync(academicYearId, input);

	[HttpGet("{academicYearId}/configuration/registration-templates")]
	public Task<PagedResultDto<AdmissionCrmRegistrationTemplateDto>> GetTemplatesAsync(
		Guid academicYearId,
		[FromQuery] GetAdmissionCrmRegistrationTemplateListInput input) =>
		templateService.GetTemplatesAsync(academicYearId, input);

	[HttpGet("configuration/registration-templates/{id}")]
	public Task<AdmissionCrmRegistrationTemplateDto> GetTemplateAsync(Guid id) =>
		templateService.GetTemplateAsync(id);

	[HttpPost("{academicYearId}/configuration/registration-templates")]
	public Task<AdmissionCrmRegistrationTemplateDto> CreateTemplateAsync(
		Guid academicYearId,
		[FromBody] CreateAdmissionCrmRegistrationTemplateDto input) =>
		templateService.CreateTemplateAsync(academicYearId, input);

	[HttpPut("configuration/registration-templates/{id}")]
	public Task<AdmissionCrmRegistrationTemplateDto> UpdateTemplateAsync(
		Guid id,
		[FromBody] UpdateAdmissionCrmRegistrationTemplateDto input) =>
		templateService.UpdateTemplateAsync(id, input);

	[HttpPost("configuration/registration-templates/{id}/publish")]
	public Task<AdmissionCrmRegistrationTemplateDto> PublishTemplateAsync(Guid id) =>
		templateService.PublishTemplateAsync(id);

	[HttpPost("configuration/registration-templates/{id}/new-version")]
	public Task<AdmissionCrmRegistrationTemplateDto> CreateTemplateVersionAsync(Guid id) =>
		templateService.CreateTemplateVersionAsync(id);

	[HttpPost("configuration/registration-templates/{id}/retire")]
	public Task<AdmissionCrmRegistrationTemplateDto> RetireTemplateAsync(Guid id) =>
		templateService.RetireTemplateAsync(id);

	[HttpGet("{academicYearId}/configuration/form-number-series")]
	public Task<PagedResultDto<AdmissionCrmFormNumberSeriesDto>> GetFormNumberSeriesAsync(Guid academicYearId) =>
		numberSeriesService.GetFormNumberSeriesAsync(academicYearId);

	[HttpPost("{academicYearId}/configuration/form-number-series")]
	public Task<AdmissionCrmFormNumberSeriesDto> CreateFormNumberSeriesAsync(
		Guid academicYearId,
		[FromBody] AdmissionCrmFormNumberSeriesUpsertDto input) =>
		numberSeriesService.CreateFormNumberSeriesAsync(academicYearId, input);

	[HttpPut("configuration/form-number-series/{id}")]
	public Task<AdmissionCrmFormNumberSeriesDto> UpdateFormNumberSeriesAsync(
		Guid id,
		[FromBody] AdmissionCrmFormNumberSeriesUpsertDto input) =>
		numberSeriesService.UpdateFormNumberSeriesAsync(id, input);

	[HttpPost("leads/{leadId}/registrations/issue")]
	public Task<AdmissionCrmRegistrationIssuanceResultDto> IssueAsync(
		Guid leadId,
		[FromBody] IssueAdmissionCrmRegistrationApplicationDto input) =>
		issuanceService.IssueAsync(leadId, input);

	[HttpGet("leads/{leadId}/registrations/readiness")]
	public Task<AdmissionCrmRegistrationReadinessDto> GetReadinessAsync(Guid leadId) =>
		issuanceService.GetReadinessAsync(leadId);

	[HttpPost("registrations/{id}/invitation/send")]
	public Task<AdmissionCrmRegistrationIssuanceResultDto> SendInvitationAsync(Guid id) =>
		issuanceService.SendInvitationAsync(id);

	[HttpGet("registrations/{id}")]
	public Task<AdmissionCrmRegistrationApplicationDto> GetApplicationAsync(Guid id) =>
		queryService.GetApplicationAsync(id);

	[HttpGet("leads/{leadId}/registrations/current")]
	public Task<AdmissionCrmRegistrationApplicationDto> GetCurrentApplicationByLeadAsync(Guid leadId) =>
		queryService.GetCurrentApplicationByLeadAsync(leadId);

	[HttpPut("registrations/{id}/staff-responses")]
	public Task<AdmissionCrmRegistrationApplicationDto> SaveStaffResponsesAsync(
		Guid id,
		[FromBody] SaveAdmissionCrmRegistrationResponsesDto input) =>
		responseService.SaveStaffResponsesAsync(id, input);

	[HttpPost("registrations/{id}/staff-submit")]
	public Task<AdmissionCrmRegistrationApplicationDto> SubmitStaffResponsesAsync(
		Guid id,
		[FromBody] SaveAdmissionCrmRegistrationResponsesDto input) =>
		responseService.SubmitStaffResponsesAsync(id, input);

	[HttpPost("registrations/{id}/return-for-correction")]
	public Task<AdmissionCrmRegistrationApplicationDto> ReturnForCorrectionAsync(
		Guid id,
		[FromBody] ReturnAdmissionCrmRegistrationForCorrectionDto input) =>
		responseService.ReturnForCorrectionAsync(id, input);

	[HttpPost("registrations/{id}/verify")]
	public Task<AdmissionCrmRegistrationApplicationDto> VerifyAsync(Guid id) =>
		responseService.VerifyAsync(id);

	[HttpPost("registrations/{id}/extend")]
	public Task<AdmissionCrmRegistrationApplicationDto> ExtendAsync(
		Guid id,
		[FromBody] ExtendAdmissionCrmRegistrationLinkDto input) =>
		linkService.ExtendAsync(id, input);

	[HttpPost("registrations/{id}/revoke")]
	public Task<AdmissionCrmRegistrationApplicationDto> RevokeAsync(
		Guid id,
		[FromBody] AdmissionCrmRegistrationReasonDto input) =>
		linkService.RevokeAsync(id, input);

	[HttpPost("registrations/{id}/reissue")]
	public Task<AdmissionCrmRegistrationApplicationDto> ReissueAsync(
		Guid id,
		[FromBody] AdmissionCrmRegistrationReissueDto input) =>
		linkService.ReissueAsync(id, input);

	[HttpPost("registrations/{id}/payment-reference")]
	public Task<AdmissionCrmRegistrationApplicationDto> RecordPaymentReferenceAsync(
		Guid id,
		[FromBody] RecordAdmissionCrmRegistrationPaymentReferenceDto input) =>
		feeService.RecordPaymentReferenceAsync(id, input);

	[HttpPost("registrations/{id}/fee-waiver")]
	public Task<AdmissionCrmRegistrationApplicationDto> WaiveFeeAsync(
		Guid id,
		[FromBody] AdmissionCrmRegistrationReasonDto input) =>
		feeService.WaiveFeeAsync(id, input);
}
