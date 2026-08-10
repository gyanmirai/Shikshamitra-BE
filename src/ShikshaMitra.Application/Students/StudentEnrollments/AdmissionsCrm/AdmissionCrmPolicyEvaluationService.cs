using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmPolicyEvaluationService(
	AdmissionCrmPolicyEvaluationWorkflow workflow) : ApplicationService, IAdmissionCrmPolicyEvaluationService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.EvaluatePolicy)]
	public Task<AdmissionCrmPolicyEvaluationDto> EvaluateRuleAsync(
		Guid leadId,
		EvaluateAdmissionCrmPolicyRuleDto input) => workflow.EvaluateAsync(leadId, input, nameof(EvaluateRuleAsync));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewPolicyPacks)]
	public Task<AdmissionCrmPolicyReadinessDto> GetPolicyReadinessAsync(Guid leadId) =>
		workflow.GetReadinessAsync(leadId, nameof(GetPolicyReadinessAsync));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.RequestPolicyException)]
	public Task<AdmissionCrmPolicyExceptionDto> RequestExceptionAsync(
		Guid evaluationId,
		RequestAdmissionCrmPolicyExceptionDto input) =>
		workflow.RequestExceptionAsync(evaluationId, input, nameof(RequestExceptionAsync));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApprovePolicyException)]
	public Task<AdmissionCrmPolicyExceptionDto> DecideExceptionAsync(
		Guid id,
		DecideAdmissionCrmPolicyExceptionDto input) => workflow.DecideExceptionAsync(id, input, nameof(DecideExceptionAsync));
}
