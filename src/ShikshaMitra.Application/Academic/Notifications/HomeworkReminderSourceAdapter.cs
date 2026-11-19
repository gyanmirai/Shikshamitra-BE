using System;
using System.Collections.Generic;
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
using static ShikshaMitra.Academic.Notifications.HomeworkNotificationContent;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationSourceAdapter))]
public sealed class HomeworkReminderSourceAdapter(HomeworkReminderRecordReader records, HomeworkReminderRoster roster,
	ICurrentTenant tenant, ITenantStore tenants, IClock clock) : INotificationSourceAdapter, ITransientDependency
{
	internal const string SourceType = "Academic.Homework";
	internal const string EventKey = AcademicNotificationEventKeys.HomeworkDue;
	internal static IReadOnlyCollection<string> SupportedEvents { get; } = Array.AsReadOnly(new[] { EventKey });

	public IReadOnlyCollection<string> EventKeys => SupportedEvents;

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken cancellationToken)
	{
		if (context.TenantId == Guid.Empty || context.TenantId != tenant.Id || context.SourceType != SourceType ||
			context.EventKey != EventKey || context.SourceId == Guid.Empty)
			return Unavailable();

		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		var record = await records.FindAsync(context.TenantId, context.SourceId, now, cancellationToken);
		if (record == null || record.Task.SubmissionType == SubmissionType.NoSubmissionRequired) return Unavailable();
		var occurrence = context.DueOccurrenceAt;
		var upcoming = record.Schedules.Where(x => x.DueAt > now &&
			(!occurrence.HasValue || x.DueAt == occurrence && Scope(record, x.DivisionId) == context.SourceScope)).ToArray();
		if (upcoming.Length == 0) return Unavailable();
		var schoolName = tenant.Name ?? (await tenants.FindAsync(context.TenantId))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();

		var divisions = upcoming.Select(x => x.DivisionId).ToArray();
		var captured = context.IntentId == Guid.Empty ? null : context.AffectedRecipients.Select(x => x.StudentId).OfType<Guid>().ToArray();
		var eligible = await roster.FindAsync(context.TenantId, record, divisions, captured, pendingOnly: true, cancellationToken);
		if (eligible == null) return Unavailable();

		var allRecipients = new List<NotificationRecipientRefDto>();
		var slices = new List<NotificationSourceAudienceSliceDto>();
		var content = new List<NotificationSourceRecipientContentDto>();
		var common = Variables(record, schoolName);
		var groups = eligible.ToLookup(x => x.DivisionId);
		foreach (var schedule in upcoming)
		{
			var recipients = groups[schedule.DivisionId].Select(x => new NotificationRecipientRefDto("Student", x.StudentId)).ToArray();
			if (recipients.Length == 0) continue;
			var variables = WithDeadline(common, schedule.DueAt);
			allRecipients.AddRange(recipients);
			slices.Add(new(Scope(record, schedule.DivisionId), recipients));
			content.AddRange(recipients.Select(x => new NotificationSourceRecipientContentDto(x, variables, Link)));
		}

		var revision = occurrence.HasValue
			? DueRevision(Scope(record, upcoming[0].DivisionId), occurrence.Value)
			: Hash(new { record.Task.Title, record.Task.SubjectId, record.Task.SubmissionType, record.Placement, record.Subject, record.Schedules });
		return new(true, null, revision, Link, common, allRecipients, upcoming.Max(x => x.DueAt), slices, content);
	}

	internal static string DueRevision(NotificationSourceScopeDto scope, DateTimeOffset dueAt) =>
		Hash(new { Scope = scope, DueAt = dueAt.ToUniversalTime() });

	private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
