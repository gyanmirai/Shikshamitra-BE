using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShikshaMitra.HR.Reports;

internal static class HrReportRowFormatting
{
	public static Dictionary<string, string?> Row(params (string Key, string? Value)[] values) =>
		values.ToDictionary(x => x.Key, x => x.Value);

	public static string DisplayName(string? employeeName, string? firstName, string? lastName) =>
		!string.IsNullOrWhiteSpace(employeeName) ? employeeName : string.Join(" ", new[] { firstName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x)));

	public static string? FormatDate(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
	public static string? FormatDate(DateTime? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
	public static string? FormatDateTime(DateTime? date) => date?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
	public static string? FormatDateTime(DateTimeOffset? date) => date?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
	public static string FormatDecimal(decimal? value) => value?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
	public static string FormatMoney(decimal? value) => value?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;
}
