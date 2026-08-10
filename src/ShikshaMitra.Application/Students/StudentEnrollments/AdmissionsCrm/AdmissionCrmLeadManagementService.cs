using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
public class AdmissionCrmLeadManagementService(
	AdmissionCrmLeadAssignmentWorkflow assignment,
	AdmissionCrmLeadLifecycleWorkflow lifecycle) : ApplicationService, IAdmissionCrmLeadManagementService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignOwner)]
	public Task<AdmissionCrmLeadDto> AssignOwnerAsync(Guid leadId, AssignAdmissionCrmLeadOwnerDto input) => assignment.AssignOwnerAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.SetPriority)]
	public Task<AdmissionCrmLeadDto> ChangePriorityAsync(Guid leadId, ChangeAdmissionCrmLeadPriorityDto input) => assignment.ChangePriorityAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm)]
	public Task<AdmissionCrmLeadDto> IssueRegistrationFormAsync(Guid leadId, IssueAdmissionCrmRegistrationFormDto input) => assignment.IssueRegistrationFormAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeStage)]
	public Task<AdmissionCrmLeadDto> ChangeStageAsync(Guid leadId, ChangeAdmissionCrmLeadStageDto input) => assignment.ChangeStageAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmLeadDto> RecalculateScoreAsync(Guid leadId) => lifecycle.RecalculateScoreAsync(leadId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.MergeDuplicates)]
	public Task<AdmissionCrmLeadDto> MergeDuplicateAsync(Guid targetLeadId, MergeAdmissionCrmDuplicateLeadDto input) => lifecycle.MergeDuplicateAsync(targetLeadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ArchiveReopen)]
	public Task<AdmissionCrmLeadDto> ArchiveAsync(Guid leadId, ArchiveAdmissionCrmLeadDto input) => lifecycle.ArchiveAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ArchiveReopen)]
	public Task<AdmissionCrmLeadDto> ReopenAsync(Guid leadId, ReopenAdmissionCrmLeadDto input) => lifecycle.ReopenAsync(leadId, input);
}