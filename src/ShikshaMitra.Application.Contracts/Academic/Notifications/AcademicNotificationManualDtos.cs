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
	Guid? TemplateId = null, IReadOnlyList<string>? SelectedFamilyRelationships = null);

public sealed record Student360NotificationFamilyChoiceDto(string Relationship, bool IsPrimary);

public sealed record Student360NotificationAudienceDto(Guid? AcademicMediumId,
	IReadOnlyList<Student360NotificationFamilyChoiceDto> Family, IReadOnlyList<NotificationRecipientRefDto> Staff)
{
	public bool PrimaryGuardianNotConfigured { get; init; }
}

public sealed record AcademicNotificationManualPreviewDto(Guid? ReviewToken, DateTimeOffset? ExpiresAt,
	int EligibleRecipientCount, bool RequiresApproval, IReadOnlyList<AcademicNotificationPreviewChannelDto> Channels,
	IReadOnlyList<AcademicNotificationPreviewSampleDto> Samples, IReadOnlyList<string> BlockingIssues)
{
	public bool IsQueueWorkerEnabled { get; init; }
	public bool IsSchoolQueuePaused { get; init; }
	public IReadOnlyList<CommunicationChannelType> PausedChannels { get; init; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfirmAcademicNotificationManualDto(Guid ReviewToken, string IdempotencyKey);

public sealed record AcademicNotificationOperationDto(Guid OperationId, CommunicationCampaignStatus Status)
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public bool? IsQueueWorkerEnabled { get; init; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public bool? IsSchoolQueuePaused { get; init; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public IReadOnlyList<CommunicationChannelType>? PausedChannels { get; init; }
}

public sealed record AcademicNotificationApprovalReviewDto(bool CanApprove, int EligibleRecipientCount,
	IReadOnlyList<AcademicNotificationPreviewChannelDto> Channels, IReadOnlyList<AcademicNotificationPreviewSampleDto> Samples,
	IReadOnlyList<string> BlockingIssues);

public interface IAcademicNotificationManualAppService : IApplicationService
{
	Task<Student360NotificationAudienceDto> GetStudent360AudienceAsync(Guid studentId, string eventKey,
		CancellationToken cancellationToken = default);
	Task<IReadOnlyList<NotificationRecipientRefDto>> GetDiaryFollowUpRecipientsAsync(Guid diaryId,
		CancellationToken cancellationToken = default);
	Task<AcademicNotificationManualPreviewDto> PreviewAsync(PreviewAcademicNotificationManualDto input,
		CancellationToken cancellationToken = default);
	Task<AcademicNotificationOperationDto> SendAsync(ConfirmAcademicNotificationManualDto input,
		CancellationToken cancellationToken = default);
	Task<AcademicNotificationApprovalReviewDto> GetApprovalReviewAsync(Guid operationId, CancellationToken cancellationToken = default);
	Task<AcademicNotificationOperationDto> ApproveAsync(Guid operationId, CancellationToken cancellationToken = default);
	Task<AcademicNotificationOperationDto> RejectAsync(Guid operationId, CancellationToken cancellationToken = default);
}
