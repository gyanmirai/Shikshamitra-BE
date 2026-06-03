using ShikshaMitra.HR.AttendanceAndLeaves;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Dashboard;

internal static class HRDashboardAttendanceProjection
{
	public static HRDashboardAttendanceTodayDto BuildAttendanceToday(
		IReadOnlyCollection<AttendanceStatusCount> attendanceCounts,
		DateOnly today,
		int activeStaffCount)
	{
		var todayRecords = attendanceCounts.Where(a => a.AttendanceDate == today).ToList();
		var present = todayRecords.Where(a => a.StatusType == EmployeeAttendanceStatusType.Present).Sum(a => a.Count);
		var absent = todayRecords.Where(a => a.StatusType == EmployeeAttendanceStatusType.FullDayLeave).Sum(a => a.Count);
		var halfDay = todayRecords.Where(a => a.StatusType == EmployeeAttendanceStatusType.HalfDayLeave).Sum(a => a.Count);
		var totalMarked = present + absent + halfDay;
		var missingAttendance = Math.Max(0, activeStaffCount - totalMarked);
		var attendanceRate = activeStaffCount == 0
			? 0
			: Math.Round(((decimal)present + (halfDay * 0.5m)) / activeStaffCount * 100, 2);

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
		IReadOnlyCollection<AttendanceStatusCount> attendanceCounts,
		IReadOnlyCollection<LeaveTrendRow> approvedLeaves,
		DateOnly trendStart,
		DateOnly trendEnd)
	{
		var result = new List<HRDashboardAttendanceTrendPointDto>();
		for (var date = trendStart; date <= trendEnd; date = date.AddDays(1))
		{
			var records = attendanceCounts.Where(a => a.AttendanceDate == date).ToList();
			var leaveCount = approvedLeaves
				.Where(l => l.StartDate <= date && l.EndDate >= date)
				.Select(l => l.EmployeeId)
				.Distinct()
				.Count();

			result.Add(new HRDashboardAttendanceTrendPointDto
			{
				Date = date.ToDateTime(TimeOnly.MinValue),
				Present = records.Where(a => a.StatusType == EmployeeAttendanceStatusType.Present).Sum(a => a.Count),
				Absent = records.Where(a => a.StatusType == EmployeeAttendanceStatusType.FullDayLeave).Sum(a => a.Count),
				Leave = leaveCount
			});
		}

		return result;
	}
}
