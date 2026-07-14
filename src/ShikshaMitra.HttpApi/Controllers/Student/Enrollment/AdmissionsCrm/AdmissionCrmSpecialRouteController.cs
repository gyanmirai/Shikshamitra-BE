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
public class AdmissionCrmSpecialRouteController(IAdmissionCrmSpecialRouteAppService appService) : AbpControllerBase
{
	[HttpPost("{academicYearId}/admission-routes")]
	public Task<AdmissionCrmRouteDefinitionDto> CreateDefinitionAsync(Guid academicYearId, [FromBody] CreateAdmissionCrmRouteDefinitionDto input) => appService.CreateDefinitionAsync(academicYearId, input);
	[HttpGet("{academicYearId}/admission-routes")]
	public Task<IReadOnlyList<AdmissionCrmRouteDefinitionDto>> GetDefinitionsAsync(Guid academicYearId) => appService.GetDefinitionsAsync(academicYearId);
	[HttpPost("admission-routes/{id}/submit")]
	public Task<AdmissionCrmRouteDefinitionDto> SubmitDefinitionAsync(Guid id) => appService.SubmitDefinitionAsync(id);
	[HttpPost("admission-routes/{id}/publish")]
	public Task<AdmissionCrmRouteDefinitionDto> PublishDefinitionAsync(Guid id) => appService.PublishDefinitionAsync(id);
	[HttpPost("leads/{leadId}/admission-route")]
	public Task<AdmissionCrmRouteAssignmentDto> AssignAsync(Guid leadId, [FromBody] AssignAdmissionCrmSpecialRouteDto input) => appService.AssignAsync(leadId, input);
	[HttpPost("leads/{leadId}/admission-route/preview")]
	public Task<AdmissionCrmRouteImpactPreviewDto> PreviewAssignmentAsync(Guid leadId, [FromBody] AssignAdmissionCrmSpecialRouteDto input) => appService.PreviewAssignmentAsync(leadId, input);
	[HttpGet("leads/{leadId}/admission-route")]
	public Task<IReadOnlyList<AdmissionCrmRouteAssignmentDto>> GetHistoryAsync(Guid leadId) => appService.GetHistoryAsync(leadId);
	[HttpPost("admission-route-assignments/{id}/evaluate")]
	public Task<AdmissionCrmRouteAssignmentDto> EvaluateAsync(Guid id, [FromBody] EvaluateAdmissionCrmSpecialRouteDto input) => appService.EvaluateAsync(id, input);
	[HttpPost("{academicYearId}/rte-allotments/import")]
	public Task<AdmissionCrmRteAllotmentImportResultDto> ImportRteAllotmentsAsync(Guid academicYearId, [FromBody] ImportAdmissionCrmRteAllotmentsDto input) => appService.ImportRteAllotmentsAsync(academicYearId, input);
	[HttpPost("{academicYearId}/rte-allotments/preview")]
	public Task<AdmissionCrmRteAllotmentImportResultDto> PreviewRteAllotmentsAsync(Guid academicYearId, [FromBody] ImportAdmissionCrmRteAllotmentsDto input) => appService.PreviewRteAllotmentsAsync(academicYearId, input);
	[HttpPost("admission-route-assignments/{assignmentId}/appeals")]
	public Task<AdmissionCrmRouteAppealDto> RequestAppealAsync(Guid assignmentId, [FromBody] RequestAdmissionCrmRouteAppealDto input) => appService.RequestAppealAsync(assignmentId, input);
	[HttpPost("admission-route-appeals/{appealId}/decision")]
	public Task<AdmissionCrmRouteAppealDto> DecideAppealAsync(Guid appealId, [FromBody] DecideAdmissionCrmRouteAppealDto input) => appService.DecideAppealAsync(appealId, input);
	[HttpGet("leads/{leadId}/admission-route-appeals")]
	public Task<IReadOnlyList<AdmissionCrmRouteAppealDto>> GetAppealsAsync(Guid leadId) => appService.GetAppealsAsync(leadId);
}
