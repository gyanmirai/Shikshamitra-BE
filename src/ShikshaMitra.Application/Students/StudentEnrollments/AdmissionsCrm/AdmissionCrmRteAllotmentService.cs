using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmRteAllotmentService(
	AdmissionCrmRteAllotmentWorkflow workflow) : ApplicationService, IAdmissionCrmRteAllotmentService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageGovernmentReadiness)]
	public Task<AdmissionCrmRteAllotmentImportResultDto> PreviewRteAllotmentsAsync(
		Guid academicYearId,
		ImportAdmissionCrmRteAllotmentsDto input) => workflow.PreviewAsync(academicYearId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageGovernmentReadiness)]
	public Task<AdmissionCrmRteAllotmentImportResultDto> ImportRteAllotmentsAsync(
		Guid academicYearId,
		ImportAdmissionCrmRteAllotmentsDto input) => workflow.ImportAsync(academicYearId, input);
}
