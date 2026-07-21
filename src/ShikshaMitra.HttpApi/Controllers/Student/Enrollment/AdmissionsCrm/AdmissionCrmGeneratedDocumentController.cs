using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmGeneratedDocumentController(
	IAdmissionCrmGeneratedDocumentAppService appService) : AbpControllerBase
{
	[HttpPost("leads/{leadId}/documents/preview")]
	public Task<AdmissionCrmGeneratedDocumentDto> PreviewAsync(
		Guid leadId,
		[FromBody] PreviewAdmissionCrmGeneratedDocumentDto input) =>
		appService.PreviewAsync(leadId, input);

	[HttpPost("leads/{leadId}/documents/issue")]
	public Task<AdmissionCrmGeneratedDocumentDto> IssueAsync(
		Guid leadId,
		[FromBody] IssueAdmissionCrmGeneratedDocumentDto input) =>
		appService.IssueAsync(leadId, input);

	[HttpGet("leads/{leadId}/documents/history")]
	public Task<IReadOnlyList<AdmissionCrmGeneratedDocumentHistoryDto>> GetHistoryAsync(Guid leadId) =>
		appService.GetHistoryAsync(leadId);

	[HttpGet("leads/{leadId}/documents/availability")]
	public Task<IReadOnlyList<AdmissionCrmGeneratedDocumentAvailabilityDto>> GetAvailabilityAsync(Guid leadId) =>
		appService.GetAvailabilityAsync(leadId);
}
