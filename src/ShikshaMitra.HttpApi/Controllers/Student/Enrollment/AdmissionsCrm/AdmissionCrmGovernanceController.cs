using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmGovernanceController(IAdmissionCrmGovernanceAppService appService) : AbpControllerBase
{
	[HttpGet("{academicYearId}/governance/retention-policies")]
	public Task<IReadOnlyList<AdmissionCrmRetentionPolicyDto>> GetRetentionPoliciesAsync(Guid academicYearId) => appService.GetRetentionPoliciesAsync(academicYearId);

	[HttpPost("{academicYearId}/governance/retention-policies")]
	public Task<AdmissionCrmRetentionPolicyDto> CreateRetentionPolicyAsync(Guid academicYearId, [FromBody] CreateAdmissionCrmRetentionPolicyDto input) => appService.CreateRetentionPolicyAsync(academicYearId, input);

	[HttpPost("governance/retention-policies/{id}/submit")]
	public Task<AdmissionCrmRetentionPolicyDto> SubmitRetentionPolicyAsync(Guid id) => appService.SubmitRetentionPolicyAsync(id);

	[HttpPost("governance/retention-policies/{id}/publish")]
	public Task<AdmissionCrmRetentionPolicyDto> PublishRetentionPolicyAsync(Guid id) => appService.PublishRetentionPolicyAsync(id);

	[HttpGet("leads/{leadId}/governance/privacy/preview")]
	public Task<AdmissionCrmPrivacyPreviewDto> PreviewPrivacyRequestAsync(Guid leadId, [FromQuery] AdmissionCrmPrivacyRequestType type) => appService.PreviewPrivacyRequestAsync(leadId, type);

	[HttpPost("leads/{leadId}/governance/privacy-requests")]
	public Task<AdmissionCrmPrivacyRequestDto> CreatePrivacyRequestAsync(Guid leadId, [FromBody] CreateAdmissionCrmPrivacyRequestDto input) => appService.CreatePrivacyRequestAsync(leadId, input);

	[HttpGet("{academicYearId}/governance/privacy-requests")]
	public Task<IReadOnlyList<AdmissionCrmPrivacyRequestDto>> GetPrivacyRequestsAsync(Guid academicYearId) => appService.GetPrivacyRequestsAsync(academicYearId);

	[HttpPost("governance/privacy-requests/{id}/decision")]
	public Task<AdmissionCrmPrivacyRequestDto> DecidePrivacyRequestAsync(Guid id, [FromBody] DecideAdmissionCrmPrivacyRequestDto input) => appService.DecidePrivacyRequestAsync(id, input);

	[HttpPost("governance/privacy-requests/{id}/complete")]
	public Task<AdmissionCrmPrivacyRequestDto> CompletePrivacyRequestAsync(Guid id, [FromBody] CompleteAdmissionCrmPrivacyRequestDto input) => appService.CompletePrivacyRequestAsync(id, input);

	[HttpPost("{academicYearId}/governance/exports")]
	public Task<AdmissionCrmExportRequestDto> RequestExportAsync(Guid academicYearId, [FromBody] RequestAdmissionCrmExportDto input) => appService.RequestExportAsync(academicYearId, input);

	[HttpGet("{academicYearId}/governance/exports")]
	public Task<IReadOnlyList<AdmissionCrmExportRequestDto>> GetExportsAsync(Guid academicYearId) => appService.GetExportsAsync(academicYearId);

	[HttpGet("governance/exports/{id}/download")]
	public Task<AdmissionCrmExportDownloadDto> DownloadExportAsync(Guid id) => appService.DownloadExportAsync(id);

	[HttpPost("{academicYearId}/governance/report-schedules")]
	public Task<AdmissionCrmReportScheduleDto> CreateReportScheduleAsync(Guid academicYearId, [FromBody] CreateAdmissionCrmReportScheduleDto input) => appService.CreateReportScheduleAsync(academicYearId, input);

	[HttpGet("{academicYearId}/governance/report-schedules")]
	public Task<IReadOnlyList<AdmissionCrmReportScheduleDto>> GetReportSchedulesAsync(Guid academicYearId) => appService.GetReportSchedulesAsync(academicYearId);

	[HttpPut("governance/report-schedules/{id}/active")]
	public Task<AdmissionCrmReportScheduleDto> SetReportScheduleActiveAsync(Guid id, [FromQuery] bool active) => appService.SetReportScheduleActiveAsync(id, active);

	[HttpPost("{academicYearId}/governance/report-schedules/run-due")]
	public Task<AdmissionCrmReportScheduleRunResultDto> RunDueReportSchedulesAsync(Guid academicYearId) => appService.RunDueReportSchedulesAsync(academicYearId);

	[HttpPost("{academicYearId}/operations/delegations")]
	public Task<AdmissionCrmWorkDelegationDto> CreateDelegationAsync(Guid academicYearId, [FromBody] CreateAdmissionCrmWorkDelegationDto input) => appService.CreateDelegationAsync(academicYearId, input);

	[HttpGet("{academicYearId}/operations/delegations")]
	public Task<IReadOnlyList<AdmissionCrmWorkDelegationDto>> GetDelegationsAsync(Guid academicYearId) => appService.GetDelegationsAsync(academicYearId);

	[HttpPost("operations/delegations/{id}/cancel")]
	public Task<AdmissionCrmWorkDelegationDto> CancelDelegationAsync(Guid id) => appService.CancelDelegationAsync(id);

	[HttpGet("governance/audit")]
	public Task<PagedResultDto<AdmissionCrmTimelineEventDto>> SearchAuditAsync([FromQuery] GetAdmissionCrmAuditInput input) => appService.SearchAuditAsync(input);

	[HttpGet("{academicYearId}/analytics/advisory-forecast")]
	public Task<AdmissionCrmForecastDto> GetAdvisoryForecastAsync(Guid academicYearId) => appService.GetAdvisoryForecastAsync(academicYearId);
}
