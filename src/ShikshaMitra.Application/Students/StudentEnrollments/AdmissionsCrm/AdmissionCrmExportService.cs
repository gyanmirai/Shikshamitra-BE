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
public class AdmissionCrmExportService(AdmissionCrmExportWorkflow workflow) : ApplicationService, IAdmissionCrmExportService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.SensitiveExport)]
	public Task<AdmissionCrmExportRequestDto> RequestExportAsync(Guid academicYearId, RequestAdmissionCrmExportDto input) => workflow.RequestExportAsync(academicYearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.SensitiveExport)]
	public Task<IReadOnlyList<AdmissionCrmExportRequestDto>> GetExportsAsync(Guid academicYearId) => workflow.GetExportsAsync(academicYearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.SensitiveExport)]
	public Task<AdmissionCrmExportDownloadDto> DownloadExportAsync(Guid id) => workflow.DownloadExportAsync(id);
}
