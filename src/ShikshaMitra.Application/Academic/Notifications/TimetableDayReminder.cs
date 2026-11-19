using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;
using TimetableEntity = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class TimetableDayReminder(AcademicNotificationWorkingDays calendar, NotificationSchoolTimeZone schoolZone,
	TimetableNotificationAudience audience, IClock clock) : ITransientDependency
{
	internal const string EventKey = AcademicNotificationEventKeys.TimetableDayReminder;

	internal async Task<NotificationDueSourceCandidate?> FindCandidateAsync(TimetableEntity timetable, TimetableNotificationScope scope,
		NotificationRuleTimingConfiguration timing, DateTimeOffset now, TimeZoneInfo zone, CancellationToken token)
	{
		var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
		var horizon = NotificationReminderTimingPolicy.MaximumLookbackDays;
		var first = DateOnly.FromDayNumber(Math.Max(DateOnly.MinValue.DayNumber, today.DayNumber - horizon));
		var last = DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber - 1, today.DayNumber + horizon));
		var days = await calendar.ReadWindowAsync(scope.RuleScope, first, last, token);
		var date = days == null ? null : FindDueSchoolDate(today, now, timing, zone, days);
		if (!date.HasValue) return null;

		var weekly = TimetableNotificationTransition.Schedule(timetable);
		var periods = TeachingPeriods(weekly, date.Value);
		if (periods.Length == 0) return null;
		var start = FirstPeriodAt(date.Value, periods, zone);
		return start <= now ? null : new(EventKey, timetable.Id, Revision(timetable, weekly), scope.Scope, start, start,
			NotificationDueTimeMode.PrecedingSchoolDays);
	}

	internal async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, TimetableEntity timetable,
		TimetableNotificationScope scope, string? schoolName, CancellationToken token)
	{
		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		if (timetable.Status != TimetableStatus.Published || string.IsNullOrWhiteSpace(schoolName) ||
			context.SourceScope != scope.Scope || context.DueOccurrenceAt is not { } occurrence || occurrence <= now)
			return Unavailable();
		var weekly = TimetableNotificationTransition.Schedule(timetable);
		var revision = Revision(timetable, weekly);
		if (context.SourceRevision != revision) return Unavailable();

		TimeZoneInfo zone;
		try { zone = await schoolZone.GetAsync(token); }
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { return Unavailable(); }
		var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(occurrence, zone).DateTime);
		var periods = TeachingPeriods(weekly, date);
		if (periods.Length == 0 || FirstPeriodAt(date, periods, zone) != occurrence) return Unavailable();
		var day = await calendar.ReadWindowAsync(scope.RuleScope, date, date, token);
		if (day == null || !NotificationReminderTimingPolicy.WorkingDayMatcher(day)(date)) return Unavailable();

		var learners = await audience.FindDailyLearnersAsync(context.TenantId, scope, TimetableNotificationTransition.Electives(weekly), token);
		if (learners == null) return Unavailable();
		var teachers = await audience.FindTeachersAsync(context.TenantId,
			periods.Select(x => x.TeacherId).OfType<Guid>().Distinct().ToArray(), token);
		var common = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["class_name"] = new(NotificationVariableType.Text, scope.ClassName),
			["session_date"] = new(NotificationVariableType.Date, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
		};
		var content = new List<NotificationSourceRecipientContentDto>();
		foreach (var learner in learners)
			AddPeriods(learner.Recipient, periods.Where(x => !x.GroupId.HasValue ||
				learner.Choices.Contains(new(x.GroupId.Value, x.SubjectId!.Value))));
		foreach (var teacher in teachers)
			AddPeriods(teacher, periods.Where(x => x.TeacherId.HasValue &&
				(x.TeacherId == teacher.EmployeeId || x.TeacherId == teacher.UserId)));

		var recipients = content.Select(x => x.Recipient).ToArray();
		return recipients.Length == 0 ? Unavailable() : new(true, null, revision, Link, common, recipients, occurrence,
			[new(scope.Scope, recipients)], content);

		void AddPeriods(NotificationRecipientRefDto recipient, IEnumerable<TimetableNotificationEntry> entries)
		{
			var times = entries.Select(x => (x.Start, x.End)).Distinct().OrderBy(x => x.Start).ThenBy(x => x.End).ToArray();
			if (times.Length == 0) return;
			var variables = new Dictionary<string, NotificationVariableValueDto>(common, StringComparer.Ordinal)
			{
				["period_count"] = new(NotificationVariableType.Number, times.Length.ToString(CultureInfo.InvariantCulture)),
				["period_times"] = new(NotificationVariableType.Text, string.Join(", ", times.Select(x =>
					$"{x.Start.ToString("HH:mm", CultureInfo.InvariantCulture)}–{x.End.ToString("HH:mm", CultureInfo.InvariantCulture)}")))
			};
			content.Add(new(recipient, variables, Link));
		}
	}

	internal static DateOnly? FindDueSchoolDate(DateOnly today, DateTimeOffset now, NotificationRuleTimingConfiguration timing,
		TimeZoneInfo zone, NotificationWorkingDays days)
	{
		var isWorkingDay = NotificationReminderTimingPolicy.WorkingDayMatcher(days);
		// Each scan targets the occurrence for the most recent school-local reminder cutoff.
		var anchor = today.DayNumber;
		var todayCutoff = NotificationDeliveryTimingPolicy.Boundary(today.ToDateTime(timing.ReminderLocalTime), zone, later: false);
		if (todayCutoff > now) anchor--;
		anchor = Math.Min(anchor, days.EndDate.DayNumber);
		while (anchor >= days.StartDate.DayNumber && !isWorkingDay(DateOnly.FromDayNumber(anchor))) anchor--;
		if (anchor < days.StartDate.DayNumber) return null;

		var remaining = timing.ReminderSchoolDaysBefore;
		var limit = Math.Min(days.EndDate.DayNumber, anchor + NotificationReminderTimingPolicy.MaximumLookbackDays);
		for (var index = anchor; index <= limit; index++)
		{
			var date = DateOnly.FromDayNumber(index);
			if (isWorkingDay(date) && remaining-- == 0) return date;
		}
		return null;
	}

	private static TimetableNotificationEntry[] TeachingPeriods(TimetableNotificationEntry[] weekly, DateOnly date) => weekly
		.Where(x => x.Day == (TimetableDayOfWeek)((int)date.DayOfWeek - 1) && x.Type == SlotType.Period &&
			x.SubjectId.HasValue && x.Start < x.End).ToArray();

	private static DateTimeOffset FirstPeriodAt(DateOnly date, TimetableNotificationEntry[] periods, TimeZoneInfo zone) =>
		NotificationDeliveryTimingPolicy.Boundary(date.ToDateTime(periods.Min(x => x.Start)), zone, later: false);

	private static string Revision(TimetableEntity timetable, TimetableNotificationEntry[] weekly) =>
		(TimetableNotificationTransition.Read(timetable)?.Revision ?? "legacy") + ":" + TimetableNotificationTransition.Fingerprint(weekly);
}
