using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmCatalogController(IAdmissionCrmCatalogAppService appService) : AbpControllerBase
{
	[HttpGet("{academicYearId}/catalog/inquiry-sources")]
	public Task<PagedResultDto<AdmissionCrmInquirySourceDto>> GetInquirySourcesAsync(Guid academicYearId, [FromQuery] bool includeInactive = true) =>
		appService.GetInquirySourcesAsync(academicYearId, includeInactive);
	[HttpPost("{academicYearId}/catalog/inquiry-sources")]
	public Task<AdmissionCrmInquirySourceDto> CreateInquirySourceAsync(Guid academicYearId, [FromBody] AdmissionCrmInquirySourceUpsertDto input) =>
		appService.CreateInquirySourceAsync(academicYearId, input);
	[HttpPut("catalog/inquiry-sources/{id}")]
	public Task<AdmissionCrmInquirySourceDto> UpdateInquirySourceAsync(Guid id, [FromBody] AdmissionCrmInquirySourceUpsertDto input) =>
		appService.UpdateInquirySourceAsync(id, input);
	[HttpPatch("catalog/inquiry-sources/{id}/status")]
	public Task<AdmissionCrmInquirySourceDto> SetInquirySourceStatusAsync(Guid id, [FromBody] AdmissionCrmCatalogStatusDto input) =>
		appService.SetInquirySourceStatusAsync(id, input);

	[HttpGet("{academicYearId}/catalog/exam-slots")]
	public Task<PagedResultDto<AdmissionCrmExamSlotDto>> GetExamSlotsAsync(Guid academicYearId, [FromQuery] DateTimeOffset? from = null,
		[FromQuery] DateTimeOffset? to = null, [FromQuery] bool includeInactive = false) =>
		appService.GetExamSlotsAsync(academicYearId, from, to, includeInactive);
	[HttpPost("{academicYearId}/catalog/exam-slots")]
	public Task<AdmissionCrmExamSlotDto> CreateExamSlotAsync(Guid academicYearId, [FromBody] AdmissionCrmExamSlotUpsertDto input) =>
		appService.CreateExamSlotAsync(academicYearId, input);
	[HttpPut("catalog/exam-slots/{id}")]
	public Task<AdmissionCrmExamSlotDto> UpdateExamSlotAsync(Guid id, [FromBody] AdmissionCrmExamSlotUpsertDto input) =>
		appService.UpdateExamSlotAsync(id, input);
	[HttpPatch("catalog/exam-slots/{id}/status")]
	public Task<AdmissionCrmExamSlotDto> SetExamSlotStatusAsync(Guid id, [FromBody] AdmissionCrmCatalogStatusDto input) =>
		appService.SetExamSlotStatusAsync(id, input);

	[HttpGet("{academicYearId}/catalog/document-types")]
	public Task<PagedResultDto<AdmissionCrmDocumentTypeDto>> GetDocumentTypesAsync(Guid academicYearId, [FromQuery] bool includeInactive = true) =>
		appService.GetDocumentTypesAsync(academicYearId, includeInactive);
	[HttpPost("{academicYearId}/catalog/document-types")]
	public Task<AdmissionCrmDocumentTypeDto> CreateDocumentTypeAsync(Guid academicYearId, [FromBody] AdmissionCrmDocumentTypeUpsertDto input) =>
		appService.CreateDocumentTypeAsync(academicYearId, input);
	[HttpPut("catalog/document-types/{id}")]
	public Task<AdmissionCrmDocumentTypeDto> UpdateDocumentTypeAsync(Guid id, [FromBody] AdmissionCrmDocumentTypeUpsertDto input) =>
		appService.UpdateDocumentTypeAsync(id, input);
	[HttpPatch("catalog/document-types/{id}/status")]
	public Task<AdmissionCrmDocumentTypeDto> SetDocumentTypeStatusAsync(Guid id, [FromBody] AdmissionCrmCatalogStatusDto input) =>
		appService.SetDocumentTypeStatusAsync(id, input);
}
