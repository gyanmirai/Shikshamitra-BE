using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationSourceAdapter))]
public sealed class LearningTaskSubmissionDigestSourceAdapter(LearningTaskNotificationRecordReader records, LearningTaskNotificationRoster roster,
	HomeworkNotificationTeachers teachers, IRepository<StudentSubmissionEntity, Guid> submissions, IAsyncQueryableExecuter queries,
	LearningTaskSubmissionDigestTiming timing, NotificationSchoolTimeZone schoolZone, IClock clock, ICurrentTenant tenant, ITenantStore tenants)
	: INotificationSourceAdapter, ITransientDependency
{
	private sealed record ScopedWindow(LearningTaskNotificationSchedule Schedule, NotificationSourceScopeDto Scope, LearningTaskDigestWindow Window);

	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[]
	{
		AcademicNotificationEventKeys.HomeworkSubmitted, AcademicNotificationEventKeys.AssignmentSubmitted
	});

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken cancellationToken)
	{
		if (context.TenantId == Guid.Empty || context.TenantId != tenant.Id || context.SourceId == Guid.Empty ||
			!EventKeys.Contains(context.EventKey)) return Unavailable();
		var isAssignment = context.EventKey == AcademicNotificationEventKeys.AssignmentSubmitted;
		var sourceType = isAssignment ? LearningTaskReminderSourceAdapter.AssignmentSourceType : LearningTaskReminderSourceAdapter.HomeworkSourceType;
		if (context.SourceType != sourceType) return Unavailable();

		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		var record = isAssignment
			? await records.FindAssignmentAsync(context.TenantId, context.SourceId, now, cancellationToken, includeClosed: true)
			: await records.FindHomeworkAsync(context.TenantId, context.SourceId, now, cancellationToken, includeClosed: true);
		if (record == null || record.Task.SubmissionType == SubmissionType.NoSubmissionRequired) return Unavailable();

		var windows = await FindWindowsAsync(context, record, now, cancellationToken);
		if (windows.Count == 0) return Unavailable();
		var divisions = windows.Select(x => x.Schedule.DivisionId).ToArray();
		var students = await roster.FindAsync(context.TenantId, record, divisions, null, pendingOnly: false, cancellationToken);
		var staff = await teachers.FindAsync(context.TenantId, record, divisions, cancellationToken);
		if (students == null || staff == null) return Unavailable();
		var schoolName = tenant.Name ?? (await tenants.FindAsync(context.TenantId))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();

		var counts = await CountAsync(context, windows, students, isAssignment, cancellationToken);
		return BuildSnapshot(context, record, windows, staff, counts, schoolName);
	}

	private async Task<List<ScopedWindow>> FindWindowsAsync(NotificationIntentContext context, LearningTaskNotificationRecord record,
		DateTimeOffset now, CancellationToken cancellationToken)
	{
		var zone = await schoolZone.GetAsync(cancellationToken);
		var windows = new List<ScopedWindow>();
		foreach (var schedule in record.Schedules)
		{
			var scope = Scope(record, schedule.DivisionId);
			var automatic = context.DueOccurrenceAt.HasValue;
			if (automatic && context.SourceScope != scope) continue;
			var window = await timing.ResolveAsync(context.EventKey, scope, context.OccurredAt, zone, automatic, cancellationToken);
			if (window == null || window.ExpiresAt <= now || (automatic && context.DueOccurrenceAt != window.End)) continue;
			windows.Add(new(schedule, scope, window));
		}
		return windows;
	}

	private async Task<Dictionary<Guid, int>> CountAsync(NotificationIntentContext context, IReadOnlyList<ScopedWindow> windows,
		IReadOnlyList<LearningTaskNotificationLearner> students, bool isAssignment, CancellationToken cancellationToken)
	{
		var counts = new Dictionary<Guid, int>();
		var byDivision = students.ToLookup(x => x.DivisionId, x => x.StudentId);
		var query = (await submissions.GetQueryableAsync()).Where(x => isAssignment
			? x.AssignmentId == context.SourceId && !x.HomeworkAssignmentId.HasValue
			: x.HomeworkAssignmentId == context.SourceId && !x.AssignmentId.HasValue);
		foreach (var item in windows)
		{
			var division = item.Schedule.DivisionId;
			var studentIds = byDivision[division].ToArray();
			if (studentIds.Length == 0) continue;
			var from = item.Schedule.PublishedAt is { } publication && publication > item.Window.Start ? publication : item.Window.Start;
			if (from >= item.Window.End) continue;
			var facts = LearningTaskSubmissionFacts.InWindow(query, context.TenantId, from, item.Window.End)
				.Where(x => x.DivisionId == division && Enumerable.Contains(studentIds, x.StudentId));
			counts[division] = await queries.CountAsync(facts.Select(x => x.StudentId).Distinct(), cancellationToken);
		}
		return counts;
	}

	private static NotificationSourceSnapshot BuildSnapshot(NotificationIntentContext context, LearningTaskNotificationRecord record,
		IReadOnlyList<ScopedWindow> windows, IReadOnlyList<HomeworkNotificationTeacher> staff,
		IReadOnlyDictionary<Guid, int> counts, string schoolName)
	{
		var captured = context.IntentId == Guid.Empty ? null : context.AffectedRecipients.ToHashSet();
		var byDivision = staff.Where(x => captured == null || captured.Contains(x.Reference)).ToLookup(x => x.DivisionId, x => x.Reference);
		var slices = new List<NotificationSourceAudienceSliceDto>();
		var totals = new Dictionary<NotificationRecipientRefDto, (int Count, DateTimeOffset Deadline)>();
		foreach (var item in windows)
		{
			var count = counts.GetValueOrDefault(item.Schedule.DivisionId);
			if (count == 0) continue;
			var section = byDivision[item.Schedule.DivisionId];
			var recipients = (context.DueOccurrenceAt.HasValue ? section.Concat(byDivision[null]) : section).ToArray();
			if (recipients.Length > 0) slices.Add(new(item.Scope, recipients));
			foreach (var teacher in section.Concat(byDivision[null]))
			{
				var previous = totals.GetValueOrDefault(teacher);
				var earliest = previous.Count == 0 || item.Schedule.DueAt < previous.Deadline ? item.Schedule.DueAt : previous.Deadline;
				totals[teacher] = (previous.Count + count, earliest);
			}
		}
		if (!context.DueOccurrenceAt.HasValue)
		{
			var classTeachers = byDivision[null].Where(totals.ContainsKey).ToArray();
			if (classTeachers.Length > 0) slices.Add(new(Scope(record), classTeachers));
		}

		var common = Variables(record, schoolName);
		var content = totals.Select(row =>
		{
			var values = WithDeadline(common, row.Value.Deadline);
			values["submitted_count"] = new(NotificationVariableType.Number, row.Value.Count.ToString(CultureInfo.InvariantCulture));
			return new NotificationSourceRecipientContentDto(row.Key, values, Link);
		}).ToArray();
		var revision = context.DueOccurrenceAt.HasValue
			? LearningTaskSubmissionDigestTiming.Revision(windows[0].Scope, windows[0].Window)
			: LearningTaskSubmissionDigestTiming.Fingerprint(windows.Select(x => new { x.Scope, x.Window }).ToArray());
		var recipientsInScope = slices.SelectMany(x => x.Recipients).Distinct().ToArray();
		return new(true, null, revision, Link, common, recipientsInScope, windows.Max(x => x.Window.ExpiresAt), slices, content);
	}
}
