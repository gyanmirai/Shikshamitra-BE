using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.HR.Audit;

namespace ShikshaMitra.HR.Policies;

internal static class WorkCalendarPolicyAuditChangeBuilder
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static List<HrAuditChangedFieldDto> Build(
		WorkCalendarPolicyDto before,
		WorkCalendarPolicyDto after)
	{
		var changes = HrAuditWriter.Diff(
			before,
			after,
			"lastModificationTime",
			"lastModifierId",
			"holidays",
			"holidaySummary");

		if (!SummaryEquals(before.HolidaySummary, after.HolidaySummary))
		{
			changes.Add(new HrAuditChangedFieldDto
			{
				Field = "holidaySummary",
				OldValue = JsonSerializer.Serialize(before.HolidaySummary, JsonOptions),
				NewValue = JsonSerializer.Serialize(after.HolidaySummary, JsonOptions)
			});
		}

		var beforeByKey = IndexHolidays(before.Holidays);
		var afterByKey = IndexHolidays(after.Holidays);
		var changedKeys = beforeByKey.Keys
			.Union(afterByKey.Keys)
			.Where(key =>
				!beforeByKey.TryGetValue(key, out var previous) ||
				!afterByKey.TryGetValue(key, out var current) ||
				!string.Equals(
					JsonSerializer.Serialize(previous, JsonOptions),
					JsonSerializer.Serialize(current, JsonOptions),
					StringComparison.Ordinal))
			.OrderBy(key => key, StringComparer.Ordinal)
			.ToList();

		if (changedKeys.Count > 0)
		{
			changes.Add(new HrAuditChangedFieldDto
			{
				Field = "holidays",
				OldValue = JsonSerializer.Serialize(
					changedKeys.Where(beforeByKey.ContainsKey).Select(key => beforeByKey[key]),
					JsonOptions),
				NewValue = JsonSerializer.Serialize(
					changedKeys.Where(afterByKey.ContainsKey).Select(key => afterByKey[key]),
					JsonOptions)
			});
		}

		return changes
			.OrderBy(change => change.Field, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static Dictionary<string, object> IndexHolidays(IEnumerable<WorkCalendarHolidayDto> holidays) =>
		holidays
			.GroupBy(HolidayKey, StringComparer.Ordinal)
			.ToDictionary(
				group => group.Key,
				group => Canonicalize(group.Last()),
				StringComparer.Ordinal);

	private static string HolidayKey(WorkCalendarHolidayDto holiday)
	{
		if (holiday.CalendarHolidayId.HasValue)
		{
			return $"calendar:{holiday.CalendarHolidayId.Value:N}";
		}

		var from = holiday.FromDate ?? holiday.Date;
		var to = holiday.ToDate ?? holiday.Date;
		return $"value:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:{holiday.HolidayName.Trim().ToUpperInvariant()}:{holiday.Source.Trim().ToUpperInvariant()}";
	}

	private static object Canonicalize(WorkCalendarHolidayDto holiday) => new
	{
		holiday.HolidayName,
		Date = holiday.Date.ToString("yyyy-MM-dd"),
		FromDate = (holiday.FromDate ?? holiday.Date).ToString("yyyy-MM-dd"),
		ToDate = (holiday.ToDate ?? holiday.Date).ToString("yyyy-MM-dd"),
		holiday.DayCount,
		holiday.Source,
		holiday.IsIncluded,
		holiday.Notes
	};

	private static bool SummaryEquals(
		WorkCalendarHolidaySummaryDto left,
		WorkCalendarHolidaySummaryDto right) =>
		left.TotalCount == right.TotalCount &&
		left.IncludedCount == right.IncludedCount &&
		left.RemovedCount == right.RemovedCount &&
		left.ManualCount == right.ManualCount &&
		left.CentralCount == right.CentralCount;
}
