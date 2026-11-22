using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Users;

namespace ShikshaMitra.Academic.Notifications;

public sealed class AssignmentPublicationEventCapture(INotificationIntentWriter writer, IRepository<NotificationRule, Guid> rules,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant, ICurrentUser user, ICancellationTokenProvider cancellation)
	: ILocalEventHandler<AssignmentDivisionPublished>, ITransientDependency
{
	public async Task HandleEventAsync(AssignmentDivisionPublished publication)
	{
		if (tenant.Id is not Guid tenantId || publication.TenantId != tenantId) return;

		const string eventKey = AssignmentPublicationSourceAdapter.EventKey;
		var token = cancellation.Token;
		if (!await queries.AnyAsync((await rules.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.IsEnabled && x.EventKey == eventKey), token)) return;

		await writer.WriteAsync(new(eventKey, LearningTaskReminderSourceAdapter.AssignmentSourceType, publication.ScheduleId,
			AssignmentPublicationSourceAdapter.Revision, LearningTaskNotificationRecordReader.AsUtc(publication.PublishedAt), [], user.Id), token);
	}
}
