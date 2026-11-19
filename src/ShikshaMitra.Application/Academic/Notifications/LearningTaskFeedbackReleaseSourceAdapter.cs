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
public sealed class LearningTaskFeedbackReleaseSourceAdapter(IRepository<StudentSubmissionEntity, Guid> submissions,
	LearningTaskNotificationRecordReader records, LearningTaskNotificationRoster roster, IAsyncQueryableExecuter queries,
	ICurrentTenant tenant, ITenantStore tenants, IClock clock) : INotificationSourceAdapter, ITransientDependency
{
	internal const string HomeworkEventKey = AcademicNotificationEventKeys.HomeworkFeedbackReleased;
	internal const string AssignmentEventKey = AcademicNotificationEventKeys.AssignmentFeedbackReleased;
	internal const string ReleaseRevision = "first-feedback-release";
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { HomeworkEventKey, AssignmentEventKey });

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken cancellationToken)
	{
		var isAssignment = context.EventKey == AssignmentEventKey && context.SourceType == LearningTaskReminderSourceAdapter.AssignmentSourceType;
		var isHomework = context.EventKey == HomeworkEventKey && context.SourceType == LearningTaskReminderSourceAdapter.HomeworkSourceType;
		if (context.TenantId == Guid.Empty || context.TenantId != tenant.Id || context.SourceId == Guid.Empty || (!isAssignment && !isHomework))
			return Unavailable();

		var submission = await queries.SingleOrDefaultAsync((await submissions.GetQueryableAsync()).Where(x =>
			x.TenantId == context.TenantId && x.Id == context.SourceId && !x.IsDeleted &&
			(isAssignment ? x.AssignmentId.HasValue && !x.HomeworkAssignmentId.HasValue : x.HomeworkAssignmentId.HasValue && !x.AssignmentId.HasValue) &&
			x.Status == SubmissionStatus.Reviewed && (x.ReviewedAt.HasValue || x.GradedDate.HasValue))
			.Select(x => new { TaskId = x.AssignmentId ?? x.HomeworkAssignmentId, x.StudentId, x.DivisionId }), cancellationToken);
		if (submission?.TaskId is not Guid taskId) return Unavailable();

		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		var record = isAssignment
			? await records.FindAssignmentAsync(context.TenantId, taskId, now, cancellationToken, includeClosed: true)
			: await records.FindHomeworkAsync(context.TenantId, taskId, now, cancellationToken, includeClosed: true);
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
