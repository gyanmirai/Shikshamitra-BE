using System;
using ShikshaMitra.HR.Employees;

namespace ShikshaMitra.HR.Policies;

internal static class HrPolicyEmployeeOverridePayloadFactory
{
	public static CreateUpdateHrPolicyAssignmentDto Build(
		UpsertEmployeePolicyOverrideDto input,
		Employee employee,
		string targetKey,
		DateTime effectiveFrom,
		DateTime? effectiveTo) => new()
	{
		AcademicYearId = input.AcademicYearId,
		TargetType = HrPolicyMapping.MapAssignmentTargetType(HrPolicyAssignmentTargetType.Employee),
		TargetKey = targetKey,
		TargetDisplayName = employee.EmployeeName,
		AttendancePolicyId = input.AttendancePolicyId,
		PayrollPolicyId = input.PayrollPolicyId,
		LeavePolicyId = input.LeavePolicyId,
		EffectiveFrom = effectiveFrom,
		EffectiveTo = effectiveTo,
		IsActive = true,
		Priority = 100,
		AssignmentReason = string.IsNullOrWhiteSpace(input.AssignmentReason)
			? "Staff payroll step override"
			: input.AssignmentReason,
		ConfirmationToken = input.ConfirmationToken
	};
}
