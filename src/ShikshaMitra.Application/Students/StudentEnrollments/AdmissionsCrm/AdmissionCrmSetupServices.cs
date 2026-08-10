using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
public class AdmissionCrmAdmissionYearSetupService(AdmissionCrmAdmissionYearWorkflow workflow) : ApplicationService, IAdmissionCrmAdmissionYearSetupService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<IReadOnlyList<AdmissionCrmAdmissionYearSetupDto>> GetAdmissionYearVersionsAsync(Guid academicYearId) => workflow.GetAdmissionYearVersionsAsync(academicYearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<IReadOnlyList<AdmissionCrmAdmissionYearStandardOptionDto>> GetAdmissionYearStandardOptionsAsync(Guid academicYearId) => workflow.GetAdmissionYearStandardOptionsAsync(academicYearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmAdmissionYearSetupDto> CreateAdmissionYearDraftAsync(Guid academicYearId) => workflow.CreateAdmissionYearDraftAsync(academicYearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmAdmissionYearSetupDto> UpdateAdmissionYearDraftAsync(Guid id, UpdateAdmissionCrmAdmissionYearSetupDto input) => workflow.UpdateAdmissionYearDraftAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmAdmissionYearSetupDto> PublishAdmissionYearAsync(Guid id, PublishAdmissionCrmAdmissionYearSetupDto input) => workflow.PublishAdmissionYearAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmAdmissionYearSetupDto> CloseAdmissionYearAsync(Guid id, CloseAdmissionCrmAdmissionYearSetupDto input) => workflow.CloseAdmissionYearAsync(id, input);
}

[Authorize]
public class AdmissionCrmCycleReadinessService(AdmissionCrmCycleReadinessWorkflow workflow) : ApplicationService, IAdmissionCrmCycleReadinessService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmSetupStatusDto> GetStatusAsync(Guid academicYearId) => workflow.GetStatusAsync(academicYearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<PagedResultDto<AdmissionCrmCycleListItemDto>> GetCyclesAsync(PagedResultRequestDto input) => workflow.GetCyclesAsync(input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmCycleSummaryDto> GetCycleSummaryAsync(Guid academicYearId) => workflow.GetCycleSummaryAsync(academicYearId);
}

[Authorize]
public class AdmissionCrmApplicationStagePolicyService(AdmissionCrmApplicationStagePolicyWorkflow workflow) : ApplicationService, IAdmissionCrmApplicationStagePolicyService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmApplicationStagePolicyDto> GetApplicationStagePolicyAsync(Guid academicYearId) => workflow.GetApplicationStagePolicyAsync(academicYearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmApplicationStagePolicyImpactDto> PreviewApplicationStagePolicyAsync(Guid academicYearId, UpdateAdmissionCrmApplicationStagePolicyDto input) => workflow.PreviewApplicationStagePolicyAsync(academicYearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmApplicationStagePolicyDto> UpdateApplicationStagePolicyAsync(Guid academicYearId, UpdateAdmissionCrmApplicationStagePolicyDto input) => workflow.UpdateApplicationStagePolicyAsync(academicYearId, input);
}

[Authorize]
public class AdmissionCrmSettingsAccessService(AdmissionCrmSettingsAccessWorkflow workflow) : ApplicationService, IAdmissionCrmSettingsAccessService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<IReadOnlyList<AdmissionCrmSettingsAccessDto>> GetAccessPolicyAsync() => workflow.GetAccessPolicyAsync();
	[Authorize(AdmissionCrmPlatformPermissions.ManageTenantAccess)]
	public Task<IReadOnlyList<AdmissionCrmSettingsAccessDto>> UpdateAccessPolicyAsync(Guid tenantId, Guid supportSessionId, IReadOnlyList<UpdateAdmissionCrmSettingsAccessDto> input) => workflow.UpdateAccessPolicyAsync(tenantId, supportSessionId, input);
}

[Authorize]
public class AdmissionCrmSupportSessionService(AdmissionCrmSupportSessionWorkflow workflow) : ApplicationService, IAdmissionCrmSupportSessionService
{
	[Authorize(AdmissionCrmPlatformPermissions.StartSupportSession)]
	public Task<AdmissionCrmSupportSessionDto> StartSupportSessionAsync(StartAdmissionCrmSupportSessionDto input) => workflow.StartSupportSessionAsync(input);
	[Authorize(AdmissionCrmPlatformPermissions.StartSupportSession)]
	public Task<AdmissionCrmSupportSessionDto> GetSupportSessionAsync(Guid id) => workflow.GetSupportSessionAsync(id);
	[Authorize(AdmissionCrmPlatformPermissions.StartSupportSession)]
	public Task EndSupportSessionAsync(Guid id) => workflow.EndSupportSessionAsync(id);
}

[Authorize]
public class AdmissionCrmPlatformPresetService(AdmissionCrmPlatformPresetWorkflow workflow) : ApplicationService, IAdmissionCrmPlatformPresetService
{
	[Authorize(AdmissionCrmPlatformPermissions.ManagePresets)]
	public Task<AdmissionCrmPlatformPresetDto> CreatePresetAsync(CreateAdmissionCrmPlatformPresetDto input) => workflow.CreatePresetAsync(input);
	[Authorize(AdmissionCrmPlatformPermissions.ManagePresets)]
	public Task<AdmissionCrmPlatformPresetDto> PublishPresetAsync(Guid id) => workflow.PublishPresetAsync(id);
	[Authorize(AdmissionCrmPlatformPermissions.ManagePresets)]
	public Task<PagedResultDto<AdmissionCrmPlatformPresetDto>> GetPresetsAsync(PagedResultRequestDto input) => workflow.GetPresetsAsync(input);
	[Authorize(AdmissionCrmPlatformPermissions.ApplyPresets)]
	public Task<AdmissionCrmPresetPreviewDto> PreviewPresetAsync(AdmissionCrmPresetOperationDto input) => workflow.PreviewPresetAsync(input);
	[Authorize(AdmissionCrmPlatformPermissions.ApplyPresets)]
	public Task<AdmissionCrmPresetApplyResultDto> ApplyPresetAsync(AdmissionCrmPresetOperationDto input) => workflow.ApplyPresetAsync(input);
	[Authorize(AdmissionCrmPlatformPermissions.ApplyPresets)]
	public Task RollbackPresetAsync(Guid adoptionId, Guid supportSessionId) => workflow.RollbackPresetAsync(adoptionId, supportSessionId);
}