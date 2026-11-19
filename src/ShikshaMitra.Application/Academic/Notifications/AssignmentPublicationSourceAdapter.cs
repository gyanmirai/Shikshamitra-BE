using System;
using System.Collections.Generic;
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
public sealed class AssignmentPublicationSourceAdapter(IRepository<AssignmentDivisionSchedule, Guid> schedules,
	LearningTaskNotificationRecordReader records, LearningTaskNotificationRoster roster, HomeworkNotificationTeachers teachers,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant, ITenantStore tenants, IClock clock)
	: INotificationSourceAdapter, ITransientDependency
{
	internal const string EventKey = AcademicNotificationEventKeys.AssignmentPublished;
	internal const string Revision = "published";

	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { EventKey });

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken cancellationToken)
	{
		if (context.TenantId == Guid.Empty || context.TenantId != tenant.Id ||
			context.SourceType != LearningTaskReminderSourceAdapter.AssignmentSourceType || context.EventKey != EventKey ||
			context.SourceId == Guid.Empty) return Unavailable();

		var occurrence = await queries.SingleOrDefaultAsync((await schedules.GetQueryableAsync())
			.Where(x => x.Id == context.SourceId && x.TenantId == context.TenantId && x.IsPublished && x.PublishedAt.HasValue &&
				x.Assignment.TenantId == context.TenantId && x.Assignment.Status == AssignmentStatus.Published)
			.Select(x => new { x.AssignmentId, x.DivisionId }), cancellationToken);
		if (occurrence == null) return Unavailable();

		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		var record = await records.FindAssignmentAsync(context.TenantId, occurrence.AssignmentId, now, cancellationToken);
		var schedule = record?.Schedules.SingleOrDefault(x => x.DivisionId == occurrence.DivisionId);
		if (record == null || schedule == null || schedule.DueAt <= now) return Unavailable();

		var schoolName = tenant.Name ?? (await tenants.FindAsync(context.TenantId))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();

		var captured = context.IntentId == Guid.Empty ? null : context.AffectedRecipients.ToHashSet();
		var capturedStudents = captured?.Select(x => x.StudentId).OfType<Guid>().ToArray();
		Guid[] divisionIds = [occurrence.DivisionId];
		var students = await roster.FindAsync(context.TenantId, record, divisionIds, capturedStudents, pendingOnly: false, cancellationToken);
		var staff = await teachers.FindAsync(context.TenantId, record, divisionIds, cancellationToken);
		if (students == null || staff == null || students.Count + staff.Count > 10000) return Unavailable();

		var slices = new List<NotificationSourceAudienceSliceDto>();
		var currentStaff = staff.Where(x => captured == null || captured.Contains(x.Reference)).ToArray();
		var divisionRecipients = students.Select(x => new NotificationRecipientRefDto("Student", x.StudentId))
			.Concat(currentStaff.Where(x => x.DivisionId.HasValue).Select(x => x.Reference)).ToArray();
		if (divisionRecipients.Length > 0) slices.Add(new(Scope(record, occurrence.DivisionId), divisionRecipients));

		// Class-wide teachers receive the first release even when later divisions publish independently.
		var firstPublication = record.Schedules.OrderBy(x => x.PublishedAt).ThenBy(x => x.DivisionId).First();
		if (firstPublication.DivisionId == occurrence.DivisionId)
		{
			var classRecipients = currentStaff.Where(x => !x.DivisionId.HasValue).Select(x => x.Reference).ToArray();
			if (classRecipients.Length > 0) slices.Add(new(Scope(record), classRecipients));
		}

		var recipients = slices.SelectMany(x => x.Recipients).Distinct().ToArray();
		return new(true, null, Revision, Link, WithDeadline(Variables(record, schoolName), schedule.DueAt),
			recipients, schedule.DueAt, slices);
	}
}
