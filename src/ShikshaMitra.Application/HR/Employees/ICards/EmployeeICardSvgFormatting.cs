using System;
using System.Net;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardSvgFormatting
{
	public static string Truncate(string value, int maxLength) =>
		value.Length <= maxLength ? value : value[..Math.Max(1, maxLength - 3)] + "...";

	public static string Escape(string value) =>
		WebUtility.HtmlEncode(value);

	public static string NormalizeColor(string value) =>
		value.StartsWith("#", StringComparison.Ordinal) ? value : $"#{value}";
}
