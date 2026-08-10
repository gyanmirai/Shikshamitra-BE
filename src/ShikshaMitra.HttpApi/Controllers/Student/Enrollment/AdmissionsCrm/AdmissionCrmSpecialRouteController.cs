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
public class AdmissionCrmSpecialRouteController(
	IAdmissionCrmRouteDefinitionService definitionService,
	IAdmissionCrmRouteAssignmentService assignmentService,
	IAdmissionCrmRteAllotmentService rteAllotmentService,
	IAdmissionCrmRouteAppealService appealService) : AbpControllerBase
{
	[HttpPost("{academicYearId}/admission-routes")]
	public Task<AdmissionCrmRouteDefinitionDto> CreateDefinitionAsync(Guid academicYearId, [FromBody] CreateAdmissionCrmRouteDefinitionDto input) => definitionService.CreateDefinitionAsync(academicYearId, input);
	[HttpGet("{academicYearId}/admission-routes")]
	public Task<IReadOnlyList<AdmissionCrmRouteDefinitionDto>> GetDefinitionsAsync(Guid academicYearId) => definitionService.GetDefinitionsAsync(academicYearId);
	[HttpPost("admission-routes/{id}/submit")]
	public Task<AdmissionCrmRouteDefinitionDto> SubmitDefinitionAsync(Guid id) => definitionService.SubmitDefinitionAsync(id);
	[HttpPost("admission-routes/{id}/publish")]
	public Task<AdmissionCrmRouteDefinitionDto> PublishDefinitionAsync(Guid id) => definitionService.PublishDefinitionAsync(id);
	[HttpPost("leads/{leadId}/admission-route")]
	public Task<AdmissionCrmRouteAssignmentDto> AssignAsync(Guid leadId, [FromBody] AssignAdmissionCrmSpecialRouteDto input) => assignmentService.AssignAsync(leadId, input);
	[HttpPost("leads/{leadId}/admission-route/preview")]
	public Task<AdmissionCrmRouteImpactPreviewDto> PreviewAssignmentAsync(Guid leadId, [FromBody] AssignAdmissionCrmSpecialRouteDto input) => assignmentService.PreviewAssignmentAsync(leadId, input);
	[HttpGet("leads/{leadId}/admission-route")]
	public Task<IReadOnlyList<AdmissionCrmRouteAssignmentDto>> GetHistoryAsync(Guid leadId) => assignmentService.GetHistoryAsync(leadId);
	[HttpPost("admission-route-assignments/{id}/evaluate")]
	public Task<AdmissionCrmRouteAssignmentDto> EvaluateAsync(Guid id, [FromBody] EvaluateAdmissionCrmSpecialRouteDto input) => assignmentService.EvaluateAsync(id, input);
	[HttpPost("{academicYearId}/rte-allotments/import")]
	public Task<AdmissionCrmRteAllotmentImportResultDto> ImportRteAllotmentsAsync(Guid academicYearId, [FromBody] ImportAdmissionCrmRteAllotmentsDto input) => rteAllotmentService.ImportRteAllotmentsAsync(academicYearId, input);
	[HttpPost("{academicYearId}/rte-allotments/preview")]
	public Task<AdmissionCrmRteAllotmentImportResultDto> PreviewRteAllotmentsAsync(Guid academicYearId, [FromBody] ImportAdmissionCrmRteAllotmentsDto input) => rteAllotmentService.PreviewRteAllotmentsAsync(academicYearId, input);
	[HttpPost("admission-route-assignments/{assignmentId}/appeals")]
	public Task<AdmissionCrmRouteAppealDto> RequestAppealAsync(Guid assignmentId, [FromBody] RequestAdmissionCrmRouteAppealDto input) => appealService.RequestAppealAsync(assignmentId, input);
	[HttpPost("admission-route-appeals/{appealId}/decision")]
	public Task<AdmissionCrmRouteAppealDto> DecideAppealAsync(Guid appealId, [FromBody] DecideAdmissionCrmRouteAppealDto input) => appealService.DecideAppealAsync(appealId, input);
	[HttpGet("leads/{leadId}/admission-route-appeals")]
	public Task<IReadOnlyList<AdmissionCrmRouteAppealDto>> GetAppealsAsync(Guid leadId) => appealService.GetAppealsAsync(leadId);
}
