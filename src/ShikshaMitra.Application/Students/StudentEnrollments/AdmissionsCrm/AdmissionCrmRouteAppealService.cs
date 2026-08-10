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
public class AdmissionCrmRouteAppealService(
	AdmissionCrmRouteAppealWorkflow workflow) : ApplicationService, IAdmissionCrmRouteAppealService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRouteAppeals)]
	public Task<AdmissionCrmRouteAppealDto> RequestAppealAsync(
		Guid assignmentId,
		RequestAdmissionCrmRouteAppealDto input) => workflow.RequestAsync(assignmentId, input, nameof(RequestAppealAsync));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveRouteAppeals)]
	public Task<AdmissionCrmRouteAppealDto> DecideAppealAsync(
		Guid appealId,
		DecideAdmissionCrmRouteAppealDto input) => workflow.DecideAsync(appealId, input, nameof(DecideAppealAsync));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewAdmissionRoutes)]
	public Task<IReadOnlyList<AdmissionCrmRouteAppealDto>> GetAppealsAsync(Guid leadId) =>
		workflow.GetListAsync(leadId, nameof(GetAppealsAsync));
}
