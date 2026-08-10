using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.IO;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmDocumentController(
	IAdmissionCrmDocumentTemplateService templateService,
	IAdmissionCrmDocumentChecklistService checklistService,
	IAdmissionCrmDocumentReviewService reviewService) : AbpControllerBase
{
	[HttpGet("{academicYearId}/configuration/document-checklists")]
	public Task<PagedResultDto<AdmissionCrmDocumentChecklistTemplateDto>> GetTemplatesAsync(
		Guid academicYearId,
		[FromQuery] GetAdmissionCrmDocumentChecklistTemplateListInput input) =>
		templateService.GetTemplatesAsync(academicYearId, input);

	[HttpPost("{academicYearId}/configuration/document-checklists")]
	public Task<AdmissionCrmDocumentChecklistTemplateDto> CreateTemplateAsync(
		Guid academicYearId,
		[FromBody] AdmissionCrmDocumentChecklistTemplateUpsertDto input) =>
		templateService.CreateTemplateAsync(academicYearId, input);

	[HttpPut("configuration/document-checklists/{id}")]
	public Task<AdmissionCrmDocumentChecklistTemplateDto> UpdateTemplateAsync(
		Guid id,
		[FromBody] AdmissionCrmDocumentChecklistTemplateUpsertDto input) =>
		templateService.UpdateTemplateAsync(id, input);

	[HttpPost("configuration/document-checklists/{id}/publish")]
	public Task<AdmissionCrmDocumentChecklistTemplateDto> PublishTemplateAsync(Guid id) =>
		templateService.PublishTemplateAsync(id);

	[HttpPost("configuration/document-checklists/{id}/new-version")]
	public Task<AdmissionCrmDocumentChecklistTemplateDto> CreateTemplateVersionAsync(Guid id) =>
		templateService.CreateTemplateVersionAsync(id);

	[HttpPost("configuration/document-checklists/{id}/retire")]
	public Task<AdmissionCrmDocumentChecklistTemplateDto> RetireTemplateAsync(Guid id) =>
		templateService.RetireTemplateAsync(id);

	[HttpPost("registrations/{registrationApplicationId}/document-checklist")]
	public Task<AdmissionCrmApplicantDocumentChecklistDto> AssignChecklistAsync(
		Guid registrationApplicationId,
		[FromBody] AssignAdmissionCrmDocumentChecklistDto input) =>
		checklistService.AssignChecklistAsync(registrationApplicationId, input);

	[HttpGet("registrations/{registrationApplicationId}/document-checklist")]
	public Task<AdmissionCrmApplicantDocumentChecklistDto> GetChecklistAsync(Guid registrationApplicationId) =>
		checklistService.GetChecklistAsync(registrationApplicationId);

	[HttpPost("documents/{documentId}/review")]
	public Task<AdmissionCrmApplicantDocumentDto> ReviewAsync(
		Guid documentId,
		[FromBody] ReviewAdmissionCrmApplicantDocumentDto input) =>
		reviewService.ReviewAsync(documentId, input);

	[HttpPost("registrations/{registrationApplicationId}/documents")]
	[Consumes("multipart/form-data")]
	public async Task<AdmissionCrmApplicantDocumentChecklistDto> UploadFromStaffAsync(
		Guid registrationApplicationId,
		[FromForm] AdmissionCrmStaffDocumentUploadRequest request)
	{
		const long maximumBytes = 25L * 1024 * 1024;
		if (request.File == null || request.File.Length == 0 || request.File.Length > maximumBytes)
			throw new BusinessException("AdmissionCrm:InvalidDocumentContent");
		await using var stream = request.File.OpenReadStream();
		await using var memory = new MemoryStream((int)Math.Min(request.File.Length, 1024 * 1024));
		await stream.CopyToAsync(memory, HttpContext.RequestAborted);
		if (memory.Length > maximumBytes) throw new BusinessException("AdmissionCrm:DocumentFileSizeExceeded");
		return await reviewService.UploadFromStaffAsync(
			registrationApplicationId,
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

	[HttpPost("document-checklists/{assignmentId}/requirements/{requirementKey}/waive")]
	public Task<AdmissionCrmApplicantDocumentDto> WaiveAsync(
		Guid assignmentId,
		string requirementKey,
		[FromBody] AdmissionCrmDocumentReasonDto input) =>
		reviewService.WaiveAsync(assignmentId, requirementKey, input);

	[HttpPost("documents/{documentId}/original-seen")]
	public Task<AdmissionCrmApplicantDocumentDto> MarkOriginalSeenAsync(Guid documentId) =>
		reviewService.MarkOriginalSeenAsync(documentId);

	[HttpPost("document-checklists/{assignmentId}/confirm-readiness")]
	public Task<AdmissionCrmApplicantDocumentChecklistDto> ConfirmReadinessAsync(
		Guid assignmentId,
		[FromBody] ConfirmAdmissionCrmReadinessDto input) =>
		reviewService.ConfirmReadinessAsync(assignmentId, input);

	[HttpGet("documents/{documentId}/download")]
	public async Task<IActionResult> DownloadAsync(Guid documentId)
	{
		var document = await reviewService.DownloadAsync(documentId);
		return File(document.Content, document.ContentType, document.FileName);
	}
}

public class AdmissionCrmStaffDocumentUploadRequest
{
	[Required]
	public string RequirementKey { get; set; } = string.Empty;
	public DateTime? ValidUntil { get; set; }
	[Required]
	public IFormFile? File { get; set; }
}
