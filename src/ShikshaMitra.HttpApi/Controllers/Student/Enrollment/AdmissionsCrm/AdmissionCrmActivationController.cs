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
public class AdmissionCrmActivationController(IAdmissionCrmActivationAppService appService) : AbpControllerBase
{
	[HttpPost("{academicYearId}/activation/number-series")]
	public Task<AdmissionCrmNumberSeriesDto> CreateNumberSeriesAsync(Guid academicYearId, [FromBody] CreateAdmissionCrmNumberSeriesDto input) => appService.CreateNumberSeriesAsync(academicYearId, input);
	[HttpGet("{academicYearId}/activation/number-series")]
	public Task<IReadOnlyList<AdmissionCrmNumberSeriesDto>> GetNumberSeriesAsync(Guid academicYearId) => appService.GetNumberSeriesAsync(academicYearId);
	[HttpGet("{academicYearId}/activation/placement-options")]
	public Task<IReadOnlyList<AdmissionCrmPlacementOptionDto>> GetPlacementOptionsAsync(Guid academicYearId) => appService.GetPlacementOptionsAsync(academicYearId);
	[HttpPost("activation/number-series/{id}/submit")]
	public Task<AdmissionCrmNumberSeriesDto> SubmitNumberSeriesAsync(Guid id) => appService.SubmitNumberSeriesAsync(id);
	[HttpPost("activation/number-series/{id}/publish")]
	public Task<AdmissionCrmNumberSeriesDto> PublishNumberSeriesAsync(Guid id) => appService.PublishNumberSeriesAsync(id);
	[HttpGet("{academicYearId}/activation/number-series/{numberSeriesId}/governance")]
	public Task<AdmissionCrmIdentifierGovernanceReportDto> GetIdentifierGovernanceReportAsync(Guid academicYearId, Guid numberSeriesId) =>
		appService.GetIdentifierGovernanceReportAsync(academicYearId, numberSeriesId);
	[HttpPost("leads/{leadId}/activation/preview")]
	public Task<AdmissionCrmActivationPreviewDto> PreviewAsync(Guid leadId, [FromBody] PreviewAdmissionCrmActivationDto input) => appService.PreviewAsync(leadId, input);
	[HttpPost("leads/{leadId}/activation")]
	public Task<AdmissionCrmActivationDto> RequestAsync(Guid leadId, [FromBody] RequestAdmissionCrmActivationDto input) => appService.RequestAsync(leadId, input);
	[HttpPost("activation/{id}/approve")]
	public Task<AdmissionCrmActivationDto> ApproveAsync(Guid id) => appService.ApproveAsync(id);
	[HttpPost("activation/{id}/execute")]
	public Task<AdmissionCrmActivationDto> ExecuteAsync(Guid id) => appService.ExecuteAsync(id);
	[HttpPost("activation/{activationId}/manual-identifier")]
	public Task<AdmissionCrmIdentifierAllocationDto> AssignManualIdentifierAsync(
		Guid activationId, [FromBody] AssignManualAdmissionCrmIdentifierDto input) =>
		appService.AssignManualIdentifierAsync(activationId, input);
	[HttpGet("activation/{id}/cancellation-preview")]
	public Task<AdmissionCrmActivationCancellationPreviewDto> PreviewCancellationAsync(Guid id) => appService.PreviewCancellationAsync(id);
	[HttpPost("activation/{id}/request-cancellation")]
	public Task<AdmissionCrmActivationDto> RequestCancellationAsync(Guid id, [FromBody] RequestAdmissionCrmActivationCancellationDto input) =>
		appService.RequestCancellationAsync(id, input);
	[HttpPost("activation/{id}/approve-cancellation")]
	public Task<AdmissionCrmActivationDto> ApproveCancellationAsync(Guid id) => appService.ApproveCancellationAsync(id);
	[HttpGet("leads/{leadId}/activation")]
	public Task<AdmissionCrmActivationDto?> GetByLeadAsync(Guid leadId) => appService.GetByLeadAsync(leadId);
	[HttpGet("activation/{activationId}/service-handoffs")]
	public Task<IReadOnlyList<AdmissionCrmServiceHandoffDto>> GetServiceHandoffsAsync(Guid activationId) =>
		appService.GetServiceHandoffsAsync(activationId);
	[HttpPut("activation/service-handoffs/{id}")]
	public Task<AdmissionCrmServiceHandoffDto> UpdateServiceHandoffAsync(
		Guid id, [FromBody] UpdateAdmissionCrmServiceHandoffDto input) => appService.UpdateServiceHandoffAsync(id, input);
}
