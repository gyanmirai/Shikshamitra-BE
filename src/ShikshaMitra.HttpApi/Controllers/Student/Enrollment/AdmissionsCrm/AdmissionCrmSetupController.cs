using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm/setup")]
public class AdmissionCrmSetupController(IAdmissionCrmSetupAppService setupAppService) : AbpControllerBase
{
	[HttpGet("cycles")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<PagedResultDto<AdmissionCrmCycleListItemDto>> GetCyclesAsync([FromQuery] PagedResultRequestDto input) => setupAppService.GetCyclesAsync(input);

	[HttpGet("cycles/{academicYearId:guid}/summary")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmCycleSummaryDto> GetCycleSummaryAsync(Guid academicYearId) => setupAppService.GetCycleSummaryAsync(academicYearId);

	[HttpGet("cycles/{academicYearId:guid}/stage-policy")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmApplicationStagePolicyDto> GetApplicationStagePolicyAsync(Guid academicYearId) => setupAppService.GetApplicationStagePolicyAsync(academicYearId);

	[HttpPost("cycles/{academicYearId:guid}/stage-policy/preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmApplicationStagePolicyImpactDto> PreviewApplicationStagePolicyAsync(Guid academicYearId, [FromBody] UpdateAdmissionCrmApplicationStagePolicyDto input) => setupAppService.PreviewApplicationStagePolicyAsync(academicYearId, input);

	[HttpPut("cycles/{academicYearId:guid}/stage-policy")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmApplicationStagePolicyDto> UpdateApplicationStagePolicyAsync(Guid academicYearId, [FromBody] UpdateAdmissionCrmApplicationStagePolicyDto input) => setupAppService.UpdateApplicationStagePolicyAsync(academicYearId, input);

	[HttpGet("{academicYearId:guid}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmSetupStatusDto> GetStatusAsync(Guid academicYearId) => setupAppService.GetStatusAsync(academicYearId);

	[HttpGet("access-policy")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<IReadOnlyList<AdmissionCrmSettingsAccessDto>> GetAccessPolicyAsync() => setupAppService.GetAccessPolicyAsync();

	[HttpPost("platform/support-sessions")]
	[Authorize(AdmissionCrmPlatformPermissions.StartSupportSession)]
	public Task<AdmissionCrmSupportSessionDto> StartSupportSessionAsync([FromBody] StartAdmissionCrmSupportSessionDto input) => setupAppService.StartSupportSessionAsync(input);

	[HttpGet("platform/support-sessions/{id:guid}")]
	[Authorize(AdmissionCrmPlatformPermissions.StartSupportSession)]
	public Task<AdmissionCrmSupportSessionDto> GetSupportSessionAsync(Guid id) => setupAppService.GetSupportSessionAsync(id);

	[HttpDelete("platform/support-sessions/{id:guid}")]
	[Authorize(AdmissionCrmPlatformPermissions.StartSupportSession)]
	public Task EndSupportSessionAsync(Guid id) => setupAppService.EndSupportSessionAsync(id);

	[HttpGet("platform/presets")]
	[Authorize(AdmissionCrmPlatformPermissions.ManagePresets)]
	public Task<PagedResultDto<AdmissionCrmPlatformPresetDto>> GetPresetsAsync([FromQuery] PagedResultRequestDto input) => setupAppService.GetPresetsAsync(input);

	[HttpPost("platform/presets")]
	[Authorize(AdmissionCrmPlatformPermissions.ManagePresets)]
	public Task<AdmissionCrmPlatformPresetDto> CreatePresetAsync([FromBody] CreateAdmissionCrmPlatformPresetDto input) => setupAppService.CreatePresetAsync(input);

	[HttpPost("platform/presets/{id:guid}/publish")]
	[Authorize(AdmissionCrmPlatformPermissions.ManagePresets)]
	public Task<AdmissionCrmPlatformPresetDto> PublishPresetAsync(Guid id) => setupAppService.PublishPresetAsync(id);

	[HttpPost("platform/presets/preview")]
	[Authorize(AdmissionCrmPlatformPermissions.ApplyPresets)]
	public Task<AdmissionCrmPresetPreviewDto> PreviewPresetAsync([FromBody] AdmissionCrmPresetOperationDto input) => setupAppService.PreviewPresetAsync(input);

	[HttpPost("platform/presets/apply")]
	[Authorize(AdmissionCrmPlatformPermissions.ApplyPresets)]
	public Task<AdmissionCrmPresetApplyResultDto> ApplyPresetAsync([FromBody] AdmissionCrmPresetOperationDto input) => setupAppService.ApplyPresetAsync(input);

	[HttpPost("platform/adoptions/{adoptionId:guid}/rollback")]
	[Authorize(AdmissionCrmPlatformPermissions.ApplyPresets)]
	public Task RollbackPresetAsync(Guid adoptionId, [FromQuery] Guid supportSessionId) => setupAppService.RollbackPresetAsync(adoptionId, supportSessionId);

	[HttpPut("platform/tenants/{tenantId:guid}/access-policy")]
	[Authorize(AdmissionCrmPlatformPermissions.ManageTenantAccess)]
	public Task<IReadOnlyList<AdmissionCrmSettingsAccessDto>> UpdateAccessPolicyAsync(Guid tenantId, [FromQuery] Guid supportSessionId, [FromBody] IReadOnlyList<UpdateAdmissionCrmSettingsAccessDto> input) => setupAppService.UpdateAccessPolicyAsync(tenantId, supportSessionId, input);
}
