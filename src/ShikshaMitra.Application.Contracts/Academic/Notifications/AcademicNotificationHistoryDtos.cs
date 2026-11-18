using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Notifications;

public sealed class GetAcademicNotificationHistoryDto : PagedResultRequestDto
{
	public string EventKey { get; set; } = string.Empty;
	public string SourceType { get; set; } = string.Empty;
	public Guid SourceId { get; set; }
	public Guid? OperationId { get; set; }
	public NotificationDispatchState? State { get; set; }
}

public sealed record AcademicNotificationHistoryItemDto(Guid OperationId, Guid? DispatchId, Guid? AttemptId,
	Guid? RecipientSnapshotId, string? RecipientKind, Guid? RecipientId, Guid? RecipientUserId,
	CommunicationChannelType? Channel, NotificationDispatchState Status, int? AttemptNumber,
	DateTimeOffset RecordedAt, DateTimeOffset? AcceptedAt, DateTimeOffset? DeliveredAt,
	DateTimeOffset? ReadAt, string? ReasonCode);

public sealed record AcademicNotificationDeliveryCountDto(CommunicationChannelType Channel, NotificationDispatchState Status, int Count);
public sealed record AcademicNotificationIntentCountDto(NotificationIntentState Status, string? ReasonCode, int Count);

public sealed record AcademicNotificationOperationStatusDto(Guid OperationId, string EventKey, string SourceType, Guid SourceId,
	CommunicationCampaignStatus Status, bool ApprovalRequired, DateTimeOffset CreatedAt, DateTimeOffset? PublishedAt,
	DateTimeOffset? ScheduledAt, DateTimeOffset? ApprovedAt, IReadOnlyList<AcademicNotificationDeliveryCountDto> Deliveries,
	IReadOnlyList<AcademicNotificationIntentCountDto> Intents, int ExcludedBeforeDispatch);

public interface IAcademicNotificationHistoryAppService : IApplicationService
{
	Task<AcademicNotificationOperationStatusDto> GetOperationAsync(Guid operationId, CancellationToken cancellationToken = default);
	Task<PagedResultDto<AcademicNotificationHistoryItemDto>> GetHistoryAsync(GetAcademicNotificationHistoryDto input,
		CancellationToken cancellationToken = default);
}
