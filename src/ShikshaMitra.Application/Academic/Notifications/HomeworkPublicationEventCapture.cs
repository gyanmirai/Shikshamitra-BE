using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class HomeworkPublicationEventCapture(INotificationIntentWriter writer, IRepository<NotificationRule, Guid> rules,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant, IClock clock, ICancellationTokenProvider cancellation) : ITransientDependency
{
	internal async Task CaptureAsync(HomeworkAssignmentEntity homework, Guid? actorId)
	{
		if (tenant.Id is not Guid tenantId || homework.TenantId != tenantId || actorId is null ||
			homework.Status != HomeworkStatus.Published) return;
		var published = homework.DivisionSchedules.Where(x => x.TenantId == tenantId && x.IsPublished)
			.Select(x => x.DivisionId).ToArray();
		if (published.Length == 0 && homework.DivisionSchedules.Count == 0 && homework.IsPublished && homework.DivisionId.HasValue)
			published = [homework.DivisionId.Value];
		if (published.Length == 0) return;

		const string eventKey = HomeworkPublicationSourceAdapter.EventKey;
		if (!await queries.AnyAsync((await rules.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.IsEnabled && x.EventKey == eventKey), cancellation.Token)) return;

		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		await writer.WriteAsync(new(eventKey, HomeworkReminderSourceAdapter.SourceType, homework.Id, Revision(published),
			now, [], actorId), cancellation.Token);
	}

	internal static string Revision(IEnumerable<Guid> divisions) =>
		Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(divisions.Order().ToArray())));
}
