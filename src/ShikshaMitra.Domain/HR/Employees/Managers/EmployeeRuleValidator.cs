using System;
using ShikshaMitra.HR;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees;

public static class EmployeeRuleValidator
{
	public static void Validate(Employee employee, DateOnly today)
	{
		if (employee.DateOfBirth.HasValue && employee.DateOfBirth.Value >= today)
		{
			throw new BusinessException(HRErrorCodes.DobInFuture);
		}

		if (employee.DateOfJoining.HasValue && employee.DateOfBirth.HasValue
			&& employee.DateOfJoining.Value <= employee.DateOfBirth.Value)
		{
			throw new BusinessException(HRErrorCodes.DojBeforeDob);
		}
	}
}
