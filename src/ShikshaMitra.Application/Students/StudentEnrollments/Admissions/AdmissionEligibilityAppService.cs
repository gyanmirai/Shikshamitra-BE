using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionEligibility;
using WorkflowPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionEligibilityAppService(AdmissionEligibilityReadService reader, AdmissionEligibilityCommandService commands,
	AdmissionEligibilityConfigurationService configuration) : ApplicationService, IAdmissionEligibilityAppService
{
	[Authorize(Permissions.View)]
	public Task<AdmissionEligibilityDto> GetAsync(Guid applicationId, Guid stageId) => reader.GetAsync(applicationId, stageId);
	[Authorize(Permissions.View), Authorize(Permissions.Evaluate)]
	public Task<AdmissionEligibilityDto> EvaluateAsync(Guid applicationId, Guid stageId, AdmissionEligibilityEvaluateDto input) => commands.EvaluateAsync(applicationId, stageId, input);
	[Authorize(Permissions.View)]
	public Task<List<AdmissionEligibilityEvaluationDto>> GetHistoryAsync(Guid applicationId, Guid stageId) => reader.GetHistoryAsync(applicationId, stageId);
	[Authorize(Permissions.View), Authorize(Permissions.RequestException)]
	public Task<AdmissionEligibilityDto> RequestExceptionAsync(Guid applicationId, Guid stageId, Guid evaluationId, AdmissionEligibilityExceptionRequestDto input) => commands.RequestExceptionAsync(applicationId, stageId, evaluationId, input);
	[Authorize(Permissions.View), Authorize(Permissions.ReviewException)]
	public Task<AdmissionEligibilityDto> ReviewExceptionAsync(Guid applicationId, Guid stageId, Guid exceptionId, AdmissionEligibilityExceptionReviewDto input) => commands.ReviewExceptionAsync(applicationId, stageId, exceptionId, input);
	[Authorize(WorkflowPermissions.View)]
	public Task<List<AdmissionEligibilityRoleDto>> GetReviewerRolesAsync() => configuration.GetRolesAsync();
}
