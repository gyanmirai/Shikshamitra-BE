using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public class EmployeePersonalInfoPolicyOverrideWriter(
	IHrPolicyAssignmentAppService policyAssignmentAppService,
	IPermissionChecker permissionChecker)
	: ITransientDependency
{
	private const string OnboardingDefaultPolicyConfirmationTokenMarker =
		"__employee_onboarding_default_policy_assignment__";

	public async Task CheckAccessAsync(CreateEmployeePolicyOverrideDto? input)
	{
		if (input == null)
		{
			return;
		}

		var missingPermissions = new List<string>();
		if (!await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Policies.Assign))
		{
			missingPermissions.Add(ShikshaMitraSubmodulePermissions.HR.Policies.Assign);
		}
		if (RequiresDefaultPolicyConfirmation(input.ConfirmationToken) &&
			!await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact))
		{
			missingPermissions.Add(ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact);
		}
		if (missingPermissions.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.EmployeePolicyAccessRequired)
				.WithData("missingPermissions", missingPermissions.ToArray());
		}
	}

	public async Task ApplyAsync(
		Employee employee,
		CreateEmployeePolicyOverrideDto input)
	{
		var overrideInput = new UpsertEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = input.AcademicYearId,
			AttendancePolicyId = input.AttendancePolicyId,
			PayrollPolicyId = input.PayrollPolicyId,
			LeavePolicyId = input.LeavePolicyId,
			EffectiveFrom = input.EffectiveFrom,
			EffectiveTo = input.EffectiveTo,
			AssignmentReason = input.AssignmentReason ?? "Staff creation override",
			ConfirmationToken = input.ConfirmationToken
		};

		await EnsureDefaultPolicyOverrideConfirmedAsync(employee, overrideInput);
		await policyAssignmentAppService.UpsertEmployeeOverrideAsync(overrideInput);
	}

	private async Task EnsureDefaultPolicyOverrideConfirmedAsync(
		Employee employee,
		UpsertEmployeePolicyOverrideDto input)
	{
		if (!RequiresDefaultPolicyConfirmation(input.ConfirmationToken))
		{
			return;
		}

		var targetKey = HrPolicyAssignment.NormalizeTargetKey(
			HrPolicyAssignmentTargetType.Employee,
			employee.Id.ToString("D"));
		var preview = await policyAssignmentAppService.PreviewImpactAsync(
			new HrPolicyAssignmentImpactPreviewRequestDto
			{
				Action = HrPolicyAssignmentImpactService.EmployeeOverrideUpsertAction,
				Assignment = new CreateUpdateHrPolicyAssignmentDto
				{
					AcademicYearId = input.AcademicYearId,
					TargetType = HrPolicyMapping.MapAssignmentTargetType(
						HrPolicyAssignmentTargetType.Employee),
					TargetKey = targetKey,
					TargetDisplayName = employee.EmployeeName,
					AttendancePolicyId = input.AttendancePolicyId,
					PayrollPolicyId = input.PayrollPolicyId,
					LeavePolicyId = input.LeavePolicyId,
					EffectiveFrom = input.EffectiveFrom.Date,
					EffectiveTo = input.EffectiveTo?.Date,
					IsActive = true,
					Priority = 100,
					AssignmentReason = input.AssignmentReason
				}
			});

		input.ConfirmationToken = preview.RequiresConfirmation
			? preview.ConfirmationToken
			: null;
	}

	private static bool RequiresDefaultPolicyConfirmation(string? confirmationToken) =>
		string.Equals(confirmationToken, OnboardingDefaultPolicyConfirmationTokenMarker, StringComparison.Ordinal);
}
