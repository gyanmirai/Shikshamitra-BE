using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using static ShikshaMitra.Academic.Notifications.HomeworkNotificationContent;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationSourceAdapter))]
public sealed class HomeworkPublicationSourceAdapter(HomeworkReminderRecordReader records, HomeworkReminderRoster roster,
	HomeworkNotificationTeachers teachers, ICurrentTenant tenant, ITenantStore tenants, IClock clock)
	: INotificationSourceAdapter, ITransientDependency
{
	internal const string EventKey = AcademicNotificationEventKeys.HomeworkPublished;
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { EventKey });

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken cancellationToken)
	{
		if (context.TenantId == Guid.Empty || context.TenantId != tenant.Id ||
			context.SourceType != HomeworkReminderSourceAdapter.SourceType || context.EventKey != EventKey || context.SourceId == Guid.Empty)
			return Unavailable();
		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		var record = await records.FindAsync(context.TenantId, context.SourceId, now, cancellationToken);
		if (record == null) return Unavailable();
		var schedules = record.Schedules.Where(x => x.DueAt > now).ToArray();
		if (schedules.Length == 0) return Unavailable();
		var schoolName = tenant.Name ?? (await tenants.FindAsync(context.TenantId))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();

		var divisions = schedules.Select(x => x.DivisionId).ToArray();
		var captured = context.IntentId == Guid.Empty ? null : context.AffectedRecipients.ToHashSet();
		var capturedStudents = captured?.Select(x => x.StudentId).OfType<Guid>().ToArray();
		var students = await roster.FindAsync(context.TenantId, record, divisions, capturedStudents, pendingOnly: false, cancellationToken);
		var staff = await teachers.FindAsync(context.TenantId, record, divisions, cancellationToken);
		if (students == null || staff == null || students.Count + staff.Count > 10000) return Unavailable();

		var common = Variables(record, schoolName);
		var currentStaff = staff.Where(x => captured == null || captured.Contains(x.Reference)).ToArray();
		var staffByDivision = currentStaff.ToLookup(x => x.DivisionId);
		var studentsByDivision = students.ToLookup(x => x.DivisionId);
		var slices = new List<NotificationSourceAudienceSliceDto>();
		var content = new List<NotificationSourceRecipientContentDto>();
		var classTeachers = staffByDivision[null].Select(x => x.Reference).ToArray();
		if (classTeachers.Length > 0) slices.Add(new(Scope(record), classTeachers));
		foreach (var schedule in schedules)
		{
			var learners = studentsByDivision[schedule.DivisionId].Select(x => new NotificationRecipientRefDto("Student", x.StudentId)).ToArray();
			var recipients = learners.Concat(staffByDivision[schedule.DivisionId].Select(x => x.Reference)).ToArray();
			if (recipients.Length == 0) continue;
			slices.Add(new(Scope(record, schedule.DivisionId), recipients));
			var variables = WithDeadline(common, schedule.DueAt);
			content.AddRange(learners.Select(x => new NotificationSourceRecipientContentDto(x, variables, Link)));
		}

		var deadlineByDivision = schedules.ToDictionary(x => x.DivisionId, x => x.DueAt);
		var firstDeadline = schedules.Min(x => x.DueAt);
		foreach (var group in currentStaff.GroupBy(x => x.Reference))
		{
			var deadline = group.Min(x => x.DivisionId is Guid divisionId ? deadlineByDivision[divisionId] : firstDeadline);
			content.Add(new(group.Key, WithDeadline(common, deadline), Link));
		}

		var revision = HomeworkPublicationEventCapture.Revision(record.Schedules.Select(x => x.DivisionId));
		var all = slices.SelectMany(x => x.Recipients).Distinct().ToArray();
		return new(true, null, revision, Link, common, all, schedules.Max(x => x.DueAt), slices, content);
	}
}
