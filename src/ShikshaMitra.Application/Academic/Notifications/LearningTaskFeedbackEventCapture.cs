using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class LearningTaskFeedbackEventCapture(INotificationIntentWriter writer, IRepository<NotificationRule, Guid> rules,
	HomeworkAppService access, IRepository<HomeworkAssignmentEntity, Guid> tasks, AssignmentNotificationSourceAuthorizer assignmentAccess,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant, ICancellationTokenProvider cancellation) : ITransientDependency
{
	internal static bool StartsRevisionRequest(StudentSubmissionEntity submission, SubmissionStatus requestedStatus) =>
		requestedStatus == SubmissionStatus.NeedsReview && submission.SubmissionDate.HasValue &&
		(submission.Status != SubmissionStatus.NeedsReview || string.IsNullOrWhiteSpace(submission.Remarks) ||
			LearningTaskFeedbackSourceAdapter.RevisionRequestVersion(submission.SubmissionDate, submission.MobileUpdatedAt, submission.ReviewedAt) == null);

	internal async Task CaptureAsync(StudentSubmissionEntity submission, SubmissionStatus previousStatus, Guid? actorId,
		bool startsRevisionRequest = false)
	{
		var feedbackReleased = previousStatus != SubmissionStatus.Reviewed && submission.Status == SubmissionStatus.Reviewed;
		var revisionRequested = startsRevisionRequest && submission.Status == SubmissionStatus.NeedsReview;
		if ((!feedbackReleased && !revisionRequested) ||
			submission.ReviewedAt is not DateTimeOffset reviewedAt || submission.HomeworkAssignmentId.HasValue == submission.AssignmentId.HasValue ||
			tenant.Id is not Guid tenantId || submission.TenantId != tenantId || actorId is not Guid userId)
			return;

		var revision = revisionRequested
			? LearningTaskFeedbackSourceAdapter.RevisionRequestVersion(submission.SubmissionDate, submission.MobileUpdatedAt, reviewedAt)
			: LearningTaskFeedbackSourceAdapter.ReleaseRevision;
		if (revision == null) return;
		var isAssignment = submission.AssignmentId.HasValue;
		var eventKey = (isAssignment, revisionRequested) switch
		{
			(true, true) => AcademicNotificationEventKeys.AssignmentRevisionRequested,
			(false, true) => AcademicNotificationEventKeys.HomeworkRevisionRequested,
			(true, false) => AcademicNotificationEventKeys.AssignmentFeedbackReleased,
			_ => AcademicNotificationEventKeys.HomeworkFeedbackReleased
		};
		if (!await queries.AnyAsync((await rules.GetQueryableAsync()).Where(x =>
			x.TenantId == tenantId && !x.IsDeleted && x.IsEnabled && x.EventKey == eventKey), cancellation.Token)) return;
		if (submission.AssignmentId is Guid assignmentId)
		{
			if (!await assignmentAccess.CanAccessFeedbackDivisionAsync(tenantId, assignmentId, submission.DivisionId, userId, cancellation.Token))
				return;
		}
		else
		{
			var scoped = await access.ApplyCurrentUserScopeAsync((await tasks.GetQueryableAsync()).Where(x =>
				x.TenantId == tenantId && !x.IsDeleted && x.Id == submission.HomeworkAssignmentId));
			if (!await queries.AnyAsync(scoped, cancellation.Token)) return;
		}

		var sourceType = isAssignment ? LearningTaskReminderSourceAdapter.AssignmentSourceType : LearningTaskReminderSourceAdapter.HomeworkSourceType;
		await writer.WriteAsync(new(eventKey, sourceType, submission.Id, revision, reviewedAt, [], userId), cancellation.Token);
	}
}
