using System;
using System.Globalization;

namespace ShikshaMitra.HR.Payroll;

internal static class PayrollMonthNormalizer
{
	private static readonly string[] MonthFormats =
	[
		"yyyy-MM",
		"yyyy/M",
		"yyyy/MM",
		"MMMM-yyyy",
		"MMMM-yy",
		"MMMM yyyy",
		"MMM-yyyy",
		"MMM yyyy",
		"MM-yyyy",
		"M-yyyy",
		"MM/yyyy",
		"M/yyyy",
		"dd-MM-yyyy",
		"d-M-yyyy",
		"yyyy-MM-dd",
		"dd/MM/yyyy",
		"d/M/yyyy"
	];

	public static bool TryNormalizeToInternal(string? input, out string normalizedMonth)
	{
		normalizedMonth = string.Empty;
		if (string.IsNullOrWhiteSpace(input))
		{
			return false;
		}

		var trimmed = input.Trim();
		if (DateTime.TryParseExact(trimmed, MonthFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsedExact))
		{
			normalizedMonth = $"{parsedExact.Year:D4}-{parsedExact.Month:D2}";
			return true;
		}

		if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsedFlexible))
		{
			normalizedMonth = $"{parsedFlexible.Year:D4}-{parsedFlexible.Month:D2}";
			return true;
		}

		return false;
	}

	public static string? ToApiMonthYear(string? internalMonthYear)
	{
		if (string.IsNullOrWhiteSpace(internalMonthYear))
		{
			return null;
		}

		if (!DateTime.TryParseExact(
			$"{internalMonthYear}-01",
			"yyyy-MM-dd",
			CultureInfo.InvariantCulture,
			DateTimeStyles.None,
			out var monthDate))
		{
			return null;
		}

		return $"{monthDate.ToString("MMMM", CultureInfo.InvariantCulture).ToUpperInvariant()}-{monthDate.Year}";
	}
}
