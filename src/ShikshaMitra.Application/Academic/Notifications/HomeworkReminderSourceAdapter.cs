using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationSourceAdapter))]
public sealed class HomeworkReminderSourceAdapter(HomeworkReminderRecordReader records, HomeworkReminderRoster roster,
	ICurrentTenant tenant, ITenantStore tenants, IClock clock) : INotificationSourceAdapter, ITransientDependency
{
	internal const string SourceType = "Academic.Homework";
	internal const string EventKey = AcademicNotificationEventKeys.HomeworkDue;
	internal static IReadOnlyCollection<string> SupportedEvents { get; } = Array.AsReadOnly(new[] { EventKey });
	private const string Link = "/dashboard?openNotifications=1";

	public IReadOnlyCollection<string> EventKeys => SupportedEvents;

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken cancellationToken)
	{
		if (context.TenantId == Guid.Empty || context.TenantId != tenant.Id || context.SourceType != SourceType ||
			context.EventKey != EventKey || context.SourceId == Guid.Empty)
			return Unavailable();

		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		var record = await records.FindAsync(context.TenantId, context.SourceId, now, cancellationToken);
		if (record == null) return Unavailable();
		var upcoming = record.Schedules.Where(x => x.DueAt > now).ToArray();
		if (upcoming.Length == 0) return Unavailable();
		var schoolName = tenant.Name ?? (await tenants.FindAsync(context.TenantId))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();

		var divisions = upcoming.Select(x => x.DivisionId).ToArray();
		var captured = context.IntentId == Guid.Empty ? null : context.AffectedRecipients.Select(x => x.StudentId).OfType<Guid>().ToArray();
		var eligible = await roster.FindPendingAsync(context.TenantId, record, divisions, captured, cancellationToken);
		if (eligible == null) return Unavailable();

		var placements = record.Placement;
		var allRecipients = new List<NotificationRecipientRefDto>();
		var slices = new List<NotificationSourceAudienceSliceDto>();
		var content = new List<NotificationSourceRecipientContentDto>();
		var common = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = Text(schoolName), ["class_name"] = Text(placements.ClassName),
			["subject_name"] = Text(record.Subject.Name), ["task_title"] = Text(record.Task.Title),
		};
		var groups = eligible.ToLookup(x => x.DivisionId);
		foreach (var schedule in upcoming)
		{
			var recipients = groups[schedule.DivisionId].Select(x => new NotificationRecipientRefDto("Student", x.StudentId)).ToArray();
			if (recipients.Length == 0) continue;
			var variables = new Dictionary<string, NotificationVariableValueDto>(common, StringComparer.Ordinal)
			{
				["due_date"] = new(NotificationVariableType.DateTime, schedule.DueAt.ToString("O", CultureInfo.InvariantCulture)),
			};
			allRecipients.AddRange(recipients);
			slices.Add(new(new(placements.YearId, placements.BoardId, placements.MediumId, record.Task.StandardId, schedule.DivisionId), recipients));
			content.AddRange(recipients.Select(x => new NotificationSourceRecipientContentDto(x, variables, Link)));
		}

		var revision = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
		{
			record.Task.Title, record.Task.SubjectId, record.Task.SubmissionType, record.Placement, record.Subject, record.Schedules
		})));
		return new(true, null, revision, Link, common, allRecipients, upcoming.Max(x => x.DueAt), slices, content);
	}

	private static NotificationVariableValueDto Text(string value) => new(NotificationVariableType.Text, value);
	private static NotificationSourceSnapshot Unavailable() => new(false, NotificationErrorCodes.SourceUnavailable, "", null,
		new Dictionary<string, NotificationVariableValueDto>(), []);
}
