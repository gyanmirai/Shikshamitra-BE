using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Communication.Notifications;

public interface INotificationQueueRepository
{
	Task<IReadOnlyList<NotificationQueueClaim>> ClaimIntentsAsync(DateTimeOffset now, int batchSize,
		TimeSpan leaseDuration, CancellationToken cancellationToken);
	Task<IReadOnlyList<NotificationQueueClaim>> ClaimDispatchesAsync(DateTimeOffset now, int batchSize,
		TimeSpan leaseDuration, CancellationToken cancellationToken, int? maxActivePerChannel = null);
	Task<bool> RenewDispatchAsync(Guid dispatchId, Guid leaseId, DateTimeOffset now, TimeSpan leaseDuration,
		CancellationToken cancellationToken);
}
