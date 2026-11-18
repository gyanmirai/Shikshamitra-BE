using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

public sealed class NotificationOutcomeLock(INotificationPersonalWriteLock writeLock) : ITransientDependency
{
	internal Task AcquireAsync(Guid operationId, CancellationToken cancellationToken) =>
		writeLock.AcquireAsync([$"notification-outcome:{operationId:N}"], cancellationToken);

	internal Task AcquireStaffActionAsync(Guid operationId, CancellationToken cancellationToken) =>
		writeLock.AcquireAsync([$"manual-operation:{operationId:N}", $"notification-outcome:{operationId:N}"], cancellationToken);
}
