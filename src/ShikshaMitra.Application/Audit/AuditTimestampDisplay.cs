using System;
using System.Globalization;

namespace ShikshaMitra.Audit;

public static class AuditTimestampDisplay
{
	private const string InstitutionTimeZoneId = "Asia/Kolkata";
	private const string WindowsInstitutionTimeZoneId = "India Standard Time";
	private const string DisplayFormat = "dd MMM yyyy, hh:mm tt";

	public static string FormatInstitutionTime(DateTime value)
	{
		var utc = NormalizeUtc(value);
		var institutionTime = TimeZoneInfo.ConvertTimeFromUtc(utc, GetInstitutionTimeZone());
		return institutionTime.ToString(DisplayFormat, CultureInfo.InvariantCulture);
	}

	public static DateTime NormalizeUtc(DateTime value) => value.Kind switch
	{
		DateTimeKind.Utc => value,
		DateTimeKind.Local => value.ToUniversalTime(),
		_ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
	};

	private static TimeZoneInfo GetInstitutionTimeZone()
	{
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(InstitutionTimeZoneId);
		}
		catch (TimeZoneNotFoundException)
		{
			return TimeZoneInfo.FindSystemTimeZoneById(WindowsInstitutionTimeZoneId);
		}
	}
}
