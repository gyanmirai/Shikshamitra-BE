using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationSourceAdapter))]
public sealed class LearningTaskReminderSourceAdapter(LearningTaskNotificationRecordReader records, LearningTaskNotificationRoster roster,
	HomeworkNotificationTeachers teachers, ICurrentTenant tenant, ITenantStore tenants, IClock clock)
	: INotificationSourceAdapter, ITransientDependency
{
	internal const string HomeworkSourceType = "Academic.Homework";
	internal const string AssignmentSourceType = "Academic.Assignment";
	internal const string HomeworkEventKey = AcademicNotificationEventKeys.HomeworkDue;
	internal const string AssignmentEventKey = AcademicNotificationEventKeys.AssignmentDue;
	internal static readonly TimeSpan OverdueLifetime = TimeSpan.FromDays(1);
	internal static IReadOnlyCollection<string> SupportedEvents { get; } = Array.AsReadOnly(new[]
	{
		HomeworkEventKey, AssignmentEventKey, AcademicNotificationEventKeys.HomeworkOverdue, AcademicNotificationEventKeys.AssignmentOverdue
	});

	public IReadOnlyCollection<string> EventKeys => SupportedEvents;

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken cancellationToken)
	{
		var isAssignment = context.EventKey is AssignmentEventKey or AcademicNotificationEventKeys.AssignmentOverdue;
		var isOverdue = context.EventKey is AcademicNotificationEventKeys.HomeworkOverdue or AcademicNotificationEventKeys.AssignmentOverdue;
		var expectedSource = isAssignment ? AssignmentSourceType : HomeworkSourceType;
		if (context.TenantId == Guid.Empty || context.TenantId != tenant.Id || context.SourceType != expectedSource ||
			!SupportedEvents.Contains(context.EventKey) || context.SourceId == Guid.Empty)
			return Unavailable();

		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		var record = isAssignment
			? await records.FindAssignmentAsync(context.TenantId, context.SourceId, now, cancellationToken)
			: await records.FindHomeworkAsync(context.TenantId, context.SourceId, now, cancellationToken);
		if (record == null || record.Task.SubmissionType == SubmissionType.NoSubmissionRequired) return Unavailable();
		var occurrence = context.DueOccurrenceAt;
		var schedules = record.Schedules.Where(x => (isOverdue
			? x.DueAt <= now && x.DueAt > now - OverdueLifetime && (!x.PublishedAt.HasValue || x.PublishedAt <= x.DueAt)
			: x.DueAt > now) &&
			(!occurrence.HasValue || x.DueAt == occurrence && Scope(record, x.DivisionId) == context.SourceScope)).ToArray();
		if (schedules.Length == 0) return Unavailable();
		var schoolName = tenant.Name ?? (await tenants.FindAsync(context.TenantId))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();

		var divisions = schedules.Select(x => x.DivisionId).ToArray();
		var captured = context.IntentId == Guid.Empty ? null : context.AffectedRecipients.ToHashSet();
		var includeTeachers = isOverdue && (captured == null || captured.Any(x => x.Kind == "AssignedTeacher"));
		var capturedStudents = includeTeachers ? null : captured?.Select(x => x.StudentId).OfType<Guid>().ToArray();
		var eligible = await roster.FindAsync(context.TenantId, record, divisions, capturedStudents, pendingOnly: true, cancellationToken);
		if (eligible == null) return Unavailable();
		IReadOnlyList<HomeworkNotificationTeacher> staff = [];
		if (includeTeachers && eligible.Count > 0)
		{
			var assigned = await teachers.FindAsync(context.TenantId, record, divisions, cancellationToken);
			if (assigned == null || eligible.Count + assigned.Count > 10000) return Unavailable();
			staff = assigned;
		}

		var revision = occurrence.HasValue
			? DueRevision(Scope(record, schedules[0].DivisionId), occurrence.Value)
			: Hash(new { record.Task.Title, record.Task.SubjectId, record.Task.SubmissionType, record.Placement, record.Subject, record.Schedules });
		var latestDeadline = schedules.Max(x => x.DueAt);
		var expiry = isOverdue ? latestDeadline + OverdueLifetime : latestDeadline;
		return BuildSnapshot(record, schedules, eligible, staff, captured, schoolName, revision, expiry, isOverdue, occurrence.HasValue);
	}

	private static NotificationSourceSnapshot BuildSnapshot(LearningTaskNotificationRecord record, LearningTaskNotificationSchedule[] schedules,
		IReadOnlyList<AcademicNotificationLearner> eligible, IReadOnlyList<HomeworkNotificationTeacher> staff,
		HashSet<NotificationRecipientRefDto>? captured, string schoolName, string revision, DateTimeOffset expiry,
		bool isOverdue, bool singleOccurrence)
	{
		var slices = new List<NotificationSourceAudienceSliceDto>();
		var content = new List<NotificationSourceRecipientContentDto>();
		var summaries = new Dictionary<NotificationRecipientRefDto, (int Missing, DateTimeOffset Deadline)>();
		var common = Variables(record, schoolName);
		var groups = eligible.ToLookup(x => x.DivisionId);
		var staffByDivision = staff.Where(x => captured == null || captured.Contains(x.Reference)).ToLookup(x => x.DivisionId);
		foreach (var schedule in schedules)
		{
			var pending = groups[schedule.DivisionId];
			var missing = pending.Count();
			if (missing == 0) continue;
			var students = pending.Select(x => new NotificationRecipientRefDto("Student", x.StudentId))
				.Where(x => captured == null || captured.Contains(x)).ToArray();
			var divisionTeachers = staffByDivision[schedule.DivisionId].Select(x => x.Reference);
			var classTeachers = staffByDivision[null].Select(x => x.Reference);
			var recipients = students.Concat(divisionTeachers).Concat(singleOccurrence ? classTeachers : []).ToArray();
			if (recipients.Length > 0) slices.Add(new(Scope(record, schedule.DivisionId), recipients));

			var variables = WithDeadline(common, schedule.DueAt);
			if (isOverdue) AddMissingCount(variables, 1);
			content.AddRange(students.Select(x => new NotificationSourceRecipientContentDto(x, variables, Link)));

			foreach (var teacher in divisionTeachers.Concat(classTeachers))
			{
				var total = summaries.GetValueOrDefault(teacher);
				var earliest = total.Missing == 0 || schedule.DueAt < total.Deadline ? schedule.DueAt : total.Deadline;
				summaries[teacher] = (total.Missing + missing, earliest);
			}
		}

		if (!singleOccurrence)
		{
			var classRecipients = staffByDivision[null].Select(x => x.Reference).Where(summaries.ContainsKey).ToArray();
			if (classRecipients.Length > 0) slices.Add(new(Scope(record), classRecipients));
		}
		foreach (var (teacher, summary) in summaries)
		{
			var variables = WithDeadline(common, summary.Deadline);
			AddMissingCount(variables, summary.Missing);
			content.Add(new(teacher, variables, Link));
		}
		var recipientsInScope = slices.SelectMany(x => x.Recipients).Distinct().ToArray();
		return new(true, null, revision, Link, common, recipientsInScope, expiry, slices, content);
	}

	private static void AddMissingCount(Dictionary<string, NotificationVariableValueDto> variables, int missing) =>
		variables["missing_submission_count"] = new(NotificationVariableType.Number, missing.ToString(CultureInfo.InvariantCulture));

	internal static string DueRevision(NotificationSourceScopeDto scope, DateTimeOffset dueAt) =>
		Hash(new { Scope = scope, DueAt = dueAt.ToUniversalTime() });

	private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
