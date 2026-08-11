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
	IAdmissionCrmGeneratedDocumentCommandService commandService,
	IAdmissionCrmGeneratedDocumentQueryService queryService) : AbpControllerBase
{
	[HttpPost("leads/{leadId}/documents/preview")]
	public Task<AdmissionCrmGeneratedDocumentDto> PreviewAsync(
		Guid leadId,
		[FromBody] PreviewAdmissionCrmGeneratedDocumentDto input) =>
		commandService.PreviewAsync(leadId, input);

	[HttpPost("leads/{leadId}/documents/issue")]
	public Task<AdmissionCrmGeneratedDocumentDto> IssueAsync(
		Guid leadId,
		[FromBody] IssueAdmissionCrmGeneratedDocumentDto input) =>
		commandService.IssueAsync(leadId, input);

	[HttpPost("leads/{leadId}/documents/send-email")]
	public Task<AdmissionCrmGeneratedDocumentDeliveryDto> SendEmailAsync(
		Guid leadId,
		[FromBody] SendAdmissionCrmGeneratedDocumentEmailDto input) =>
		commandService.SendEmailAsync(leadId, input);

	[HttpGet("leads/{leadId}/documents/history")]
	public Task<IReadOnlyList<AdmissionCrmGeneratedDocumentHistoryDto>> GetHistoryAsync(Guid leadId) =>
		queryService.GetHistoryAsync(leadId);

	[HttpGet("leads/{leadId}/documents/availability")]
	public Task<IReadOnlyList<AdmissionCrmGeneratedDocumentAvailabilityDto>> GetAvailabilityAsync(Guid leadId) =>
		queryService.GetAvailabilityAsync(leadId);
}
