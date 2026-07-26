using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmConfigurationSafetyController(
	IAdmissionCrmConfigurationSafetyAppService configurationSafetyAppService) : AbpControllerBase
{
	[HttpGet("{academicYearId}/configuration-safety/references")]
	public async Task<AdmissionCrmConfigurationReferenceResultDto> GetReferencesAsync(
		[FromRoute] Guid academicYearId,
		[FromQuery] AdmissionCrmConfigurationReferenceKind kind,
		[FromQuery] string code)
	{
		return await configurationSafetyAppService.GetReferencesAsync(academicYearId, kind, code);
	}

	[HttpPost("{academicYearId}/configuration-safety/impact-preview")]
	public async Task<AdmissionCrmConfigurationImpactPreviewDto> PreviewImpactAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] PreviewAdmissionCrmConfigurationImpactDto input)
	{
		return await configurationSafetyAppService.PreviewImpactAsync(academicYearId, input);
	}

	[HttpPost("configuration-safety/clone/preview")]
	public async Task<AdmissionCrmConfigurationClonePreviewDto> PreviewCloneAsync(
		[FromBody] PreviewAdmissionCrmConfigurationCloneDto input)
	{
		return await configurationSafetyAppService.PreviewCloneAsync(input);
	}

	[HttpPost("configuration-safety/clone/confirm")]
	public async Task<AdmissionCrmConfigurationCloneResultDto> ConfirmCloneAsync(
		[FromBody] ConfirmAdmissionCrmConfigurationCloneDto input)
	{
		return await configurationSafetyAppService.ConfirmCloneAsync(input);
	}
}
