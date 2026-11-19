using ShikshaMitra.HR.WorkShifts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

/// <summary>
/// Resolves daytime attendance unavailability from the employee's HR shift.
/// Half-day attendance retains the existing first-half absence convention.
/// </summary>
public class AttendanceAvailabilityWindowResolver(EmployeeShiftResolver shiftResolver)
	: DomainService
{
	public async Task<(TimeOnly Start, TimeOnly End)> ResolveAsync(
		Employee employee,
		bool isFullDay,
		TimeOnly fallbackStart,
		TimeOnly fallbackEnd, DateOnly date)
	{
		var shift = await shiftResolver.FindShiftAsync(employee.Id, date);
		return Resolve(shift, isFullDay, fallbackStart, fallbackEnd);
	}

	/// <summary>Loads configured shifts once for a collection of employees.</summary>
	public Task<List<EmployeeShiftAssignment>> LoadShiftsAsync(IEnumerable<Employee> employees, DateOnly from, DateOnly to) => shiftResolver.LoadAsync(employees.Select(x => x.Id), from, to);

	/// <summary>Resolves a window from an already loaded shift.</summary>
	public static (TimeOnly Start, TimeOnly End) Resolve(
		EmployeeWorkShift? shift,
		bool isFullDay,
		TimeOnly fallbackStart,
		TimeOnly fallbackEnd)
	{

		// Availability records currently represent a single date with End > Start.
		// Keep the existing window when a shift is absent or cannot fit that contract.
		if (shift == null || shift.EndTime <= shift.StartTime)
		{
			return (fallbackStart, fallbackEnd);
		}

		var end = isFullDay
			? shift.EndTime
			: shift.StartTime.Add((shift.EndTime - shift.StartTime) / 2);
		return (shift.StartTime, end);
	}
}