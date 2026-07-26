using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;

namespace ShikshaMitra.HR.Employees.Dashboard;

internal enum EmployeeDashboardAttendanceFactSource
{
	Attendance,
	ApprovedLeave
}

internal sealed record EmployeeDashboardAttendanceFact(
	DateOnly Date,
	EmployeeAttendanceStatusType Status,
	EmployeeDashboardAttendanceFactSource Source = EmployeeDashboardAttendanceFactSource.Attendance);

internal sealed record EmployeeDashboardApprovedLeaveFact(
	DateOnly StartDate,
	DateOnly EndDate,
	bool HalfDay);

internal static class EmployeeDashboardProjection
{
	internal static List<EmployeeDashboardAttendanceFact> MergeAttendanceWithApprovedLeave(
		DateOnly rangeStart,
		DateOnly rangeEnd,
		IReadOnlyCollection<EmployeeDashboardAttendanceFact> attendance,
		IReadOnlyCollection<EmployeeDashboardApprovedLeaveFact> approvedLeaves)
	{
		var factsByDate = attendance
			.OrderBy(fact => fact.Date)
			.GroupBy(fact => fact.Date)
			.ToDictionary(group => group.Key, group => group.First());

		foreach (var leave in approvedLeaves
			         .OrderBy(leave => leave.HalfDay)
			         .ThenBy(leave => leave.StartDate)
			         .ThenBy(leave => leave.EndDate))
		{
			var leaveStart = leave.StartDate < rangeStart ? rangeStart : leave.StartDate;
			var leaveEnd = leave.EndDate > rangeEnd ? rangeEnd : leave.EndDate;
			if (leaveEnd < leaveStart)
			{
				continue;
			}

			for (var date = leaveStart; date <= leaveEnd; date = date.AddDays(1))
			{
				factsByDate.TryAdd(
					date,
					new EmployeeDashboardAttendanceFact(
						date,
						leave.HalfDay
							? EmployeeAttendanceStatusType.HalfDayLeave
							: EmployeeAttendanceStatusType.FullDayLeave,
						EmployeeDashboardAttendanceFactSource.ApprovedLeave));
			}
		}

		return factsByDate.Values
			.OrderBy(fact => fact.Date)
			.ToList();
	}

	internal static decimal? CalculateAttendancePercentage(
		IReadOnlyCollection<EmployeeDashboardAttendanceFact> facts)
	{
		var recordedAttendance = facts
			.Where(fact => fact.Source == EmployeeDashboardAttendanceFactSource.Attendance)
			.ToList();
		if (recordedAttendance.Count == 0)
		{
			return null;
		}

		var attendedUnits = recordedAttendance.Sum(fact => fact.Status switch
		{
			EmployeeAttendanceStatusType.Present => 1m,
			EmployeeAttendanceStatusType.HalfDayLeave => 0.5m,
			_ => 0m
		});

		return Math.Round(
			attendedUnits / recordedAttendance.Count * 100m,
			1,
			MidpointRounding.AwayFromZero);
	}

	internal static string ResolveTodayStatus(
		EmployeeDashboardAttendanceFact? attendance,
		bool isHoliday,
		bool isWeeklyOff)
	{
		if (attendance is not null)
		{
			return ToStatus(attendance);
		}

		if (isHoliday)
		{
			return EmployeeDashboardStatus.Holiday;
		}

		return isWeeklyOff
			? EmployeeDashboardStatus.WeeklyOff
			: EmployeeDashboardStatus.NotMarked;
	}

	internal static bool IsWeeklyOff(
		DayOfWeek dayOfWeek,
		AttendanceWeeklyOffPattern pattern,
		IReadOnlyCollection<int> customWeeklyOffs)
	{
		return pattern switch
		{
			AttendanceWeeklyOffPattern.Sunday => dayOfWeek == DayOfWeek.Sunday,
			AttendanceWeeklyOffPattern.SaturdaySunday =>
				dayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
			AttendanceWeeklyOffPattern.Custom => customWeeklyOffs.Contains((int)dayOfWeek),
			_ => false
		};
	}

	internal static List<EmployeeDashboardAttendanceMonthDto> BuildTrend(
		DateOnly rangeStart,
		DateOnly rangeEnd,
		IReadOnlyCollection<EmployeeDashboardAttendanceFact> attendance)
	{
		if (rangeEnd < rangeStart)
		{
			return [];
		}

		var grouped = attendance
			.GroupBy(row => (row.Date.Year, row.Date.Month))
			.ToDictionary(
				group => group.Key,
				group => group.GroupBy(ToStatus)
					.ToDictionary(statusGroup => statusGroup.Key, statusGroup => statusGroup.Count()));

		var result = new List<EmployeeDashboardAttendanceMonthDto>();
		var month = new DateOnly(rangeStart.Year, rangeStart.Month, 1);
		var finalMonth = new DateOnly(rangeEnd.Year, rangeEnd.Month, 1);
		while (month <= finalMonth)
		{
			grouped.TryGetValue((month.Year, month.Month), out var counts);
			result.Add(new EmployeeDashboardAttendanceMonthDto
				{
					Month = $"{month:yyyy-MM}",
					Present = counts?.GetValueOrDefault(EmployeeDashboardStatus.Present) ?? 0,
					HalfDay = counts?.GetValueOrDefault(EmployeeDashboardStatus.HalfDay) ?? 0,
					Absent = counts?.GetValueOrDefault(EmployeeDashboardStatus.Absent) ?? 0,
					HalfDayLeave = counts?.GetValueOrDefault(EmployeeDashboardStatus.HalfDayLeave) ?? 0,
					FullDayLeave = counts?.GetValueOrDefault(EmployeeDashboardStatus.FullDayLeave) ?? 0
				});
			month = month.AddMonths(1);
		}

		return result;
	}

	internal static string ToStatus(EmployeeDashboardAttendanceFact fact)
	{
		if (fact.Source == EmployeeDashboardAttendanceFactSource.ApprovedLeave)
		{
			return fact.Status == EmployeeAttendanceStatusType.HalfDayLeave
				? EmployeeDashboardStatus.HalfDayLeave
				: EmployeeDashboardStatus.FullDayLeave;
		}

		return fact.Status switch
		{
			EmployeeAttendanceStatusType.Present => EmployeeDashboardStatus.Present,
			EmployeeAttendanceStatusType.HalfDayLeave => EmployeeDashboardStatus.HalfDay,
			EmployeeAttendanceStatusType.FullDayLeave => EmployeeDashboardStatus.Absent,
			_ => EmployeeDashboardStatus.NotMarked
		};
	}
}
