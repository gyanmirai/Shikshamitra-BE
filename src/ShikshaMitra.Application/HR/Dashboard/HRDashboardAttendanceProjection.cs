using ShikshaMitra.HR.AttendanceAndLeaves;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Dashboard;

internal static class HRDashboardAttendanceProjection
{
	public static HRDashboardAttendanceTodayDto BuildAttendanceToday(
		IReadOnlyCollection<AttendanceTrendRow> attendanceRows,
		IReadOnlyCollection<LeaveTrendRow> approvedLeaves,
		DateOnly today,
		int activeStaffCount)
	{
		var todayRecords = attendanceRows
			.Where(row => row.AttendanceDate == today)
			.ToList();
		var markedEmployeeIds = todayRecords
			.Select(row => row.EmployeeId)
			.ToHashSet();
		var approvedLeaveUnitsByEmployee = BuildApprovedLeaveUnitsByDate(
				approvedLeaves,
				today,
				today)
			.GetValueOrDefault(today) ?? [];
		var unrecordedLeaveUnits = approvedLeaveUnitsByEmployee
			.Where(entry => !markedEmployeeIds.Contains(entry.Key))
			.ToDictionary(entry => entry.Key, entry => entry.Value);
		var present = todayRecords.Count(row =>
			row.StatusType == EmployeeAttendanceStatusType.Present);
		var absent = todayRecords.Count(row =>
			row.StatusType == EmployeeAttendanceStatusType.FullDayLeave);
		var halfDay = todayRecords.Count(row =>
			row.StatusType == EmployeeAttendanceStatusType.HalfDayLeave);
		var fullDayLeaveCount = unrecordedLeaveUnits.Count(entry => entry.Value == 1m);
		var expectedAttendanceUnits = Math.Max(
			0m,
			activeStaffCount - unrecordedLeaveUnits.Values.Sum());
		var missingAttendance = Math.Max(
			0,
			activeStaffCount - markedEmployeeIds.Count - fullDayLeaveCount);
		var attendanceRate = expectedAttendanceUnits == 0
			? 0
			: Math.Round(
				((decimal)present + (halfDay * 0.5m)) / expectedAttendanceUnits * 100,
				2);

		return new HRDashboardAttendanceTodayDto
		{
			Present = present,
			Absent = absent,
			HalfDay = halfDay,
			MissingAttendance = missingAttendance,
			LateOrException = absent + halfDay + missingAttendance,
			AttendanceRate = attendanceRate
		};
	}

	public static List<HRDashboardAttendanceTrendPointDto> BuildAttendanceTrend(
		IReadOnlyCollection<AttendanceTrendRow> attendanceRows,
		IReadOnlyCollection<LeaveTrendRow> approvedLeaves,
		DateOnly trendStart,
		DateOnly trendEnd)
	{
		var attendanceByDate = attendanceRows
			.GroupBy(row => row.AttendanceDate)
			.ToDictionary(group => group.Key, group => group.ToList());
		var approvedLeaveUnitsByDate = BuildApprovedLeaveUnitsByDate(
			approvedLeaves,
			trendStart,
			trendEnd);
		var result = new List<HRDashboardAttendanceTrendPointDto>();
		for (var date = trendStart; date <= trendEnd; date = date.AddDays(1))
		{
			var records = attendanceByDate.GetValueOrDefault(date) ?? [];
			var markedEmployeeIds = records
				.Select(row => row.EmployeeId)
				.ToHashSet();
			var approvedLeaveUnits = approvedLeaveUnitsByDate.GetValueOrDefault(date);
			var leaveCount = approvedLeaveUnits is null
				? 0
				: approvedLeaveUnits.Keys.Count(employeeId =>
					!markedEmployeeIds.Contains(employeeId));

			result.Add(new HRDashboardAttendanceTrendPointDto
			{
				Date = date.ToDateTime(TimeOnly.MinValue),
				Present = records.Count(row =>
					row.StatusType == EmployeeAttendanceStatusType.Present),
				Absent = records.Count(row =>
					row.StatusType == EmployeeAttendanceStatusType.FullDayLeave),
				Leave = leaveCount
			});
		}

		return result;
	}

	private static Dictionary<DateOnly, Dictionary<Guid, decimal>> BuildApprovedLeaveUnitsByDate(
		IReadOnlyCollection<LeaveTrendRow> approvedLeaves,
		DateOnly trendStart,
		DateOnly trendEnd)
	{
		var unitsByDate = new Dictionary<DateOnly, Dictionary<Guid, decimal>>();
		foreach (var leave in approvedLeaves)
		{
			var leaveStart = leave.StartDate < trendStart ? trendStart : leave.StartDate;
			var leaveEnd = leave.EndDate > trendEnd ? trendEnd : leave.EndDate;
			if (leaveEnd < leaveStart)
			{
				continue;
			}

			for (var date = leaveStart; date <= leaveEnd; date = date.AddDays(1))
			{
				if (!unitsByDate.TryGetValue(date, out var employeeUnits))
				{
					employeeUnits = [];
					unitsByDate[date] = employeeUnits;
				}

				var leaveUnits = leave.HalfDay ? 0.5m : 1m;
				employeeUnits[leave.EmployeeId] = Math.Max(
					employeeUnits.GetValueOrDefault(leave.EmployeeId),
					leaveUnits);
			}
		}

		return unitsByDate;
	}
}
