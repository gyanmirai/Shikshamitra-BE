using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.EntityFrameworkCore.Communication.Notifications;

[ExposeServices(typeof(INotificationEventCaptureLock))]
public sealed class NotificationEventCaptureLock(INotificationPersonalWriteLock transactions) : INotificationEventCaptureLock, ITransientDependency
{
	public Task AcquireAsync(string eventKeyHash, CancellationToken cancellationToken)
	{
		if (eventKeyHash == null || eventKeyHash.Length != 64 || !eventKeyHash.All(char.IsAsciiHexDigit)) throw new ArgumentException("A bounded event key hash is required.", nameof(eventKeyHash));
		return transactions.AcquireAsync(["event-capture:" + eventKeyHash], cancellationToken);
	}
}
