using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Policies;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public class EmployeePersonalInfoPolicyOverrideWriter(
	IHrPolicyAssignmentAppService policyAssignmentAppService)
	: ITransientDependency
{
	private const string OnboardingDefaultPolicyConfirmationTokenMarker =
		"__employee_onboarding_default_policy_assignment__";

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
			AssignmentReason = input.AssignmentReason ?? "Employee creation override",
			ConfirmationToken = input.ConfirmationToken
		};

		await EnsureDefaultPolicyOverrideConfirmedAsync(employee, overrideInput);
		await policyAssignmentAppService.UpsertEmployeeOverrideAsync(overrideInput);
	}

	private async Task EnsureDefaultPolicyOverrideConfirmedAsync(
		Employee employee,
		UpsertEmployeePolicyOverrideDto input)
	{
		if (!string.Equals(
			    input.ConfirmationToken,
			    OnboardingDefaultPolicyConfirmationTokenMarker,
			    StringComparison.Ordinal))
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
}
