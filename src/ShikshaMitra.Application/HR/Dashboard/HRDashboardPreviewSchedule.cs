using System;

namespace ShikshaMitra.HR.Dashboard;

internal static class HRDashboardPreviewSchedule
{
	public static DateTime GetNextScheduledRefreshAt(DateTime now)
	{
		var nowUtc = now.Kind switch
		{
			DateTimeKind.Utc => now,
			DateTimeKind.Local => now.ToUniversalTime(),
			_ => DateTime.SpecifyKind(now, DateTimeKind.Utc)
		};
		var nowIst = ConvertUtcToIndiaTime(nowUtc);
		var nextIst = nowIst.Date.AddHours(6);
		if (nowIst >= nextIst)
		{
			nextIst = nextIst.AddDays(1);
		}

		var nextUtc = DateTime.SpecifyKind(nextIst.AddHours(-5.5), DateTimeKind.Utc);
		return now.Kind == DateTimeKind.Local ? nextUtc.ToLocalTime() : DateTime.SpecifyKind(nextUtc, now.Kind);
	}

	private static DateTime ConvertUtcToIndiaTime(DateTime utcNow)
	{
		try
		{
			return TimeZoneInfo.ConvertTimeFromUtc(
				DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
				TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
		}
		catch (Exception) when (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
		{
			return TimeZoneInfo.ConvertTimeFromUtc(
				DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
				TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"));
		}
		catch
		{
			return DateTime.SpecifyKind(utcNow, DateTimeKind.Utc).AddHours(5.5);
		}
	}
}
