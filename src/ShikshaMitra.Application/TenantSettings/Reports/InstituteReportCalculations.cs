using System;
using System.Globalization;

namespace ShikshaMitra.TenantSettings.Reports;

public static class InstituteReportCalculations
{
	public static string ShiftTotalHours(TimeOnly start, TimeOnly end)
	{
		var ticks = end.Ticks - start.Ticks;
		// An earlier end time belongs to the next day; equal times describe zero elapsed hours.
		if (ticks < 0) ticks += TimeSpan.TicksPerDay;
		var hours = Math.Round((decimal)ticks / TimeSpan.TicksPerHour, 2, MidpointRounding.AwayFromZero);
		return hours.ToString("0.##", CultureInfo.InvariantCulture);
	}
}
