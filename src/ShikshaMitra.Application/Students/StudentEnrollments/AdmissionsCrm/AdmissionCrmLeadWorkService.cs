using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
public class AdmissionCrmLeadWorkService(
	AdmissionCrmLeadWorkWorkflow work,
	AdmissionCrmLeadSlaHoldWorkflow slaHolds) : ApplicationService, IAdmissionCrmLeadWorkService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTasks)]
	public Task<AdmissionCrmFollowUpTaskDto> CreateTaskAsync(Guid leadId, CreateAdmissionCrmFollowUpTaskDto input) => work.CreateTaskAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<PagedResultDto<AdmissionCrmFollowUpTaskDto>> GetTasksAsync(Guid leadId, PagedAndSortedResultRequestDto input) => work.GetTasksAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<IReadOnlyList<AdmissionCrmApplicantChecklistProgressDto>> GetApplicantChecklistAsync(Guid leadId) => work.GetApplicantChecklistAsync(leadId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTasks)]
	public Task<AdmissionCrmApplicantChecklistProgressDto> UpdateApplicantChecklistAsync(Guid leadId, string key, UpdateAdmissionCrmApplicantChecklistProgressDto input) => work.UpdateApplicantChecklistAsync(leadId, key, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTasks)]
	public Task<AdmissionCrmTimelineEventDto> AddNoteAsync(Guid leadId, CreateAdmissionCrmLeadNoteDto input) => work.AddNoteAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOperationsQueues)]
	public Task<AdmissionCrmSlaHoldDto> CreateSlaHoldAsync(Guid leadId, CreateAdmissionCrmSlaHoldDto input) => slaHolds.CreateSlaHoldAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOperationsQueues)]
	public Task<AdmissionCrmSlaHoldDto> ResumeSlaHoldAsync(Guid holdId, ResumeAdmissionCrmSlaHoldDto input) => slaHolds.ResumeSlaHoldAsync(holdId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<IReadOnlyList<AdmissionCrmSlaHoldDto>> GetSlaHoldsAsync(Guid leadId) => slaHolds.GetSlaHoldsAsync(leadId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTasks)]
	public Task<AdmissionCrmFollowUpTaskDto> CompleteTaskAsync(Guid taskId, CompleteAdmissionCrmFollowUpTaskDto input) => work.CompleteTaskAsync(taskId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<PagedResultDto<AdmissionCrmTimelineEventDto>> GetTimelineAsync(Guid leadId, PagedAndSortedResultRequestDto input) => work.GetTimelineAsync(leadId, input);
}