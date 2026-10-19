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
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public Task<PagedResultDto<HrPolicyVersionDto>> GetListAsync(
		string policyType,
		Guid policyId,
		PagedAndSortedResultRequestDto input, string? search = null, string? action = null) =>
		readService.GetListAsync(policyType, policyId, input, search, action);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public Task<HrPolicyVersionDto> GetAsync(string policyType, Guid policyId, Guid versionId) =>
		readService.GetAsync(policyType, policyId, versionId);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public Task<HrPolicyVersionCompareDto> CompareAsync(
		string policyType,
		Guid policyId,
		Guid fromVersionId,
		Guid toVersionId) =>
		readService.CompareAsync(policyType, policyId, fromVersionId, toVersionId);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact)]
	public async Task<HrPolicyImpactPreviewDto> GetRestorePreviewAsync(string policyType, Guid policyId, Guid versionId)
	{
		var version = await readService.GetVersionEntityAsync(policyType, policyId, versionId);
		return await policySafetyAppService.PreviewPolicyImpactAsync(
			HrPolicyVersionProjection.ToRestorePreviewRequest(policyType, policyId, version));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Clone)]
	public async Task<object> RestoreDraftAsync(string policyType, Guid policyId, Guid versionId, HrPolicyRestoreDraftRequestDto input)
	{
		var version = await readService.GetVersionEntityAsync(policyType, policyId, versionId);
		return await restoreService.RestoreDraftAsync(version, policyType, policyId, input);
	}

	public Task CaptureAsync(string policyType, object policyDto, string action, string? reason = null) =>
		captureService.CaptureAsync(policyType, policyDto, action, reason);
}
