using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
[Authorize]
public class HrPolicyVersionAppService(
	IHrPolicySafetyAppService policySafetyAppService,
	HrPolicyVersionReadService readService,
	HrPolicyVersionCaptureService captureService,
	HrPolicyVersionRestoreService restoreService)
	: ApplicationService, IHrPolicyVersionAppService
{
	public async Task<PagedResultDto<HrPolicyVersionDto>> GetListAsync(
		string policyType,
		Guid policyId,
		PagedAndSortedResultRequestDto input, string? search = null, string? action = null)
	{
		await CheckTypePermissionAsync(policyType, ShikshaMitraSubmodulePermissions.HR.Policies.View,
			ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.View);
		return await readService.GetListAsync(policyType, policyId, input, search, action);
	}

	public async Task<HrPolicyVersionDto> GetAsync(string policyType, Guid policyId, Guid versionId)
	{
		await CheckTypePermissionAsync(policyType, ShikshaMitraSubmodulePermissions.HR.Policies.View,
			ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.View);
		return await readService.GetAsync(policyType, policyId, versionId);
	}

	public async Task<HrPolicyVersionCompareDto> CompareAsync(
		string policyType,
		Guid policyId,
		Guid fromVersionId,
		Guid toVersionId)
	{
		await CheckTypePermissionAsync(policyType, ShikshaMitraSubmodulePermissions.HR.Policies.View,
			ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.View);
		return await readService.CompareAsync(policyType, policyId, fromVersionId, toVersionId);
	}

	public async Task<HrPolicyImpactPreviewDto> GetRestorePreviewAsync(string policyType, Guid policyId, Guid versionId)
	{
		await CheckTypePermissionAsync(policyType, ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact,
			ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.Create);
		var version = await readService.GetVersionEntityAsync(policyType, policyId, versionId);
		if (HrPolicyVersionProjection.NormalizePolicyType(policyType) == "leave")
			return new() { PolicyType = "leave", PolicyId = policyId, AcademicYearId = version.AcademicYearId,
				Action = "restore-draft", CurrentVersion = version.VersionNumber,
				ResolutionSummary = "Creates a new non-default draft with fresh rule identities. Source policies, requests, assignments and settlements remain unchanged." };
		return await policySafetyAppService.PreviewPolicyImpactAsync(
			HrPolicyVersionProjection.ToRestorePreviewRequest(policyType, policyId, version));
	}

	public async Task<object> RestoreDraftAsync(string policyType, Guid policyId, Guid versionId, HrPolicyRestoreDraftRequestDto input)
	{
		await CheckTypePermissionAsync(policyType, ShikshaMitraSubmodulePermissions.HR.Policies.Clone,
			ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.Create);
		var version = await readService.GetVersionEntityAsync(policyType, policyId, versionId);
		return await restoreService.RestoreDraftAsync(version, policyType, policyId, input);
	}

	private Task CheckTypePermissionAsync(string policyType, string existing, string leave) =>
		AuthorizationService.CheckAsync(HrPolicyVersionProjection.NormalizePolicyType(policyType) == "leave" ? leave : existing);

	public Task CaptureAsync(string policyType, object policyDto, string action, string? reason = null) =>
		captureService.CaptureAsync(policyType, policyDto, action, reason);
}
