using System;

namespace ShikshaMitra.HR.Employees.Onboarding;

internal static class EmployeeApplicationInstitutionClock
{
	private const string InstitutionTimeZoneId = "Asia/Kolkata";
	private const string WindowsInstitutionTimeZoneId = "India Standard Time";

	public static DateOnly GetDate(DateTime value)
	{
		var utc = value.Kind switch
		{
			DateTimeKind.Utc => value,
			DateTimeKind.Local => value.ToUniversalTime(),
			_ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
		};
		TimeZoneInfo timeZone;
		try
		{
			timeZone = TimeZoneInfo.FindSystemTimeZoneById(InstitutionTimeZoneId);
		}
		catch (TimeZoneNotFoundException)
		{
			timeZone = TimeZoneInfo.FindSystemTimeZoneById(WindowsInstitutionTimeZoneId);
		}

		return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone));
	}
}
