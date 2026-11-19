using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
[Authorize]
public class LeavePolicyImpactAppService(LeavePolicyImpactPreviewReader reader,
	HrPolicyImpactConfirmationService confirmation, LeavePolicyImpactDetailReader details)
	: ApplicationService, ILeavePolicyImpactAppService
{
	public async Task<LeavePolicyImpactReviewDto> PreviewAsync(LeavePolicyImpactRequest input)
	{
		await CheckActionAsync(input.Action);
		var assessment = await reader.ReadAsync(input);
		await confirmation.AttachTokenAsync(assessment.Preview);
		var preview = assessment.Preview;
		return new()
		{
			PolicyId = preview.PolicyId, Action = preview.Action, CurrentVersion = preview.CurrentVersion,
			ProposedVersion = preview.ProposedVersion, Impact = preview.LeaveImpact!,
			RequiresConfirmation = preview.RequiresConfirmation, ConfirmationToken = preview.ConfirmationToken,
			ExpiresAt = preview.ExpiresAt, Warnings = preview.Warnings, BlockingWarnings = preview.BlockingWarnings, Differences = assessment.Differences
		};
	}

	public async Task<LeavePolicyImpactPageDto> GetDetailsAsync(LeavePolicyImpactDetailsRequest input)
	{
		await CheckActionAsync(input.Assessment.Action);
		if (input.SkipCount < 0 || input.MaxResultCount < 1 || input.MaxResultCount > 100)
			throw new BusinessException(HRErrorCodes.PolicyAssignmentRangeInvalid);
		var assessment = await reader.ReadAsync(input.Assessment);
		if (!string.Equals(assessment.Preview.LeaveImpact!.Fingerprint, input.Fingerprint, StringComparison.Ordinal))
			throw new BusinessException(HRErrorCodes.HrPolicyImpactConfirmationInvalid);
		return await details.ReadAsync(assessment, input);
	}

	private Task CheckActionAsync(string action) => AuthorizationService.CheckAsync(action switch
	{
		"inspect" => ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.View,
		"create" => ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.Create,
		"update" => ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.Edit,
		"set-default" => ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.SetDefault,
		"archive" => ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.Archive,
		_ => throw new BusinessException(HRErrorCodes.HrPolicyInvalidType)
	});
}
