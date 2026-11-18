using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Notifications;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewAcademicNotificationManualDto(string EventKey, string SourceType, Guid SourceId,
	IReadOnlyList<NotificationRecipientRefDto> SelectedRecipients, IReadOnlyList<CommunicationChannelType> Channels,
	Guid? TemplateId = null);

public sealed record AcademicNotificationManualPreviewDto(Guid? ReviewToken, DateTimeOffset? ExpiresAt,
	int EligibleRecipientCount, bool RequiresApproval, IReadOnlyList<AcademicNotificationPreviewChannelDto> Channels,
	IReadOnlyList<AcademicNotificationPreviewSampleDto> Samples, IReadOnlyList<string> BlockingIssues);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfirmAcademicNotificationManualDto(Guid ReviewToken, string IdempotencyKey);

public sealed record AcademicNotificationOperationDto(Guid OperationId, CommunicationCampaignStatus Status);

public interface IAcademicNotificationManualAppService : IApplicationService
{
	Task<AcademicNotificationManualPreviewDto> PreviewAsync(PreviewAcademicNotificationManualDto input,
		CancellationToken cancellationToken = default);
	Task<AcademicNotificationOperationDto> SendAsync(ConfirmAcademicNotificationManualDto input,
		CancellationToken cancellationToken = default);
	Task<AcademicNotificationOperationDto> ApproveAsync(Guid operationId, CancellationToken cancellationToken = default);
	Task<AcademicNotificationOperationDto> RejectAsync(Guid operationId, CancellationToken cancellationToken = default);
}
