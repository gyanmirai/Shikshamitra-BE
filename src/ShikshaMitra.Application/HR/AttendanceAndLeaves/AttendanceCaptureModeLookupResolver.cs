using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Policies;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceCaptureModeLookupResolver(
	HrPolicyResolver policyResolver,
	AttendanceCaptureModeReadLookupReader readLookupReader)
	: ITransientDependency
{
	public async Task<Dictionary<Guid, AttendanceCaptureMode>> ResolveForDateAsync(
		IEnumerable<Guid> employeeIds,
		DateOnly date)
	{
		var lookup = new Dictionary<Guid, AttendanceCaptureMode>();
		foreach (var employeeId in employeeIds.Distinct())
		{
			var policies = await policyResolver.ResolveAsync(employeeId, date.ToDateTime(TimeOnly.MinValue));
			lookup[employeeId] = policies.AttendancePolicy.CaptureMode;
		}

		return lookup;
	}

	public Task<Dictionary<Guid, AttendanceCaptureMode>> ResolveForDateOrDefaultAsync(
		IEnumerable<Guid> employeeIds,
		DateOnly date,
		AttendanceCaptureMode fallback = AttendanceCaptureMode.Integrated) =>
		readLookupReader.ResolveForDateAsync(employeeIds, date, fallback);

	public async Task<Dictionary<(Guid EmployeeId, DateOnly Date), AttendanceCaptureMode>> ResolveForDatesAsync(
		IEnumerable<Guid> employeeIds,
		IEnumerable<DateOnly> dates)
	{
		var lookup = new Dictionary<(Guid EmployeeId, DateOnly Date), AttendanceCaptureMode>();
		var distinctEmployeeIds = employeeIds.Distinct().ToList();
		var distinctDates = dates.Distinct().ToList();

		foreach (var employeeId in distinctEmployeeIds)
		{
			foreach (var date in distinctDates)
			{
				var policies = await policyResolver.ResolveAsync(employeeId, date.ToDateTime(TimeOnly.MinValue));
				lookup[(employeeId, date)] = policies.AttendancePolicy.CaptureMode;
			}
		}

		return lookup;
	}

	public Task<Dictionary<(Guid EmployeeId, DateOnly Date), AttendanceCaptureMode>> ResolveForDatesOrDefaultAsync(
		IEnumerable<Guid> employeeIds,
		IEnumerable<DateOnly> dates,
		AttendanceCaptureMode fallback = AttendanceCaptureMode.Integrated) =>
		readLookupReader.ResolveForDatesAsync(employeeIds, dates, fallback);

	public async Task<Dictionary<(Guid EmployeeId, DateOnly Date), AttendanceCaptureMode>> ResolveForPairsAsync(
		IEnumerable<(Guid EmployeeId, DateOnly Date)> employeeDates)
	{
		var lookup = new Dictionary<(Guid EmployeeId, DateOnly Date), AttendanceCaptureMode>();
		foreach (var employeeDate in employeeDates.Distinct())
		{
			var policies = await policyResolver.ResolveAsync(
				employeeDate.EmployeeId,
				employeeDate.Date.ToDateTime(TimeOnly.MinValue));
			lookup[employeeDate] = policies.AttendancePolicy.CaptureMode;
		}

		return lookup;
	}
}
