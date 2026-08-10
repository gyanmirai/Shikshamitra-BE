using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmLeadController(
	IAdmissionCrmLeadCaptureService captureService,
	IAdmissionCrmLeadQueryService queryService,
	IAdmissionCrmLeadManagementService managementService,
	IAdmissionCrmLeadAutomationService automationService,
	IAdmissionCrmLeadWorkService workService) : AbpControllerBase
{
	[HttpGet("{academicYearId}/quick-inquiry-options")]
	public Task<AdmissionCrmQuickInquiryOptionsDto> GetQuickInquiryOptionsAsync(
		[FromRoute] Guid academicYearId) =>
		captureService.GetQuickInquiryOptionsAsync(academicYearId);

	[HttpGet("{academicYearId}/operational-standard-options")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add)]
	public Task<IReadOnlyList<AdmissionCrmAdmissionYearStandardOptionDto>>
		GetOperationalStandardOptionsAsync([FromRoute] Guid academicYearId) =>
		captureService.GetOperationalStandardOptionsAsync(academicYearId);

	[HttpPost("{academicYearId}/leads")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add)]
	public async Task<AdmissionCrmLeadDto> CreateAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] CreateAdmissionCrmLeadDto input)
	{
		return await captureService.CreateAsync(academicYearId, input);
	}

	[HttpGet("{academicYearId}/leads")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<PagedResultDto<AdmissionCrmLeadDto>> GetListAsync(
		[FromRoute] Guid academicYearId,
		[FromQuery] GetAdmissionCrmLeadListInput input)
	{
		return await queryService.GetListAsync(academicYearId, input);
	}

	[HttpGet("leads/{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmLeadDto> GetAsync([FromRoute] Guid id)
	{
		return await queryService.GetAsync(id);
	}

	[HttpPost("{academicYearId}/leads/duplicate-preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<IReadOnlyList<AdmissionCrmLeadDuplicateDto>> PreviewDuplicatesAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] AdmissionCrmLeadDuplicatePreviewInput input)
	{
		return await queryService.PreviewDuplicatesAsync(academicYearId, input);
	}

	[HttpPost("leads/{leadId}/owner")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignOwner)]
	public async Task<AdmissionCrmLeadDto> AssignOwnerAsync(
		[FromRoute] Guid leadId,
		[FromBody] AssignAdmissionCrmLeadOwnerDto input)
	{
		return await managementService.AssignOwnerAsync(leadId, input);
	}

	[HttpPost("leads/{leadId}/priority")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.SetPriority)]
	public async Task<AdmissionCrmLeadDto> ChangePriorityAsync(
		[FromRoute] Guid leadId,
		[FromBody] ChangeAdmissionCrmLeadPriorityDto input)
	{
		return await managementService.ChangePriorityAsync(leadId, input);
	}

	[HttpPost("leads/{leadId}/registration-form")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm)]
	public async Task<AdmissionCrmLeadDto> IssueRegistrationFormAsync(
		[FromRoute] Guid leadId,
		[FromBody] IssueAdmissionCrmRegistrationFormDto input)
	{
		return await managementService.IssueRegistrationFormAsync(leadId, input);
	}

	[HttpPost("leads/{leadId}/stage")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeStage)]
	public async Task<AdmissionCrmLeadDto> ChangeStageAsync(
		[FromRoute] Guid leadId,
		[FromBody] ChangeAdmissionCrmLeadStageDto input)
	{
		return await managementService.ChangeStageAsync(leadId, input);
	}

	[HttpPost("leads/{leadId}/score/recalculate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmLeadDto> RecalculateScoreAsync([FromRoute] Guid leadId)
	{
		return await managementService.RecalculateScoreAsync(leadId);
	}

	[HttpPost("leads/{leadId}/duplicates/merge")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.MergeDuplicates)]
	public async Task<AdmissionCrmLeadDto> MergeDuplicateAsync(
		[FromRoute] Guid leadId,
		[FromBody] MergeAdmissionCrmDuplicateLeadDto input)
	{
		return await managementService.MergeDuplicateAsync(leadId, input);
	}

	[HttpPost("leads/{leadId}/archive")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ArchiveReopen)]
	public async Task<AdmissionCrmLeadDto> ArchiveAsync(
		[FromRoute] Guid leadId,
		[FromBody] ArchiveAdmissionCrmLeadDto input)
	{
		return await managementService.ArchiveAsync(leadId, input);
	}

	[HttpPost("leads/{leadId}/reopen")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ArchiveReopen)]
	public async Task<AdmissionCrmLeadDto> ReopenAsync(
		[FromRoute] Guid leadId,
		[FromBody] ReopenAdmissionCrmLeadDto input)
	{
		return await managementService.ReopenAsync(leadId, input);
	}

	[HttpPost("{academicYearId}/automation/stale-archive/run")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageAutomation)]
	public async Task<AdmissionCrmStaleArchiveRunResultDto> RunStaleArchiveRulesAsync(
		[FromRoute] Guid academicYearId)
	{
		return await automationService.RunStaleArchiveRulesAsync(academicYearId);
	}

	[HttpPost("{academicYearId}/automation/sla-reminders/run")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageAutomation)]
	public async Task<AdmissionCrmSlaReminderRunResultDto> RunSlaReminderRulesAsync(
		[FromRoute] Guid academicYearId)
	{
		return await automationService.RunSlaReminderRulesAsync(academicYearId);
	}

	[HttpGet("leads/{leadId}/sla-holds")]
	public Task<IReadOnlyList<AdmissionCrmSlaHoldDto>> GetSlaHoldsAsync(Guid leadId) =>
		workService.GetSlaHoldsAsync(leadId);

	[HttpPost("leads/{leadId}/sla-holds")]
	public Task<AdmissionCrmSlaHoldDto> CreateSlaHoldAsync(Guid leadId, [FromBody] CreateAdmissionCrmSlaHoldDto input) =>
		workService.CreateSlaHoldAsync(leadId, input);

	[HttpPost("sla-holds/{holdId}/resume")]
	public Task<AdmissionCrmSlaHoldDto> ResumeSlaHoldAsync(Guid holdId, [FromBody] ResumeAdmissionCrmSlaHoldDto input) =>
		workService.ResumeSlaHoldAsync(holdId, input);

	[HttpPost("{academicYearId}/automation/journeys/run")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageAutomation)]
	public async Task<AdmissionCrmJourneyRunResultDto> RunJourneyRulesAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] RunAdmissionCrmJourneyRulesDto input)
	{
		return await automationService.RunJourneyRulesAsync(academicYearId, input);
	}

	[HttpPost("leads/{leadId}/tasks")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTasks)]
	public async Task<AdmissionCrmFollowUpTaskDto> CreateTaskAsync(
		[FromRoute] Guid leadId,
		[FromBody] CreateAdmissionCrmFollowUpTaskDto input)
	{
		return await workService.CreateTaskAsync(leadId, input);
	}

	[HttpGet("leads/{leadId}/tasks")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<PagedResultDto<AdmissionCrmFollowUpTaskDto>> GetTasksAsync(
		[FromRoute] Guid leadId,
		[FromQuery] PagedAndSortedResultRequestDto input)
	{
		return await workService.GetTasksAsync(leadId, input);
	}

	[HttpGet("leads/{leadId}/applicant-checklist")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<IReadOnlyList<AdmissionCrmApplicantChecklistProgressDto>> GetApplicantChecklistAsync(
		[FromRoute] Guid leadId)
	{
		return await workService.GetApplicantChecklistAsync(leadId);
	}

	[HttpPut("leads/{leadId}/applicant-checklist/{key}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTasks)]
	public async Task<AdmissionCrmApplicantChecklistProgressDto> UpdateApplicantChecklistAsync(
		[FromRoute] Guid leadId,
		[FromRoute] string key,
		[FromBody] UpdateAdmissionCrmApplicantChecklistProgressDto input)
	{
		return await workService.UpdateApplicantChecklistAsync(leadId, key, input);
	}

	[HttpGet("leads/{leadId}/timeline")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<PagedResultDto<AdmissionCrmTimelineEventDto>> GetTimelineAsync(
		[FromRoute] Guid leadId,
		[FromQuery] PagedAndSortedResultRequestDto input)
	{
		return await workService.GetTimelineAsync(leadId, input);
	}

	[HttpPost("leads/{leadId}/notes")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTasks)]
	public async Task<AdmissionCrmTimelineEventDto> AddNoteAsync(
		[FromRoute] Guid leadId,
		[FromBody] CreateAdmissionCrmLeadNoteDto input)
	{
		return await workService.AddNoteAsync(leadId, input);
	}

	[HttpPost("tasks/{taskId}/complete")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTasks)]
	public async Task<AdmissionCrmFollowUpTaskDto> CompleteTaskAsync(
		[FromRoute] Guid taskId,
		[FromBody] CompleteAdmissionCrmFollowUpTaskDto input)
	{
		return await workService.CompleteTaskAsync(taskId, input);
	}
}