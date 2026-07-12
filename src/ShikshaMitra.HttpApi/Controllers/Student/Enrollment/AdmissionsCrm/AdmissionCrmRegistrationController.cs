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
	IAdmissionCrmRegistrationAppService registrationAppService) : AbpControllerBase
{
	[HttpGet("{academicYearId}/configuration/registration-templates")]
	public Task<PagedResultDto<AdmissionCrmRegistrationTemplateDto>> GetTemplatesAsync(
		Guid academicYearId,
		[FromQuery] GetAdmissionCrmRegistrationTemplateListInput input) =>
		registrationAppService.GetTemplatesAsync(academicYearId, input);

	[HttpGet("configuration/registration-templates/{id}")]
	public Task<AdmissionCrmRegistrationTemplateDto> GetTemplateAsync(Guid id) =>
		registrationAppService.GetTemplateAsync(id);

	[HttpPost("{academicYearId}/configuration/registration-templates")]
	public Task<AdmissionCrmRegistrationTemplateDto> CreateTemplateAsync(
		Guid academicYearId,
		[FromBody] CreateAdmissionCrmRegistrationTemplateDto input) =>
		registrationAppService.CreateTemplateAsync(academicYearId, input);

	[HttpPut("configuration/registration-templates/{id}")]
	public Task<AdmissionCrmRegistrationTemplateDto> UpdateTemplateAsync(
		Guid id,
		[FromBody] UpdateAdmissionCrmRegistrationTemplateDto input) =>
		registrationAppService.UpdateTemplateAsync(id, input);

	[HttpPost("configuration/registration-templates/{id}/publish")]
	public Task<AdmissionCrmRegistrationTemplateDto> PublishTemplateAsync(Guid id) =>
		registrationAppService.PublishTemplateAsync(id);

	[HttpPost("configuration/registration-templates/{id}/new-version")]
	public Task<AdmissionCrmRegistrationTemplateDto> CreateTemplateVersionAsync(Guid id) =>
		registrationAppService.CreateTemplateVersionAsync(id);

	[HttpPost("configuration/registration-templates/{id}/retire")]
	public Task<AdmissionCrmRegistrationTemplateDto> RetireTemplateAsync(Guid id) =>
		registrationAppService.RetireTemplateAsync(id);

	[HttpGet("{academicYearId}/configuration/form-number-series")]
	public Task<PagedResultDto<AdmissionCrmFormNumberSeriesDto>> GetFormNumberSeriesAsync(Guid academicYearId) =>
		registrationAppService.GetFormNumberSeriesAsync(academicYearId);

	[HttpPost("{academicYearId}/configuration/form-number-series")]
	public Task<AdmissionCrmFormNumberSeriesDto> CreateFormNumberSeriesAsync(
		Guid academicYearId,
		[FromBody] AdmissionCrmFormNumberSeriesUpsertDto input) =>
		registrationAppService.CreateFormNumberSeriesAsync(academicYearId, input);

	[HttpPut("configuration/form-number-series/{id}")]
	public Task<AdmissionCrmFormNumberSeriesDto> UpdateFormNumberSeriesAsync(
		Guid id,
		[FromBody] AdmissionCrmFormNumberSeriesUpsertDto input) =>
		registrationAppService.UpdateFormNumberSeriesAsync(id, input);

	[HttpPost("leads/{leadId}/registrations/issue")]
	public Task<AdmissionCrmRegistrationApplicationDto> IssueAsync(
		Guid leadId,
		[FromBody] IssueAdmissionCrmRegistrationApplicationDto input) =>
		registrationAppService.IssueAsync(leadId, input);

	[HttpGet("registrations/{id}")]
	public Task<AdmissionCrmRegistrationApplicationDto> GetApplicationAsync(Guid id) =>
		registrationAppService.GetApplicationAsync(id);

	[HttpGet("leads/{leadId}/registrations/current")]
	public Task<AdmissionCrmRegistrationApplicationDto> GetCurrentApplicationByLeadAsync(Guid leadId) =>
		registrationAppService.GetCurrentApplicationByLeadAsync(leadId);

	[HttpPost("registrations/{id}/return-for-correction")]
	public Task<AdmissionCrmRegistrationApplicationDto> ReturnForCorrectionAsync(
		Guid id,
		[FromBody] ReturnAdmissionCrmRegistrationForCorrectionDto input) =>
		registrationAppService.ReturnForCorrectionAsync(id, input);

	[HttpPost("registrations/{id}/verify")]
	public Task<AdmissionCrmRegistrationApplicationDto> VerifyAsync(Guid id) =>
		registrationAppService.VerifyAsync(id);

	[HttpPost("registrations/{id}/extend")]
	public Task<AdmissionCrmRegistrationApplicationDto> ExtendAsync(
		Guid id,
		[FromBody] ExtendAdmissionCrmRegistrationLinkDto input) =>
		registrationAppService.ExtendAsync(id, input);

	[HttpPost("registrations/{id}/revoke")]
	public Task<AdmissionCrmRegistrationApplicationDto> RevokeAsync(
		Guid id,
		[FromBody] AdmissionCrmRegistrationReasonDto input) =>
		registrationAppService.RevokeAsync(id, input);

	[HttpPost("registrations/{id}/reissue")]
	public Task<AdmissionCrmRegistrationApplicationDto> ReissueAsync(
		Guid id,
		[FromBody] AdmissionCrmRegistrationReissueDto input) =>
		registrationAppService.ReissueAsync(id, input);

	[HttpPost("registrations/{id}/payment-reference")]
	public Task<AdmissionCrmRegistrationApplicationDto> RecordPaymentReferenceAsync(
		Guid id,
		[FromBody] RecordAdmissionCrmRegistrationPaymentReferenceDto input) =>
		registrationAppService.RecordPaymentReferenceAsync(id, input);

	[HttpPost("registrations/{id}/fee-waiver")]
	public Task<AdmissionCrmRegistrationApplicationDto> WaiveFeeAsync(
		Guid id,
		[FromBody] AdmissionCrmRegistrationReasonDto input) =>
		registrationAppService.WaiveFeeAsync(id, input);
}
