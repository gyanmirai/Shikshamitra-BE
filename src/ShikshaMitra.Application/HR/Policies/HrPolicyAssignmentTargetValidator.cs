using System;
using System.Threading.Tasks;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyAssignmentTargetValidator(
	IRepository<Employee, Guid> employeeRepository,
	IGroupRepository groupRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	ILeavePolicyRepository leavePolicyRepository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarPolicyRepository)
	: ITransientDependency
{
	public async Task ValidateReferencesAsync(CreateUpdateHrPolicyAssignmentDto input)
	{
		var attendancePolicy = await attendancePolicyRepository.FindAsync(input.AttendancePolicyId);
		var payrollPolicy = await payrollPolicyRepository.FindAsync(input.PayrollPolicyId);

		if (attendancePolicy == null)
		{
			throw new BusinessException(HRErrorCodes.AttendancePolicyRequired);
		}

		if (payrollPolicy == null)
		{
			throw new BusinessException(HRErrorCodes.PayrollPolicyRequired);
		}

		if (attendancePolicy.AcademicYearId != input.AcademicYearId || payrollPolicy.AcademicYearId != input.AcademicYearId)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch);
		}

		HrPolicyMapping.EnsureActive(attendancePolicy.Status);
		HrPolicyMapping.EnsureActive(payrollPolicy.Status);
		if (input.LeavePolicyId.HasValue)
		{
			var leavePolicy = await leavePolicyRepository.FindWithoutDetailsAsync(input.LeavePolicyId.Value);
			if (leavePolicy == null)
			{
				throw new BusinessException(HRErrorCodes.LeavePolicyNotFound);
			}

			if (leavePolicy.AcademicYearId != input.AcademicYearId)
			{
				throw new BusinessException(HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch);
			}

			if (leavePolicy.Status != HrPolicyStatus.Active || !leavePolicy.IsActive)
			{
				throw new BusinessException(HRErrorCodes.LeavePolicyInactive);
			}
		}

		await EnsureActiveWorkCalendarAsync(attendancePolicy.WorkCalendarPolicyId, attendancePolicy.AcademicYearId);
		await EnsureActiveWorkCalendarAsync(payrollPolicy.WorkCalendarPolicyId, payrollPolicy.AcademicYearId);
	}

	public async Task ValidateTargetAsync(HrPolicyAssignmentTargetType targetType, string targetKey, Guid academicYearId)
	{
		switch (targetType)
		{
			case HrPolicyAssignmentTargetType.Employee:
				if (!Guid.TryParse(targetKey, out var employeeId))
				{
					throw new BusinessException(HRErrorCodes.HrPolicyAssignmentTargetInvalid);
				}

				if (!await employeeRepository.AnyAsync(x => x.Id == employeeId))
				{
					throw new BusinessException(HRErrorCodes.HrPolicyAssignmentTargetInvalid);
				}
				break;

			case HrPolicyAssignmentTargetType.Group:
				if (!Guid.TryParse(targetKey, out var groupId))
				{
					throw new BusinessException(HRErrorCodes.HrPolicyAssignmentTargetInvalid);
				}

				var group = await groupRepository.FindAsync(groupId);
				if (group == null)
				{
					throw new BusinessException(HRErrorCodes.HrPolicyAssignmentTargetInvalid);
				}

				if (group.AcademicYearId != academicYearId)
				{
					throw new BusinessException(HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch);
				}
				break;
		}
	}

	private async Task EnsureActiveWorkCalendarAsync(Guid workCalendarPolicyId, Guid academicYearId)
	{
		var workCalendar = await workCalendarPolicyRepository.FindAsync(workCalendarPolicyId);
		if (workCalendar == null || workCalendar.AcademicYearId != academicYearId)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyInvalidWorkCalendar);
		}

		if (workCalendar.Status != HrPolicyStatus.Active)
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarPolicyMustBeActive);
		}
	}
}
