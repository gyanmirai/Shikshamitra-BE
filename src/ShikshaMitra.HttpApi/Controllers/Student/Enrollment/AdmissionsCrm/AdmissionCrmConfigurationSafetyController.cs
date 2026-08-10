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
	IAdmissionCrmConfigurationReferenceService referenceService,
	IAdmissionCrmConfigurationImpactService impactService,
	IAdmissionCrmConfigurationCloneService cloneService) : AbpControllerBase
{
	[HttpGet("{academicYearId}/configuration-safety/references")]
	public async Task<AdmissionCrmConfigurationReferenceResultDto> GetReferencesAsync(
		[FromRoute] Guid academicYearId,
		[FromQuery] AdmissionCrmConfigurationReferenceKind kind,
		[FromQuery] string code)
	{
		return await referenceService.GetAsync(academicYearId, kind, code);
	}

	[HttpPost("{academicYearId}/configuration-safety/impact-preview")]
	public async Task<AdmissionCrmConfigurationImpactPreviewDto> PreviewImpactAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] PreviewAdmissionCrmConfigurationImpactDto input)
	{
		return await impactService.PreviewAsync(academicYearId, input);
	}

	[HttpPost("configuration-safety/clone/preview")]
	public async Task<AdmissionCrmConfigurationClonePreviewDto> PreviewCloneAsync(
		[FromBody] PreviewAdmissionCrmConfigurationCloneDto input)
	{
		return await cloneService.PreviewAsync(input);
	}

	[HttpPost("configuration-safety/clone/confirm")]
	public async Task<AdmissionCrmConfigurationCloneResultDto> ConfirmCloneAsync(
		[FromBody] ConfirmAdmissionCrmConfigurationCloneDto input)
	{
		return await cloneService.ConfirmAsync(input);
	}
}
