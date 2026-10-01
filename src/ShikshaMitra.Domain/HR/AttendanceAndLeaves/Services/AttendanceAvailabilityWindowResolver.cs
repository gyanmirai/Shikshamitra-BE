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
public class AttendanceAvailabilityWindowResolver(IRepository<ShiftManagement, Guid> shiftRepository)
	: DomainService
{
	public async Task<(TimeOnly Start, TimeOnly End)> ResolveAsync(
		Employee employee,
		bool isFullDay,
		TimeOnly fallbackStart,
		TimeOnly fallbackEnd)
	{
		var shift = employee.ShiftId.HasValue
			? await shiftRepository.FindAsync(employee.ShiftId.Value)
			: null;
		return Resolve(shift, isFullDay, fallbackStart, fallbackEnd);
	}

	/// <summary>Loads configured shifts once for a collection of employees.</summary>
	public async Task<IReadOnlyDictionary<Guid, ShiftManagement>> LoadShiftsAsync(IEnumerable<Employee> employees)
	{
		var shiftIds = employees.Where(x => x.ShiftId.HasValue).Select(x => x.ShiftId!.Value).Distinct().ToArray();
		return shiftIds.Length == 0
			? new Dictionary<Guid, ShiftManagement>()
			: (await shiftRepository.GetListAsync(x => Enumerable.Contains(shiftIds, x.Id))).ToDictionary(x => x.Id);
	}

	/// <summary>Resolves a window from an already loaded shift.</summary>
	public static (TimeOnly Start, TimeOnly End) Resolve(
		ShiftManagement? shift,
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
