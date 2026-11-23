using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Communication.Notifications;

public interface INotificationIntentWriter
{
	Task<Guid?> WriteAsync(NotificationEventInput input, CancellationToken cancellationToken);
	Task<NotificationIntentWriteResult> WriteWithOutcomeAsync(NotificationEventInput input, CancellationToken cancellationToken);
}

public sealed record NotificationIntentWriteResult(Guid? OperationId, bool Created)
{
	public bool AddedRecipients { get; init; }
}

public interface INotificationSourceAdapter
{
	IReadOnlyCollection<string> EventKeys { get; }
	Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken cancellationToken);
}

public interface INotificationTemplateRenderer
{
	Task<NotificationRenderResult> RenderAsync(NotificationRenderInput input, CancellationToken cancellationToken);
}

public interface INotificationDispatcher
{
	Task DispatchAsync(Guid dispatchId, Guid leaseToken, CancellationToken cancellationToken, Guid? inAppPilotUserId = null);
}

public interface INotificationDueScheduler
{
	Task<NotificationScanResult> ScanAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken,
		TimeSpan? readTimeout = null);
}

public interface INotificationChannelSender
{
	CommunicationChannelType Channel { get; }
	Task<NotificationChannelSendResult> SendAsync(NotificationChannelSendRequest input, CancellationToken cancellationToken);
}
