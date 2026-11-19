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
public sealed class HomeworkFeedbackReleaseEventCapture(INotificationIntentWriter writer, IRepository<NotificationRule, Guid> rules,
	HomeworkAppService access, IRepository<HomeworkAssignmentEntity, Guid> tasks, IAsyncQueryableExecuter queries,
	ICurrentTenant tenant, ICancellationTokenProvider cancellation) : ITransientDependency
{
	internal async Task CaptureAsync(StudentSubmissionEntity submission, SubmissionStatus previousStatus, Guid? actorId)
	{
		if (previousStatus == SubmissionStatus.Reviewed || submission.Status != SubmissionStatus.Reviewed ||
			submission.ReviewedAt is not DateTimeOffset reviewedAt || submission.HomeworkAssignmentId is not Guid homeworkId ||
			submission.AssignmentId.HasValue || tenant.Id is not Guid tenantId || submission.TenantId != tenantId || actorId is null)
			return;

		const string eventKey = HomeworkFeedbackReleaseSourceAdapter.EventKey;
		if (!await queries.AnyAsync((await rules.GetQueryableAsync()).Where(x =>
			x.TenantId == tenantId && !x.IsDeleted && x.IsEnabled && x.EventKey == eventKey), cancellation.Token)) return;
		var scoped = await access.ApplyCurrentUserScopeAsync((await tasks.GetQueryableAsync()).Where(x =>
			x.TenantId == tenantId && !x.IsDeleted && x.Id == homeworkId));
		if (!await queries.AnyAsync(scoped, cancellation.Token)) return;

		await writer.WriteAsync(new(eventKey, HomeworkReminderSourceAdapter.SourceType, submission.Id,
			HomeworkFeedbackReleaseSourceAdapter.ReleaseRevision, reviewedAt, [], actorId), cancellation.Token);
	}
}
