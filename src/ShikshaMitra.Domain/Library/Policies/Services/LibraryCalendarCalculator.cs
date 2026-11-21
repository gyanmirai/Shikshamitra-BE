using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Library.Policies;

public sealed record LibraryClosedDateRange(DateOnly From, DateOnly To);

public static class LibraryCalendarCalculator
{
	/// <summary>Loan days start after the issue date; closed days never consume an open-day period.</summary>
	public static DateOnly Calculate(DateOnly start, int days, LibraryDayCountingMode mode, bool moveToNextOpenDay,
		IReadOnlyCollection<DayOfWeek> openWeekdays, IReadOnlyCollection<LibraryClosedDateRange> closures)
	{
		if (start == default || days is < 0 or > 3660 || mode is not (LibraryDayCountingMode.CalendarDays or LibraryDayCountingMode.OpenDays))
			throw new BusinessException(LibraryErrorCodes.CalendarInvalid);
		if ((mode == LibraryDayCountingMode.OpenDays || moveToNextOpenDay) && openWeekdays.Count == 0)
			throw new BusinessException(LibraryErrorCodes.CalendarNoOpenDay, "Configure at least one open Library weekday before calculating school-day deadlines.");
		bool IsOpen(DateOnly date) => openWeekdays.Contains(date.DayOfWeek) && !closures.Any(x => date >= x.From && date <= x.To);
		var date = start;
		var remaining = days;
		if (mode == LibraryDayCountingMode.CalendarDays)
		{
			if (days > DateOnly.MaxValue.DayNumber - date.DayNumber) throw new BusinessException(LibraryErrorCodes.CalendarInvalid);
			date = date.AddDays(days);
		}
		else
		{
			for (var examined = 0; remaining > 0; examined++)
			{
				if (examined >= 36600 || date == DateOnly.MaxValue) throw new BusinessException(LibraryErrorCodes.CalendarNoOpenDay);
				date = date.AddDays(1);
				if (IsOpen(date)) remaining--;
			}
		}
		if (moveToNextOpenDay)
		{
			for (var examined = 0; !IsOpen(date); examined++)
			{
				if (examined >= 36600 || date == DateOnly.MaxValue) throw new BusinessException(LibraryErrorCodes.CalendarNoOpenDay);
				date = date.AddDays(1);
			}
		}
		return date;
	}

	public static DateOnly SchoolDate(DateTimeOffset instant, TimeZoneInfo timezone) =>
		DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, timezone).DateTime);
}
