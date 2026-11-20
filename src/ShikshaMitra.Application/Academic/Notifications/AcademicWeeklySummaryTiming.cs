using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record AcademicWeeklySummaryWindow(LearningTaskDigestWindow Period, int Week)
{
	internal string Revision => FormattableString.Invariant($"{Week}:{Period.Start.UtcTicks}:{Period.End.UtcTicks}:{Period.ExpiresAt.UtcTicks}");

	internal static AcademicWeeklySummaryWindow? Read(string revision)
	{
		var parts = revision.Split(':');
		if (parts.Length != 4 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var week) ||
			week is < 100101 or > 999953 || week % 100 is < 1 or > 53 ||
			!ReadUtc(parts[1], out var start) || !ReadUtc(parts[2], out var end) || !ReadUtc(parts[3], out var expires) ||
			end - start < TimeSpan.FromDays(6) || end - start > TimeSpan.FromDays(8) ||
			expires <= end || expires - end > TimeSpan.FromDays(2)) return null;
		return new(new(start, end, expires), week);

		static bool ReadUtc(string value, out DateTimeOffset instant)
		{
			instant = default;
			if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
				ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks) return false;
			instant = new(ticks, TimeSpan.Zero);
			return true;
		}
	}
}

[RemoteService(false)]
public sealed class AcademicWeeklySummaryTiming(NotificationEffectiveRuleResolver rules, NotificationRuleConfigurationCodec codec,
	NotificationSchoolTimeZone schoolZone) : ITransientDependency
{
	internal const string EventKey = AcademicNotificationEventKeys.SummaryWeekly;
	internal const string SourceType = "Academic.Summary";

	internal async Task<AcademicWeeklySummaryWindow?> ResolveAsync(NotificationSourceScopeDto scope, DateTimeOffset at, CancellationToken token)
	{
		var rule = await rules.ResolveAsync(EventKey,
			NotificationRuleScope.Create(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId), token);
		if (rule is not { IsEnabled: true }) return null;
		try
		{
			var timing = codec.Deserialize(rule.ConfigurationJson, AcademicNotificationCatalogue.GetDefinition(EventKey)).Timing;
			var zone = await schoolZone.GetAsync(token);
			if (ClosedWindow(at, zone, timing.WeeklySummaryDay ?? DayOfWeek.Friday, timing.ReminderLocalTime) is not { } window)
				return null;
			var localEnd = TimeZoneInfo.ConvertTime(window.End, zone).DateTime;
			return new(window, ISOWeek.GetYear(localEnd) * 100 + ISOWeek.GetWeekOfYear(localEnd));
		}
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { return null; }
	}

	internal static LearningTaskDigestWindow? ClosedWindow(DateTimeOffset at, TimeZoneInfo zone, DayOfWeek day, TimeOnly cutoff)
	{
		var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);
		var daysBack = ((int)localDate.DayOfWeek - (int)day + 7) % 7;
		var date = localDate.AddDays(-daysBack);
		var end = Boundary(date);
		var expires = Boundary(date.AddDays(1));
		return at >= end && at < expires ? new(Boundary(date.AddDays(-7)), end, expires) : null;

		DateTimeOffset Boundary(DateOnly value) => NotificationDeliveryTimingPolicy.Boundary(value.ToDateTime(cutoff), zone, later: false);
	}
}
