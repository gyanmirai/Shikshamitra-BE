using System;

namespace ShikshaMitra.Students.Records;

public static class StudentDashboardPreviewSchedule
{
	public static DateTime GetNextScheduledRefreshAt(DateTime now)
	{
		var nowUtc = NormalizeToUtc(now);
		var nowIst = ConvertUtcToIndiaTime(nowUtc);
		var nextIst = nowIst.Date.AddHours(6);
		if (nowIst >= nextIst)
		{
			nextIst = nextIst.AddDays(1);
		}

		return ConvertIndiaTimeToInputKind(nextIst, now.Kind);
	}

	public static DateTime GetCurrentScheduledRefreshAt(DateTime now)
	{
		var nowUtc = NormalizeToUtc(now);
		var nowIst = ConvertUtcToIndiaTime(nowUtc);
		var currentIst = nowIst.Date.AddHours(6);
		if (nowIst < currentIst)
		{
			currentIst = currentIst.AddDays(-1);
		}

		return ConvertIndiaTimeToInputKind(currentIst, now.Kind);
	}

	public static bool IsScheduledWindowOpen(DateTime now)
	{
		var nowUtc = NormalizeToUtc(now);
		var nowIst = ConvertUtcToIndiaTime(nowUtc);
		return nowIst >= nowIst.Date.AddHours(6);
	}

	private static DateTime NormalizeToUtc(DateTime now) =>
		now.Kind switch
		{
			DateTimeKind.Utc => now,
			DateTimeKind.Local => now.ToUniversalTime(),
			_ => DateTime.SpecifyKind(now, DateTimeKind.Utc)
		};

	private static DateTime ConvertIndiaTimeToInputKind(DateTime indiaTime, DateTimeKind inputKind)
	{
		var utc = DateTime.SpecifyKind(indiaTime.AddHours(-5.5), DateTimeKind.Utc);
		return inputKind == DateTimeKind.Local ? utc.ToLocalTime() : DateTime.SpecifyKind(utc, inputKind);
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
