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
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationDueSource))]
public sealed class HomeworkReminderDueSource(IRepository<HomeworkAssignmentEntity, Guid> homeworks, LearningTaskReminderDuePager pager,
	ICurrentTenant tenant) : INotificationDueSource, ITransientDependency
{
	public string SourceType => LearningTaskReminderSourceAdapter.HomeworkSourceType;
	public IReadOnlyCollection<string> ScheduledEventKeys { get; } = Array.AsReadOnly(new[]
	{
		AcademicNotificationEventKeys.HomeworkDue, AcademicNotificationEventKeys.HomeworkOverdue
	});

	public async Task<NotificationDueSourcePage> ReadPageAsync(NotificationDueScanRequest request, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (request.TenantId == Guid.Empty || request.TenantId != tenant.Id) return new([], null);
		var startedAt = request.CycleStartedAt.UtcDateTime;
		var tasks = (await homeworks.GetQueryableAsync()).Where(x => x.TenantId == request.TenantId && !x.IsDeleted &&
			x.Status != HomeworkStatus.Closed && x.SubmissionType != SubmissionType.NoSubmissionRequired);
		var scheduled = from task in tasks
			from schedule in task.DivisionSchedules
			where schedule.TenantId == request.TenantId && schedule.IsPublished &&
				(!schedule.PublishedAt.HasValue || schedule.PublishedAt <= startedAt)
			select new { SourceId = task.Id, ClassId = task.StandardId, schedule.DivisionId, schedule.DueDate, schedule.PublishedAt };
		var legacy = from task in tasks
			where task.IsPublished && task.DivisionId.HasValue && task.DueDate.HasValue && !task.DivisionSchedules.Any()
			select new { SourceId = task.Id, ClassId = task.StandardId, DivisionId = task.DivisionId!.Value, DueDate = task.DueDate!.Value, PublishedAt = (DateTime?)null };
		var occurrences = scheduled.Concat(legacy).Select(x => new LearningTaskDueRow
		{
			SourceId = x.SourceId, ClassId = x.ClassId, DivisionId = x.DivisionId, DueDate = x.DueDate, PublishedAt = x.PublishedAt,
		});
		return await pager.ReadPageAsync(AcademicNotificationEventKeys.HomeworkDue, AcademicNotificationEventKeys.HomeworkOverdue,
			occurrences, request, cancellationToken);
	}
}
