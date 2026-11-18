using System;
using System.Linq;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationTimingDecision(DateTimeOffset? DueAt, bool IsExpired);

public sealed class NotificationDeliveryTimingPolicy : ITransientDependency
{
	public NotificationTimingDecision Evaluate(DateTimeOffset notBefore, TimeZoneInfo schoolZone, bool quietHoursEnabled,
		TimeOnly quietStart, TimeOnly quietEnd, DateTimeOffset? expiresAt = null, bool bypassQuietHours = false)
	{
		ArgumentNullException.ThrowIfNull(schoolZone);
		var due = notBefore.ToUniversalTime();
		if (!quietHoursEnabled || bypassQuietHours) return Decision(due, expiresAt);
		if (quietStart == quietEnd) throw new ArgumentException("Quiet hours must have different start and end times.");
		for (var pass = 0; pass < 4; pass++)
		{
			var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(due, schoolZone).DateTime);
			var next = due;
			for (var day = -1; day <= 0; day++)
			{
				var startDate = date.AddDays(day);
				var endDate = quietStart > quietEnd ? startDate.AddDays(1) : startDate;
				var start = Boundary(startDate.ToDateTime(quietStart), schoolZone, later: false);
				var end = Boundary(endDate.ToDateTime(quietEnd), schoolZone, later: true);
				if (due >= start && due < end && end > next) next = end;
			}
			if (next == due) return Decision(due, expiresAt);
			due = next;
		}
		throw new ArgumentException("Quiet hours could not resolve to an available school time.");
	}

	private static NotificationTimingDecision Decision(DateTimeOffset due, DateTimeOffset? expiresAt) =>
		expiresAt.HasValue && due >= expiresAt.Value ? new(null, true) : new(due, false);

	private static DateTimeOffset Boundary(DateTime local, TimeZoneInfo zone, bool later)
	{
		// A skipped local minute must advance, while repeated quiet hours cover both occurrences.
		for (var minute = 0; zone.IsInvalidTime(local); minute++)
		{
			if (minute == 2880) throw new ArgumentException("The school timezone has no valid quiet-hour boundary.");
			local = local.AddMinutes(1);
		}
		if (zone.IsAmbiguousTime(local))
		{
			var offsets = zone.GetAmbiguousTimeOffsets(local);
			return new DateTimeOffset(local, later ? offsets.Min() : offsets.Max()).ToUniversalTime();
		}
		return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
	}
}
