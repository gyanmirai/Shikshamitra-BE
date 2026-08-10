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
public class AdmissionCrmGeneratedDocumentQueryService(
	AdmissionCrmGeneratedDocumentQueryWorkflow workflow)
	: ApplicationService, IAdmissionCrmGeneratedDocumentQueryService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<IReadOnlyList<AdmissionCrmGeneratedDocumentAvailabilityDto>> GetAvailabilityAsync(Guid leadId) =>
		workflow.GetAvailabilityAsync(leadId);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewAudit)]
	public Task<IReadOnlyList<AdmissionCrmGeneratedDocumentHistoryDto>> GetHistoryAsync(Guid leadId) =>
		workflow.GetHistoryAsync(leadId);
}
