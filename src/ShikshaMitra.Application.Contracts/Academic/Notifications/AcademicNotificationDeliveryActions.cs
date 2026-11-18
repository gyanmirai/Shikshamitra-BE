using System;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Notifications;

public sealed record AcademicNotificationRetryDto(Guid DispatchId, Guid OperationId, NotificationDispatchState Status, DateTimeOffset NextAttemptAt);
public sealed record AcademicNotificationCancellationDto(Guid OperationId, CommunicationCampaignStatus Status,
	int CancelledIntents, int CancelledDispatches, int RemainingInFlight);

public interface IAcademicNotificationDeliveryActionAppService : IApplicationService
{
	Task<AcademicNotificationRetryDto> RetryAsync(Guid dispatchId, CancellationToken cancellationToken = default);
	Task<AcademicNotificationCancellationDto> CancelAsync(Guid operationId, CancellationToken cancellationToken = default);
}
