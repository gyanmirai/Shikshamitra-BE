using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionFormStageAppService(
	AdmissionFormStageReadService reader,
	AdmissionFormStageCommandService commands) : ShikshaMitraAppService, IAdmissionFormStageAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionFormStageDto> GetAsync(Guid applicationId, Guid stageId) =>
		reader.GetAsync(applicationId, stageId);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit)]
	public Task<AdmissionFormStageDto> VerifySectionAsync(
		Guid applicationId, Guid stageId, Guid sectionId, VerifyAdmissionFormSectionDto input) =>
		commands.VerifySectionAsync(applicationId, stageId, sectionId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Reopen)]
	public Task<AdmissionFormStageDto> RequestCorrectionAsync(
		Guid applicationId, Guid stageId, RequestAdmissionFormCorrectionDto input) =>
		commands.RequestCorrectionAsync(applicationId, stageId, input);
}
