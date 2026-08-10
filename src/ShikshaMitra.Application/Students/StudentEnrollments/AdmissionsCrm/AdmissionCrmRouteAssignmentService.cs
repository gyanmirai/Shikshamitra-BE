using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmRouteAssignmentService(
	AdmissionCrmRouteAssignmentWorkflow workflow,
	AdmissionCrmRouteImpactReader impactReader) : ApplicationService, IAdmissionCrmRouteAssignmentService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignAdmissionRoute)]
	public Task<AdmissionCrmRouteAssignmentDto> AssignAsync(
		Guid leadId,
		AssignAdmissionCrmSpecialRouteDto input) => workflow.AssignAsync(leadId, input, nameof(AssignAsync));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignAdmissionRoute)]
	public Task<AdmissionCrmRouteImpactPreviewDto> PreviewAssignmentAsync(
		Guid leadId,
		AssignAdmissionCrmSpecialRouteDto input) => impactReader.GetAsync(leadId, input, nameof(PreviewAssignmentAsync));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewAdmissionRoutes)]
	public Task<IReadOnlyList<AdmissionCrmRouteAssignmentDto>> GetHistoryAsync(Guid leadId) =>
		workflow.GetHistoryAsync(leadId, nameof(GetHistoryAsync));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.EvaluateAdmissionRoute)]
	public Task<AdmissionCrmRouteAssignmentDto> EvaluateAsync(
		Guid id,
		EvaluateAdmissionCrmSpecialRouteDto input) => workflow.EvaluateAsync(id, input, nameof(EvaluateAsync));
}
