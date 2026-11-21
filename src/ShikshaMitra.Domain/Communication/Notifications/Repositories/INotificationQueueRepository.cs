using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Communication.Notifications;

public interface INotificationQueueRepository
{
	Task<NotificationQueueHealth> ReadHealthAsync(DateTimeOffset now, CancellationToken cancellationToken);
	Task<IReadOnlyList<NotificationQueueClaim>> ClaimIntentsAsync(DateTimeOffset now, int batchSize,
		TimeSpan leaseDuration, CancellationToken cancellationToken);
	Task<IReadOnlyList<NotificationQueueClaim>> ClaimDispatchesAsync(DateTimeOffset now, int batchSize,
		TimeSpan leaseDuration, CancellationToken cancellationToken, int? maxActivePerChannel = null,
		IReadOnlyList<CommunicationChannelType>? pausedChannels = null);
	Task<bool> RenewDispatchAsync(Guid dispatchId, Guid leaseId, DateTimeOffset now, TimeSpan leaseDuration,
		CancellationToken cancellationToken);
}
