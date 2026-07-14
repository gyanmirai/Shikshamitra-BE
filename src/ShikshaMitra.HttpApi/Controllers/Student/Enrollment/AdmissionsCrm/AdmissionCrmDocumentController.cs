using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmDocumentController(
	IAdmissionCrmDocumentAppService documentAppService) : AbpControllerBase
{
	[HttpGet("{academicYearId}/configuration/document-checklists")]
	public Task<PagedResultDto<AdmissionCrmDocumentChecklistTemplateDto>> GetTemplatesAsync(
		Guid academicYearId,
		[FromQuery] GetAdmissionCrmDocumentChecklistTemplateListInput input) =>
		documentAppService.GetTemplatesAsync(academicYearId, input);

	[HttpPost("{academicYearId}/configuration/document-checklists")]
	public Task<AdmissionCrmDocumentChecklistTemplateDto> CreateTemplateAsync(
		Guid academicYearId,
		[FromBody] AdmissionCrmDocumentChecklistTemplateUpsertDto input) =>
		documentAppService.CreateTemplateAsync(academicYearId, input);

	[HttpPut("configuration/document-checklists/{id}")]
	public Task<AdmissionCrmDocumentChecklistTemplateDto> UpdateTemplateAsync(
		Guid id,
		[FromBody] AdmissionCrmDocumentChecklistTemplateUpsertDto input) =>
		documentAppService.UpdateTemplateAsync(id, input);

	[HttpPost("configuration/document-checklists/{id}/publish")]
	public Task<AdmissionCrmDocumentChecklistTemplateDto> PublishTemplateAsync(Guid id) =>
		documentAppService.PublishTemplateAsync(id);

	[HttpPost("configuration/document-checklists/{id}/new-version")]
	public Task<AdmissionCrmDocumentChecklistTemplateDto> CreateTemplateVersionAsync(Guid id) =>
		documentAppService.CreateTemplateVersionAsync(id);

	[HttpPost("configuration/document-checklists/{id}/retire")]
	public Task<AdmissionCrmDocumentChecklistTemplateDto> RetireTemplateAsync(Guid id) =>
		documentAppService.RetireTemplateAsync(id);

	[HttpPost("registrations/{registrationApplicationId}/document-checklist")]
	public Task<AdmissionCrmApplicantDocumentChecklistDto> AssignChecklistAsync(
		Guid registrationApplicationId,
		[FromBody] AssignAdmissionCrmDocumentChecklistDto input) =>
		documentAppService.AssignChecklistAsync(registrationApplicationId, input);

	[HttpGet("registrations/{registrationApplicationId}/document-checklist")]
	public Task<AdmissionCrmApplicantDocumentChecklistDto> GetChecklistAsync(Guid registrationApplicationId) =>
		documentAppService.GetChecklistAsync(registrationApplicationId);

	[HttpPost("documents/{documentId}/review")]
	public Task<AdmissionCrmApplicantDocumentDto> ReviewAsync(
		Guid documentId,
		[FromBody] ReviewAdmissionCrmApplicantDocumentDto input) =>
		documentAppService.ReviewAsync(documentId, input);

	[HttpPost("document-checklists/{assignmentId}/requirements/{requirementKey}/waive")]
	public Task<AdmissionCrmApplicantDocumentDto> WaiveAsync(
		Guid assignmentId,
		string requirementKey,
		[FromBody] AdmissionCrmDocumentReasonDto input) =>
		documentAppService.WaiveAsync(assignmentId, requirementKey, input);

	[HttpPost("documents/{documentId}/original-seen")]
	public Task<AdmissionCrmApplicantDocumentDto> MarkOriginalSeenAsync(Guid documentId) =>
		documentAppService.MarkOriginalSeenAsync(documentId);

	[HttpPost("document-checklists/{assignmentId}/confirm-readiness")]
	public Task<AdmissionCrmApplicantDocumentChecklistDto> ConfirmReadinessAsync(
		Guid assignmentId,
		[FromBody] ConfirmAdmissionCrmReadinessDto input) =>
		documentAppService.ConfirmReadinessAsync(assignmentId, input);

	[HttpGet("documents/{documentId}/download")]
	public async Task<IActionResult> DownloadAsync(Guid documentId)
	{
		var document = await documentAppService.DownloadAsync(documentId);
		return File(document.Content, document.ContentType, document.FileName);
	}
}
