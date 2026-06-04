using System;
using System.Globalization;
using System.Linq;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

internal static class SalarySlipProjectionFormatting
{
	public static string? FormatDate(DateTime? date) =>
		date?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

	public static string? FormatDate(DateOnly? date) =>
		date?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

	public static string FormatPayPeriod(string monthYear)
	{
		if (DateTime.TryParseExact(monthYear + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
			    DateTimeStyles.None, out var date))
		{
			return date.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
		}

		return monthYear;
	}

	public static string FormatDayCalculationMode(string? mode) =>
		mode switch
		{
			"working-days" => "Working days",
			"total-month-days" => "Calendar days",
			"manual-days" => "Manual days",
			null or "" => "-",
			_ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(mode.Replace("-", " ", StringComparison.Ordinal))
		};

	public static string? MaskAccountNumber(string? accountNumber)
	{
		if (string.IsNullOrWhiteSpace(accountNumber))
		{
			return null;
		}

		var digits = new string(accountNumber.Where(char.IsLetterOrDigit).ToArray());
		if (digits.Length <= 4)
		{
			return $"XXXX{digits}";
		}

		return $"XXXX{digits[^4..]}";
	}
}
