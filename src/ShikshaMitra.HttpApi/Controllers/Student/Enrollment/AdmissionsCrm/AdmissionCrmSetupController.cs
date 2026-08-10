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
public class AdmissionCrmSetupController(
	IAdmissionCrmAdmissionYearSetupService admissionYearService,
	IAdmissionCrmCycleReadinessService cycleReadinessService,
	IAdmissionCrmApplicationStagePolicyService stagePolicyService,
	IAdmissionCrmSettingsAccessService settingsAccessService,
	IAdmissionCrmSupportSessionService supportSessionService,
	IAdmissionCrmPlatformPresetService platformPresetService) : AbpControllerBase
{
	[HttpGet("admission-years/{academicYearId:guid}/versions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<IReadOnlyList<AdmissionCrmAdmissionYearSetupDto>> GetAdmissionYearVersionsAsync(
		Guid academicYearId) =>
		admissionYearService.GetAdmissionYearVersionsAsync(academicYearId);

	[HttpGet("admission-years/{academicYearId:guid}/standard-options")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<IReadOnlyList<AdmissionCrmAdmissionYearStandardOptionDto>>
		GetAdmissionYearStandardOptionsAsync(Guid academicYearId) =>
		admissionYearService.GetAdmissionYearStandardOptionsAsync(academicYearId);

	[HttpPost("admission-years/{academicYearId:guid}/draft")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmAdmissionYearSetupDto> CreateAdmissionYearDraftAsync(
		Guid academicYearId) =>
		admissionYearService.CreateAdmissionYearDraftAsync(academicYearId);

	[HttpPut("admission-years/drafts/{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmAdmissionYearSetupDto> UpdateAdmissionYearDraftAsync(
		Guid id,
		[FromBody] UpdateAdmissionCrmAdmissionYearSetupDto input) =>
		admissionYearService.UpdateAdmissionYearDraftAsync(id, input);

	[HttpPost("admission-years/{id:guid}/publish")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmAdmissionYearSetupDto> PublishAdmissionYearAsync(
		Guid id,
		[FromBody] PublishAdmissionCrmAdmissionYearSetupDto input) =>
		admissionYearService.PublishAdmissionYearAsync(id, input);

	[HttpPost("admission-years/{id:guid}/close")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmAdmissionYearSetupDto> CloseAdmissionYearAsync(
		Guid id,
		[FromBody] CloseAdmissionCrmAdmissionYearSetupDto input) =>
		admissionYearService.CloseAdmissionYearAsync(id, input);

	[HttpGet("cycles")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<PagedResultDto<AdmissionCrmCycleListItemDto>> GetCyclesAsync([FromQuery] PagedResultRequestDto input) => cycleReadinessService.GetCyclesAsync(input);

	[HttpGet("cycles/{academicYearId:guid}/summary")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmCycleSummaryDto> GetCycleSummaryAsync(Guid academicYearId) => cycleReadinessService.GetCycleSummaryAsync(academicYearId);

	[HttpGet("cycles/{academicYearId:guid}/stage-policy")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmApplicationStagePolicyDto> GetApplicationStagePolicyAsync(Guid academicYearId) => stagePolicyService.GetApplicationStagePolicyAsync(academicYearId);

	[HttpPost("cycles/{academicYearId:guid}/stage-policy/preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmApplicationStagePolicyImpactDto> PreviewApplicationStagePolicyAsync(Guid academicYearId, [FromBody] UpdateAdmissionCrmApplicationStagePolicyDto input) => stagePolicyService.PreviewApplicationStagePolicyAsync(academicYearId, input);

	[HttpPut("cycles/{academicYearId:guid}/stage-policy")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmApplicationStagePolicyDto> UpdateApplicationStagePolicyAsync(Guid academicYearId, [FromBody] UpdateAdmissionCrmApplicationStagePolicyDto input) => stagePolicyService.UpdateApplicationStagePolicyAsync(academicYearId, input);

	[HttpGet("{academicYearId:guid}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmSetupStatusDto> GetStatusAsync(Guid academicYearId) => cycleReadinessService.GetStatusAsync(academicYearId);

	[HttpGet("access-policy")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<IReadOnlyList<AdmissionCrmSettingsAccessDto>> GetAccessPolicyAsync() => settingsAccessService.GetAccessPolicyAsync();

	[HttpPost("platform/support-sessions")]
	[Authorize(AdmissionCrmPlatformPermissions.StartSupportSession)]
	public Task<AdmissionCrmSupportSessionDto> StartSupportSessionAsync([FromBody] StartAdmissionCrmSupportSessionDto input) => supportSessionService.StartSupportSessionAsync(input);

	[HttpGet("platform/support-sessions/{id:guid}")]
	[Authorize(AdmissionCrmPlatformPermissions.StartSupportSession)]
	public Task<AdmissionCrmSupportSessionDto> GetSupportSessionAsync(Guid id) => supportSessionService.GetSupportSessionAsync(id);

	[HttpDelete("platform/support-sessions/{id:guid}")]
	[Authorize(AdmissionCrmPlatformPermissions.StartSupportSession)]
	public Task EndSupportSessionAsync(Guid id) => supportSessionService.EndSupportSessionAsync(id);

	[HttpGet("platform/presets")]
	[Authorize(AdmissionCrmPlatformPermissions.ManagePresets)]
	public Task<PagedResultDto<AdmissionCrmPlatformPresetDto>> GetPresetsAsync([FromQuery] PagedResultRequestDto input) => platformPresetService.GetPresetsAsync(input);

	[HttpPost("platform/presets")]
	[Authorize(AdmissionCrmPlatformPermissions.ManagePresets)]
	public Task<AdmissionCrmPlatformPresetDto> CreatePresetAsync([FromBody] CreateAdmissionCrmPlatformPresetDto input) => platformPresetService.CreatePresetAsync(input);

	[HttpPost("platform/presets/{id:guid}/publish")]
	[Authorize(AdmissionCrmPlatformPermissions.ManagePresets)]
	public Task<AdmissionCrmPlatformPresetDto> PublishPresetAsync(Guid id) => platformPresetService.PublishPresetAsync(id);

	[HttpPost("platform/presets/preview")]
	[Authorize(AdmissionCrmPlatformPermissions.ApplyPresets)]
	public Task<AdmissionCrmPresetPreviewDto> PreviewPresetAsync([FromBody] AdmissionCrmPresetOperationDto input) => platformPresetService.PreviewPresetAsync(input);

	[HttpPost("platform/presets/apply")]
	[Authorize(AdmissionCrmPlatformPermissions.ApplyPresets)]
	public Task<AdmissionCrmPresetApplyResultDto> ApplyPresetAsync([FromBody] AdmissionCrmPresetOperationDto input) => platformPresetService.ApplyPresetAsync(input);

	[HttpPost("platform/adoptions/{adoptionId:guid}/rollback")]
	[Authorize(AdmissionCrmPlatformPermissions.ApplyPresets)]
	public Task RollbackPresetAsync(Guid adoptionId, [FromQuery] Guid supportSessionId) => platformPresetService.RollbackPresetAsync(adoptionId, supportSessionId);

	[HttpPut("platform/tenants/{tenantId:guid}/access-policy")]
	[Authorize(AdmissionCrmPlatformPermissions.ManageTenantAccess)]
	public Task<IReadOnlyList<AdmissionCrmSettingsAccessDto>> UpdateAccessPolicyAsync(Guid tenantId, [FromQuery] Guid supportSessionId, [FromBody] IReadOnlyList<UpdateAdmissionCrmSettingsAccessDto> input) => settingsAccessService.UpdateAccessPolicyAsync(tenantId, supportSessionId, input);
}