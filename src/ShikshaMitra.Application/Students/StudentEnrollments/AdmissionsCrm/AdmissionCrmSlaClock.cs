using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public static class AdmissionCrmSlaClock
{
	public static double CalculateWorkingHours(DateTimeOffset from, DateTimeOffset to, AdmissionCrmSlaPolicyDto policy)
	{
		if (to <= from) return 0;
		if (policy.WorkingDayStartHour is < 0 or > 23 || policy.WorkingDayEndHour is < 1 or > 24 ||
			policy.WorkingDayEndHour <= policy.WorkingDayStartHour || policy.WorkingDays.Count == 0)
			throw new ArgumentException("Invalid admissions SLA working calendar.", nameof(policy));
		TimeZoneInfo timeZone;
		try { timeZone = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId); }
		catch { timeZone = TimeZoneInfo.Utc; }
		var localFrom = TimeZoneInfo.ConvertTime(from, timeZone);
		var localTo = TimeZoneInfo.ConvertTime(to, timeZone);
		var holidayDates = policy.Holidays.Select(x => x.Date).ToHashSet();
		var total = TimeSpan.Zero;
		for (var date = localFrom.Date; date <= localTo.Date; date = date.AddDays(1))
		{
			if (!policy.WorkingDays.Contains(date.DayOfWeek) || holidayDates.Contains(date)) continue;
			var localStart = DateTime.SpecifyKind(date.AddHours(policy.WorkingDayStartHour), DateTimeKind.Unspecified);
			var localEnd = DateTime.SpecifyKind(date.AddHours(policy.WorkingDayEndHour), DateTimeKind.Unspecified);
			var dayStart = new DateTimeOffset(localStart, timeZone.GetUtcOffset(localStart));
			var dayEnd = new DateTimeOffset(localEnd, timeZone.GetUtcOffset(localEnd));
			var overlapStart = dayStart > from ? dayStart : from;
			var overlapEnd = dayEnd < to ? dayEnd : to;
			if (overlapEnd > overlapStart) total += overlapEnd - overlapStart;
		}
		return total.TotalHours;
	}
}
