using System;
using System.Threading.Tasks;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyAssignmentTargetValidator(
	IRepository<Employee, Guid> employeeRepository,
	IGroupRepository groupRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository)
	: ITransientDependency
{
	public async Task ValidateReferencesAsync(CreateUpdateHrPolicyAssignmentDto input)
	{
		var attendancePolicy = await attendancePolicyRepository.GetAsync(input.AttendancePolicyId);
		var payrollPolicy = await payrollPolicyRepository.GetAsync(input.PayrollPolicyId);

		if (attendancePolicy.AcademicYearId != input.AcademicYearId || payrollPolicy.AcademicYearId != input.AcademicYearId)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch);
		}

		HrPolicyMapping.EnsureActive(attendancePolicy.Status);
		HrPolicyMapping.EnsureActive(payrollPolicy.Status);
	}

	public async Task ValidateTargetAsync(HrPolicyAssignmentTargetType targetType, string targetKey)
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

				if (!await groupRepository.AnyAsync(x => x.Id == groupId))
				{
					throw new BusinessException(HRErrorCodes.HrPolicyAssignmentTargetInvalid);
				}
				break;
		}
	}
}
