using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationDueSource))]
public sealed class AssignmentReminderDueSource(IRepository<AssignmentEntity, Guid> assignments, LearningTaskReminderDuePager pager,
	ICurrentTenant tenant) : INotificationDueSource, ITransientDependency
{
	public string SourceType => LearningTaskReminderSourceAdapter.AssignmentSourceType;

	public async Task<NotificationDueSourcePage> ReadPageAsync(NotificationDueScanRequest request, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (request.TenantId == Guid.Empty || request.TenantId != tenant.Id) return new([], null);
		var now = request.Now.UtcDateTime;
		var startedAt = request.CycleStartedAt.UtcDateTime;
		var occurrences = from assignment in await assignments.GetQueryableAsync()
			where assignment.TenantId == request.TenantId && !assignment.IsDeleted &&
				assignment.Status != AssignmentStatus.Closed && assignment.SubmissionType != SubmissionType.NoSubmissionRequired
			from schedule in assignment.DivisionSchedules
			where schedule.TenantId == request.TenantId && schedule.IsPublished && schedule.DueDate > now &&
				(!schedule.PublishedAt.HasValue || schedule.PublishedAt <= startedAt)
			select new LearningTaskDueRow
			{
				SourceId = assignment.Id, ClassId = assignment.StandardId, DivisionId = schedule.DivisionId, DueDate = schedule.DueDate,
			};
		return await pager.ReadPageAsync(LearningTaskReminderSourceAdapter.AssignmentEventKey, occurrences, request, cancellationToken);
	}
}
