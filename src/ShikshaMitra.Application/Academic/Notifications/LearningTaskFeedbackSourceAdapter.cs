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
public sealed class LearningTaskFeedbackSourceAdapter(IRepository<StudentSubmissionEntity, Guid> submissions,
	LearningTaskNotificationRecordReader records, LearningTaskNotificationRoster roster, IAsyncQueryableExecuter queries,
	ICurrentTenant tenant, ITenantStore tenants, IClock clock) : INotificationSourceAdapter, ITransientDependency
{
	internal const string ReleaseRevision = "first-feedback-release";
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[]
	{
		AcademicNotificationEventKeys.HomeworkFeedbackReleased, AcademicNotificationEventKeys.HomeworkRevisionRequested,
		AcademicNotificationEventKeys.AssignmentFeedbackReleased, AcademicNotificationEventKeys.AssignmentRevisionRequested
	});

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken cancellationToken)
	{
		var isAssignment = context.EventKey is AcademicNotificationEventKeys.AssignmentFeedbackReleased or AcademicNotificationEventKeys.AssignmentRevisionRequested;
		var sourceType = isAssignment ? LearningTaskReminderSourceAdapter.AssignmentSourceType : LearningTaskReminderSourceAdapter.HomeworkSourceType;
		if (!EventKeys.Contains(context.EventKey) || context.SourceType != sourceType ||
			context.TenantId == Guid.Empty || context.TenantId != tenant.Id || context.SourceId == Guid.Empty)
			return Unavailable();
		var revisionRequested = context.EventKey is AcademicNotificationEventKeys.AssignmentRevisionRequested or AcademicNotificationEventKeys.HomeworkRevisionRequested;

		var submission = await queries.SingleOrDefaultAsync((await submissions.GetQueryableAsync()).Where(x =>
			x.TenantId == context.TenantId && x.Id == context.SourceId && !x.IsDeleted &&
			(isAssignment ? x.AssignmentId.HasValue && !x.HomeworkAssignmentId.HasValue : x.HomeworkAssignmentId.HasValue && !x.AssignmentId.HasValue) &&
			(revisionRequested
				? x.Status == SubmissionStatus.NeedsReview && x.SubmissionDate.HasValue && x.ReviewedAt.HasValue &&
					x.Remarks != null && x.Remarks.Trim() != ""
				: x.Status == SubmissionStatus.Reviewed && (x.ReviewedAt.HasValue || x.GradedDate.HasValue)))
			.Select(x => new { TaskId = x.AssignmentId ?? x.HomeworkAssignmentId, x.StudentId, x.DivisionId,
				x.SubmissionDate, x.MobileUpdatedAt, x.ReviewedAt }), cancellationToken);
		if (submission?.TaskId is not Guid taskId) return Unavailable();
		var revision = revisionRequested ? RevisionRequestVersion(submission.SubmissionDate, submission.MobileUpdatedAt, submission.ReviewedAt) : ReleaseRevision;
		if (revision == null) return Unavailable();

		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		var record = isAssignment
			? await records.FindAssignmentAsync(context.TenantId, taskId, now, cancellationToken, includeClosed: !revisionRequested)
			: await records.FindHomeworkAsync(context.TenantId, taskId, now, cancellationToken, includeClosed: !revisionRequested);
		var schedule = record?.Schedules.SingleOrDefault(x => x.DivisionId == submission.DivisionId);
		if (record == null || schedule == null) return Unavailable();
		var eligible = await roster.FindAsync(context.TenantId, record, [submission.DivisionId], [submission.StudentId],
			pendingOnly: false, cancellationToken);
		if (eligible?.Count != 1) return Unavailable();

		var schoolName = tenant.Name ?? (await tenants.FindAsync(context.TenantId))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();

		var variables = WithDeadline(Variables(record, schoolName), schedule.DueAt);
		var recipients = new[] { new NotificationRecipientRefDto("Student", submission.StudentId) };
		return new(true, null, revision, Link, variables, recipients,
			AudienceSlices: [new(Scope(record, submission.DivisionId), recipients)]);
	}

	internal static string? RevisionRequestVersion(DateTimeOffset? submittedAt, DateTimeOffset? mobileUpdatedAt, DateTimeOffset? reviewedAt)
	{
		if (submittedAt is not DateTimeOffset submitted || reviewedAt is not DateTimeOffset reviewed) return null;
		var attempt = mobileUpdatedAt is DateTimeOffset updated && updated > submitted ? updated : submitted;
		// PostgreSQL stores microseconds. Feedback edits change the review timestamp, while the submitted attempt stays fixed.
		var attemptVersion = attempt.UtcTicks / 10;
		return reviewed.UtcTicks / 10 < attemptVersion ? null : "submitted-" + attemptVersion.ToString(CultureInfo.InvariantCulture);
	}
}
