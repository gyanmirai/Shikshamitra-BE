using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace ShikshaMitra.Academic.Notifications;

public sealed class RoomNotificationCapture(INotificationIntentWriter writer, IRepository<NotificationRule, Guid> rules,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant, ICurrentUser user, ICancellationTokenProvider cancellation,
	IClock clock) : ILocalEventHandler<RoomReservationNotification>, ITransientDependency
{
	public async Task HandleEventAsync(RoomReservationNotification notice)
	{
		if (tenant.Id is not Guid tenantId || notice.TenantId != tenantId) return;
		var token = cancellation.Token;
		if (!await queries.AnyAsync((await rules.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.IsEnabled && x.EventKey == notice.EventKey), token)) return;
		await writer.WriteAsync(new(notice.EventKey, RoomNotificationSource.Type, notice.ReservationId, notice.Revision,
			new DateTimeOffset(clock.Now.ToUniversalTime()), [], user.Id), token);
	}
}
