using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController]
[Authorize]
[Route("api/student/enrollment/admissions/applications/{applicationId:guid}/form-stages/{stageId:guid}")]
public class AdmissionFormStagesController(IAdmissionFormStageAppService service) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<Result<AdmissionFormStageDto>> GetAsync(Guid applicationId, Guid stageId) =>
		Result.Success(await service.GetAsync(applicationId, stageId));

	[HttpPost("sections/{sectionId:guid}/verification")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit)]
	public async Task<Result<AdmissionFormStageDto>> VerifySectionAsync(
		Guid applicationId, Guid stageId, Guid sectionId, VerifyAdmissionFormSectionDto input) =>
		Result.Success(await service.VerifySectionAsync(applicationId, stageId, sectionId, input));

	[HttpPost("correction")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Reopen)]
	public async Task<Result<AdmissionFormStageDto>> RequestCorrectionAsync(
		Guid applicationId, Guid stageId, RequestAdmissionFormCorrectionDto input) =>
		Result.Success(await service.RequestCorrectionAsync(applicationId, stageId, input));
}
