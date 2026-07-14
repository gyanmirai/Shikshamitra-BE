using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmPlaybookController(
	IAdmissionCrmPlaybookAppService playbookAppService) : AbpControllerBase
{
	[HttpPost("{academicYearId}/configuration/playbooks")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePlaybooks)]
	public async Task<AdmissionCrmPlaybookDto> CreateAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] CreateAdmissionCrmPlaybookDto input)
	{
		return await playbookAppService.CreateAsync(academicYearId, input);
	}

	[HttpGet("{academicYearId}/configuration/playbooks")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<PagedResultDto<AdmissionCrmPlaybookDto>> GetListAsync(
		[FromRoute] Guid academicYearId,
		[FromQuery] GetAdmissionCrmPlaybookListInput input)
	{
		return await playbookAppService.GetListAsync(academicYearId, input);
	}

	[HttpGet("configuration/playbooks/{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmPlaybookDto> GetAsync([FromRoute] Guid id)
	{
		return await playbookAppService.GetAsync(id);
	}

	[HttpPut("configuration/playbooks/{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePlaybooks)]
	public async Task<AdmissionCrmPlaybookDto> UpdateAsync(
		[FromRoute] Guid id,
		[FromBody] UpdateAdmissionCrmPlaybookDto input)
	{
		return await playbookAppService.UpdateAsync(id, input);
	}

	[HttpPost("configuration/playbooks/{id}/publish")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PublishPlaybooks)]
	public async Task<AdmissionCrmPlaybookDto> PublishAsync([FromRoute] Guid id)
	{
		return await playbookAppService.PublishAsync(id);
	}

	[HttpPost("configuration/playbooks/{id}/new-version")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePlaybooks)]
	public async Task<AdmissionCrmPlaybookDto> CreateNewVersionAsync([FromRoute] Guid id)
	{
		return await playbookAppService.CreateNewVersionAsync(id);
	}

	[HttpPost("configuration/playbooks/{id}/retire")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PublishPlaybooks)]
	public async Task<AdmissionCrmPlaybookDto> RetireAsync([FromRoute] Guid id)
	{
		return await playbookAppService.RetireAsync(id);
	}

	[HttpPost("{academicYearId}/playbooks/resolve")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmPlaybookResolutionDto> ResolveAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] ResolveAdmissionCrmPlaybookDto input)
	{
		return await playbookAppService.ResolveAsync(academicYearId, input);
	}

	[HttpPost("configuration/playbooks/{id}/slots/generate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageExamConfiguration)]
	public async Task<AdmissionCrmSlotGenerationResultDto> GenerateSlotsAsync(
		[FromRoute] Guid id,
		[FromBody] GenerateAdmissionCrmPlaybookSlotsDto input)
	{
		return await playbookAppService.GenerateSlotsAsync(id, input);
	}

	[HttpPost("configuration/playbooks/{id}/slots/recommend")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmSlotRecommendationDto> RecommendSlotAsync(
		[FromRoute] Guid id,
		[FromBody] RecommendAdmissionCrmPlaybookSlotDto input)
	{
		return await playbookAppService.RecommendSlotAsync(id, input);
	}

	[HttpPost("configuration/playbooks/{id}/exam-results/preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmExamResultPreviewDto> PreviewExamResultAsync(
		[FromRoute] Guid id,
		[FromBody] PreviewAdmissionCrmExamResultDto input)
	{
		return await playbookAppService.PreviewExamResultAsync(id, input);
	}
}
