using ShikshaMitra.Common;
using System;
using System.Linq;

namespace ShikshaMitra.HR.Employees;

public static class EmployeeLifecycleEligibility
{
	public static IQueryable<Employee> ApplyPayrollMonthEligibility(
		this IQueryable<Employee> query,
		DateOnly monthStart,
		DateOnly monthEnd,
		bool treatNullStatusAsActive = true)
	{
		var joinedEndExclusive = monthEnd.ToDateTime(TimeOnly.MinValue).AddDays(1);
		return query.Where(employee =>
			((employee.DateOfJoining.HasValue && employee.DateOfJoining.Value <= monthEnd) ||
			 (!employee.DateOfJoining.HasValue && employee.CreationTime < joinedEndExclusive)) &&
			((!employee.EmployeeStatusType.HasValue && treatNullStatusAsActive) ||
			 employee.EmployeeStatusType == EmployeeStatusType.Active ||
			 (employee.EmployeeStatusType != EmployeeStatusType.Active &&
			  employee.StatusEffectiveDate.HasValue &&
			  employee.StatusEffectiveDate.Value >= monthStart)));
	}

	public static IQueryable<Employee> ApplyAttendanceDateEligibility(
		this IQueryable<Employee> query,
		DateOnly attendanceDate,
		bool treatNullStatusAsActive = true)
	{
		var attendanceDateEndExclusive = attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(1);
		return query.Where(employee =>
			((employee.DateOfJoining.HasValue && employee.DateOfJoining.Value <= attendanceDate) ||
			 (!employee.DateOfJoining.HasValue && employee.CreationTime < attendanceDateEndExclusive)) &&
			((!employee.EmployeeStatusType.HasValue && treatNullStatusAsActive) ||
			 employee.EmployeeStatusType == EmployeeStatusType.Active ||
			 (employee.EmployeeStatusType != EmployeeStatusType.Active &&
			  employee.StatusEffectiveDate.HasValue &&
			  employee.StatusEffectiveDate.Value >= attendanceDate)));
	}

	public static bool IsAttendanceEligibleOnDate(
		EmployeeStatusType? status,
		DateOnly? statusEffectiveDate,
		DateOnly attendanceDate,
		bool treatNullStatusAsActive = true)
	{
		if (!status.HasValue)
		{
			return treatNullStatusAsActive;
		}

		if (status.Value == EmployeeStatusType.Active)
		{
			return true;
		}

		return statusEffectiveDate.HasValue && statusEffectiveDate.Value >= attendanceDate;
	}
}
