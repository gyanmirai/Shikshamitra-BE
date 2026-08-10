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
public class AdmissionCrmRouteDefinitionService(
	AdmissionCrmRouteDefinitionWorkflow workflow) : ApplicationService, IAdmissionCrmRouteDefinitionService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageAdmissionRoutes)]
	public Task<AdmissionCrmRouteDefinitionDto> CreateDefinitionAsync(
		Guid academicYearId,
		CreateAdmissionCrmRouteDefinitionDto input) => workflow.CreateAsync(academicYearId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewAdmissionRoutes)]
	public Task<IReadOnlyList<AdmissionCrmRouteDefinitionDto>> GetDefinitionsAsync(Guid academicYearId) =>
		workflow.GetListAsync(academicYearId);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageAdmissionRoutes)]
	public Task<AdmissionCrmRouteDefinitionDto> SubmitDefinitionAsync(Guid id) => workflow.SubmitAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveAdmissionRoutes)]
	public Task<AdmissionCrmRouteDefinitionDto> PublishDefinitionAsync(Guid id) => workflow.PublishAsync(id);
}
