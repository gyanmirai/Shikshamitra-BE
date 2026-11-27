using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves;

namespace ShikshaMitra.HR.Dashboard;

// This read model does not change the stored attendance or payroll interpretation.
internal static class HRDashboardDailyStatusProjection
{
	internal static readonly string[] Statuses =
		Enum.GetNames<HRDashboardDailyStatus>();

	internal static (HRDashboardDailyStatus Status, List<HRDashboardReviewFlagDto> Flags) Classify(
		EmployeeAttendanceStatusType? recordedStatus,
		bool fullDayLeave,
		bool halfDayLeave,
		bool? isWorkingDay,
		bool incompleteIntegratedPunch = false)
	{
		var flags = new List<HRDashboardReviewFlagDto>();
		if (fullDayLeave && recordedStatus is EmployeeAttendanceStatusType.Present or EmployeeAttendanceStatusType.HalfDayLeave)
			flags.Add(new(HRDashboardReviewFlagCode.FullDayLeaveConflict, "Attendance conflicts with approved full-day leave."));
		if (incompleteIntegratedPunch)
			flags.Add(new(HRDashboardReviewFlagCode.IncompletePunch, "Attendance has an incomplete punch."));
		if (!isWorkingDay.HasValue)
			flags.Add(new(HRDashboardReviewFlagCode.CalendarNeedsConfiguration, "Attendance calendar needs configuration."));
		if (isWorkingDay == false && recordedStatus is EmployeeAttendanceStatusType.Present or EmployeeAttendanceStatusType.HalfDayLeave)
			flags.Add(new(HRDashboardReviewFlagCode.AttendanceOnNonWorkingDay, "Attendance recorded on a non-working day."));

		// Incomplete integration data cannot establish absence or a completed half day.
		if (incompleteIntegratedPunch && recordedStatus != EmployeeAttendanceStatusType.Present)
			return (isWorkingDay == false ? HRDashboardDailyStatus.NotScheduled : fullDayLeave ? HRDashboardDailyStatus.OnLeave :
				isWorkingDay.HasValue ? HRDashboardDailyStatus.NotMarked : HRDashboardDailyStatus.NeedsSetup, flags);
		if (recordedStatus == EmployeeAttendanceStatusType.Present)
			return (HRDashboardDailyStatus.Present, flags);
		if (recordedStatus == EmployeeAttendanceStatusType.HalfDayLeave)
			return (HRDashboardDailyStatus.HalfDay, flags);
		if (isWorkingDay == false)
			return (HRDashboardDailyStatus.NotScheduled, flags);
		if (fullDayLeave)
			return (HRDashboardDailyStatus.OnLeave, flags);
		if (recordedStatus == EmployeeAttendanceStatusType.FullDayLeave)
			return (HRDashboardDailyStatus.Absent, flags);
		if (halfDayLeave)
			flags.Add(new(HRDashboardReviewFlagCode.RemainingHalfDayUnmarked, "Attendance for the remaining half day is not marked."));
		return (isWorkingDay.HasValue ? HRDashboardDailyStatus.NotMarked : HRDashboardDailyStatus.NeedsSetup, flags);
	}

	internal static HRDashboardDailyCountsDto Count(string date, IReadOnlyCollection<HRDashboardStaffDto> rows) =>
		Count(date, rows.Select(x => (x.Status, x.ReviewFlags.Count > 0)));

	internal static HRDashboardDailyCountsDto Count(
		string date, IEnumerable<(HRDashboardDailyStatus Status, bool NeedsReview)> rows)
	{
		var counts = new HRDashboardDailyCountsDto { Date = date };
		foreach (var (status, needsReview) in rows)
		{
			counts.StaffCount++;
			if (needsReview) counts.NeedsReview++;
			switch (status)
			{
				case HRDashboardDailyStatus.Present: counts.Present++; break;
				case HRDashboardDailyStatus.Absent: counts.Absent++; break;
				case HRDashboardDailyStatus.OnLeave: counts.OnLeave++; break;
				case HRDashboardDailyStatus.HalfDay: counts.HalfDay++; break;
				case HRDashboardDailyStatus.NotMarked: counts.NotMarked++; break;
				case HRDashboardDailyStatus.NotScheduled: counts.NotScheduled++; break;
				case HRDashboardDailyStatus.NeedsSetup: counts.NeedsSetup++; break;
			}
		}
		return counts;
	}

	internal static HRDashboardStaffPageDto Page(
		List<HRDashboardStaffDto> rows, HRDashboardStaffInput input, string date, DateTimeOffset generatedAt)
	{
		var filtered = rows.AsEnumerable();
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim();
			filtered = filtered.Where(x =>
				x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
				x.EmployeeCode.Contains(search, StringComparison.OrdinalIgnoreCase));
		}
		if (!string.IsNullOrWhiteSpace(input.Status))
			filtered = filtered.Where(x => x.Status.ToString() == input.Status);
		var ordered = input.Sorting switch
		{
			"name desc" => filtered.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase),
			"status" => filtered.OrderBy(x => x.Status).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
			"status desc" => filtered.OrderByDescending(x => x.Status).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
			_ => filtered.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
		};
		var all = ordered.ThenBy(x => x.EmployeeId).ToList();
		return new HRDashboardStaffPageDto
		{
			Date = date, GeneratedAt = generatedAt, TotalCount = all.Count,
			Items = all.Skip(input.SkipCount).Take(input.MaxResultCount).ToList()
		};
	}
}
