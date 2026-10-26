using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.HR.Policies;

internal static class HrPolicyProposalCollectionComparison
{
	public static bool Supports(string field) => field is "holidays" or "holidayOverrides" or "customWeeklyOffs" or "manualMonthDays";

	public static void Normalize(Dictionary<string, JsonElement> properties)
	{
		foreach (var key in properties.Keys.Where(Supports).ToList())
		{
			var value = properties[key];
			if (value.ValueKind != JsonValueKind.Array) continue;
			var rows = value.EnumerateArray().Select(row => Describe(key, row)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
			properties[key] = JsonSerializer.SerializeToElement(rows);
		}
	}

	public static string Display(JsonElement rows) => rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() > 0
		? string.Join("\n", rows.EnumerateArray().Select(x => x.GetString())) : "None";

	private static string Describe(string field, JsonElement row)
	{
		if (field == "customWeeklyOffs")
			return row.TryGetInt32(out var day) && day is >= 0 and <= 6
				? CultureInfo.InvariantCulture.DateTimeFormat.GetDayName((DayOfWeek) day) : row.ToString();
		if (row.ValueKind != JsonValueKind.Object) return row.ToString();
		string Read(string key) => row.TryGetProperty(key, out var property) && property.ValueKind != JsonValueKind.Null ? property.ToString() : string.Empty;
		string Date(string key) => Read(key).Split('T')[0];
		string Flag(string key, string yes, string no) => row.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.True ? yes : no;
		var reference = string.IsNullOrWhiteSpace(Read("calendarHolidayId")) ? string.Empty : $" | Calendar reference: {Read("calendarHolidayId")}";
		return field switch
		{
			"holidays" => $"{Read("holidayName")} | {(string.IsNullOrEmpty(Date("fromDate")) ? Date("date") : Date("fromDate"))} to {(string.IsNullOrEmpty(Date("toDate")) ? (string.IsNullOrEmpty(Date("fromDate")) ? Date("date") : Date("fromDate")) : Date("toDate"))} | {Flag("isIncluded", "Included", "Excluded")} | {HrPolicyVersionDisplayFormatter.ToDisplayText(Read("source"))}{reference} | Notes: {Read("notes")}",
			"holidayOverrides" => $"{Date("date")}: {Read("holidayName")} | {Flag("includedInPayroll", "Included in payroll", "Excluded from payroll")}{reference}",
			"manualMonthDays" => $"{Read("year")}-{Read("month")}: {Read("manualDays")} days",
			_ => row.ToString()
		};
	}
}