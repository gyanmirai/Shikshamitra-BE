using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;

namespace ShikshaMitra.HR.Policies;

public static class WorkCalendarPolicyChangeDetector
{
	public static bool IsUnchanged(
		WorkCalendarPolicy entity,
		CreateUpdateWorkCalendarPolicyDto input,
		IReadOnlyList<WorkCalendarHolidayDto> existingHolidays,
		HrPolicyStatus status,
		AttendanceWeeklyOffPattern weeklyOffPattern,
		WorkCalendarHolidaySyncMode holidaySyncMode)
	{
		return entity.AcademicYearId == input.AcademicYearId &&
			string.Equals(entity.Name, input.Name.Trim(), StringComparison.Ordinal) &&
			entity.Status == status &&
			NullableDatesEqual(entity.EffectiveFrom, input.EffectiveFrom) &&
			NullableDatesEqual(entity.EffectiveTo, input.EffectiveTo) &&
			entity.WeeklyOffPattern == weeklyOffPattern &&
			IntListsEqual(entity.GetCustomWeeklyOffs(), NormalizeWeeklyOffs(input.CustomWeeklyOffs)) &&
			entity.HolidaySyncMode == holidaySyncMode &&
			HolidaysEqual(existingHolidays, input.Holidays);
	}

	public static DateOnly ResolveHolidayFromDate(WorkCalendarHolidayDto row) =>
		row.FromDate ?? row.Date;

	public static DateOnly ResolveHolidayToDate(WorkCalendarHolidayDto row) =>
		row.ToDate ?? row.FromDate ?? row.Date;

	private static IReadOnlyList<int> NormalizeWeeklyOffs(IReadOnlyList<int>? values) =>
		values?.Distinct().OrderBy(x => x).ToList() ?? [];

	private static bool NullableDatesEqual(DateTime? left, DateTime? right) =>
		left?.Date == right?.Date;

	private static bool IntListsEqual(IReadOnlyList<int> left, IReadOnlyList<int> right) =>
		left.Count == right.Count && left.Zip(right).All(pair => pair.First == pair.Second);

	private static bool HolidaysEqual(
		IReadOnlyList<WorkCalendarHolidayDto> left,
		IReadOnlyList<WorkCalendarHolidayDto>? right)
	{
		var leftRows = NormalizeHolidayRows(left);
		var rightRows = NormalizeHolidayRows(right ?? []);
		return leftRows.Count == rightRows.Count && leftRows.Zip(rightRows).All(pair =>
			pair.First.AcademicYearId == pair.Second.AcademicYearId &&
			pair.First.FromDate == pair.Second.FromDate &&
			pair.First.ToDate == pair.Second.ToDate &&
			string.Equals(pair.First.HolidayName, pair.Second.HolidayName, StringComparison.Ordinal) &&
			string.Equals(pair.First.Source, pair.Second.Source, StringComparison.Ordinal) &&
			pair.First.CalendarHolidayId == pair.Second.CalendarHolidayId &&
			pair.First.IsIncluded == pair.Second.IsIncluded &&
			string.Equals(pair.First.Notes, pair.Second.Notes, StringComparison.Ordinal));
	}

	private static List<NormalizedHolidayRow> NormalizeHolidayRows(IReadOnlyList<WorkCalendarHolidayDto> rows) =>
		rows
			.Select(row => new NormalizedHolidayRow(
				row.AcademicYearId,
				ResolveHolidayFromDate(row),
				ResolveHolidayToDate(row),
				row.HolidayName.Trim(),
				row.Source,
				row.CalendarHolidayId,
				row.IsIncluded,
				row.Notes?.Trim() ?? string.Empty))
			.OrderBy(row => row.FromDate)
			.ThenBy(row => row.ToDate)
			.ThenBy(row => row.HolidayName)
			.ThenBy(row => row.Source)
			.ThenBy(row => row.CalendarHolidayId)
			.ToList();

	private sealed record NormalizedHolidayRow(
		Guid AcademicYearId,
		DateOnly FromDate,
		DateOnly ToDate,
		string HolidayName,
		string Source,
		Guid? CalendarHolidayId,
		bool IsIncluded,
		string Notes);
}
