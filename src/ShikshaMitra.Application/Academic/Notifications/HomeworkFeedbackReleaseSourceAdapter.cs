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
public sealed class HomeworkFeedbackReleaseSourceAdapter(IRepository<StudentSubmissionEntity, Guid> submissions,
	LearningTaskNotificationRecordReader records, LearningTaskNotificationRoster roster, IAsyncQueryableExecuter queries,
	ICurrentTenant tenant, ITenantStore tenants, IClock clock) : INotificationSourceAdapter, ITransientDependency
{
	internal const string EventKey = AcademicNotificationEventKeys.HomeworkFeedbackReleased;
	internal const string ReleaseRevision = "first-feedback-release";
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { EventKey });

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken cancellationToken)
	{
		if (context.TenantId == Guid.Empty || context.TenantId != tenant.Id || context.SourceId == Guid.Empty ||
			context.EventKey != EventKey || context.SourceType != LearningTaskReminderSourceAdapter.HomeworkSourceType)
			return Unavailable();

		var submission = await queries.SingleOrDefaultAsync((await submissions.GetQueryableAsync()).Where(x =>
			x.TenantId == context.TenantId && x.Id == context.SourceId && !x.IsDeleted && x.AssignmentId == null &&
			x.Status == SubmissionStatus.Reviewed && (x.ReviewedAt.HasValue || x.GradedDate.HasValue))
			.Select(x => new { x.HomeworkAssignmentId, x.StudentId, x.DivisionId }), cancellationToken);
		if (submission?.HomeworkAssignmentId is not Guid homeworkId) return Unavailable();

		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		var record = await records.FindHomeworkAsync(context.TenantId, homeworkId, now, cancellationToken, includeClosed: true);
		var schedule = record?.Schedules.SingleOrDefault(x => x.DivisionId == submission.DivisionId);
		if (record == null || schedule == null) return Unavailable();
		var eligible = await roster.FindAsync(context.TenantId, record, [submission.DivisionId], [submission.StudentId],
			pendingOnly: false, cancellationToken);
		if (eligible?.Count != 1) return Unavailable();

		var schoolName = tenant.Name ?? (await tenants.FindAsync(context.TenantId))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();

		var variables = WithDeadline(Variables(record, schoolName), schedule.DueAt);
		var recipients = new[] { new NotificationRecipientRefDto("Student", submission.StudentId) };
		return new(true, null, ReleaseRevision, Link, variables, recipients,
			AudienceSlices: [new(Scope(record, submission.DivisionId), recipients)]);
	}
}
