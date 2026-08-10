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
public class AdmissionCrmActivationController(
	IAdmissionCrmNumberSeriesService numberSeriesService,
	IAdmissionCrmIdentifierService identifierService,
	IAdmissionCrmActivationLifecycleService lifecycleService,
	IAdmissionCrmActivationExecutionService executionService,
	IAdmissionCrmActivationCancellationService cancellationService,
	IAdmissionCrmServiceHandoffService handoffService) : AbpControllerBase
{
	[HttpPost("{academicYearId}/activation/number-series")]
	public Task<AdmissionCrmNumberSeriesDto> CreateNumberSeriesAsync(Guid academicYearId, [FromBody] CreateAdmissionCrmNumberSeriesDto input) => numberSeriesService.CreateNumberSeriesAsync(academicYearId, input);
	[HttpGet("{academicYearId}/activation/number-series")]
	public Task<IReadOnlyList<AdmissionCrmNumberSeriesDto>> GetNumberSeriesAsync(Guid academicYearId) => numberSeriesService.GetNumberSeriesAsync(academicYearId);
	[HttpGet("{academicYearId}/activation/placement-options")]
	public Task<IReadOnlyList<AdmissionCrmPlacementOptionDto>> GetPlacementOptionsAsync(Guid academicYearId) => numberSeriesService.GetPlacementOptionsAsync(academicYearId);
	[HttpPost("activation/number-series/{id}/submit")]
	public Task<AdmissionCrmNumberSeriesDto> SubmitNumberSeriesAsync(Guid id) => numberSeriesService.SubmitNumberSeriesAsync(id);
	[HttpPost("activation/number-series/{id}/publish")]
	public Task<AdmissionCrmNumberSeriesDto> PublishNumberSeriesAsync(Guid id) => numberSeriesService.PublishNumberSeriesAsync(id);
	[HttpGet("{academicYearId}/activation/number-series/{numberSeriesId}/governance")]
	public Task<AdmissionCrmIdentifierGovernanceReportDto> GetIdentifierGovernanceReportAsync(Guid academicYearId, Guid numberSeriesId) =>
		identifierService.GetIdentifierGovernanceReportAsync(academicYearId, numberSeriesId);
	[HttpPost("leads/{leadId}/activation/preview")]
	public Task<AdmissionCrmActivationPreviewDto> PreviewAsync(Guid leadId, [FromBody] PreviewAdmissionCrmActivationDto input) => lifecycleService.PreviewAsync(leadId, input);
	[HttpPost("leads/{leadId}/activation")]
	public Task<AdmissionCrmActivationDto> RequestAsync(Guid leadId, [FromBody] RequestAdmissionCrmActivationDto input) => lifecycleService.RequestAsync(leadId, input);
	[HttpPost("activation/{id}/approve")]
	public Task<AdmissionCrmActivationDto> ApproveAsync(Guid id) => lifecycleService.ApproveAsync(id);
	[HttpPost("activation/{id}/execute")]
	public Task<AdmissionCrmActivationDto> ExecuteAsync(Guid id) => executionService.ExecuteAsync(id);
	[HttpPost("activation/{activationId}/manual-identifier")]
	public Task<AdmissionCrmIdentifierAllocationDto> AssignManualIdentifierAsync(
		Guid activationId, [FromBody] AssignManualAdmissionCrmIdentifierDto input) =>
		identifierService.AssignManualIdentifierAsync(activationId, input);
	[HttpGet("activation/{id}/cancellation-preview")]
	public Task<AdmissionCrmActivationCancellationPreviewDto> PreviewCancellationAsync(Guid id) => cancellationService.PreviewCancellationAsync(id);
	[HttpPost("activation/{id}/request-cancellation")]
	public Task<AdmissionCrmActivationDto> RequestCancellationAsync(Guid id, [FromBody] RequestAdmissionCrmActivationCancellationDto input) =>
		cancellationService.RequestCancellationAsync(id, input);
	[HttpPost("activation/{id}/approve-cancellation")]
	public Task<AdmissionCrmActivationDto> ApproveCancellationAsync(Guid id) => cancellationService.ApproveCancellationAsync(id);
	[HttpGet("leads/{leadId}/activation")]
	public Task<AdmissionCrmActivationDto?> GetByLeadAsync(Guid leadId) => lifecycleService.GetByLeadAsync(leadId);
	[HttpGet("activation/{activationId}/service-handoffs")]
	public Task<IReadOnlyList<AdmissionCrmServiceHandoffDto>> GetServiceHandoffsAsync(Guid activationId) =>
		handoffService.GetServiceHandoffsAsync(activationId);
	[HttpPut("activation/service-handoffs/{id}")]
	public Task<AdmissionCrmServiceHandoffDto> UpdateServiceHandoffAsync(
		Guid id, [FromBody] UpdateAdmissionCrmServiceHandoffDto input) => handoffService.UpdateServiceHandoffAsync(id, input);
}
