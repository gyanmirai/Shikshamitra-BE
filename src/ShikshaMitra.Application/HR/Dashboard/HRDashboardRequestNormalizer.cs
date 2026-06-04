using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Volo.Abp.Validation;

namespace ShikshaMitra.HR.Dashboard;

internal static class HRDashboardRequestNormalizer
{
	public const int DefaultRangeDays = 7;
	public const int MaxRangeDays = 31;

	public static DateTime ParseDateOrDefault(string? value, DateTime fallback)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return fallback;
		}

		if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
		{
			return parsed.Date;
		}

		throw new AbpValidationException(
			"Dashboard date is invalid.",
			[new ValidationResult("Date must be yyyy-MM-dd.", [nameof(HRDashboardSummaryRequestDto.Date)])]);
	}

	public static string NormalizeMonth(string? value, DateTime fallbackDate)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return fallbackDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
		}

		if (DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
		{
			return parsed.ToString("yyyy-MM", CultureInfo.InvariantCulture);
		}

		throw new AbpValidationException(
			"Dashboard payroll month is invalid.",
			[new ValidationResult("Month must be yyyy-MM.", [nameof(HRDashboardSummaryRequestDto.Month)])]);
	}
}
