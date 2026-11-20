using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), DisableAuditing]
public sealed class AcademicNotificationHistoryRows(IRepository<NotificationDispatch, Guid> dispatches,
	IRepository<DeliveryAttempt, Guid> attempts, IRepository<CommunicationRecipientSnapshot, Guid> recipients,
	IRepository<NotificationIntent, Guid> intents, IAsyncQueryableExecuter queries) : ITransientDependency
{
	private sealed class HistoryRow
	{
		public Guid Id { get; init; }
		public Guid OperationId { get; init; }
		public Guid? DispatchId { get; init; }
		public Guid? AttemptId { get; init; }
		public Guid? RecipientSnapshotId { get; init; }
		public CommunicationChannelType? Channel { get; init; }
		public NotificationDispatchState Status { get; init; }
		public int? AttemptNumber { get; init; }
		public DateTime CreatedAt { get; init; }
		public DateTimeOffset? QueuedAt { get; init; }
		public DateTimeOffset? AcceptedAt { get; init; }
		public DateTimeOffset? DeliveredAt { get; init; }
		public DateTimeOffset? ReadAt { get; init; }
		public string? ReasonCode { get; init; }
	}

	private sealed record RecipientRow(Guid Id, Guid OperationId, string Kind, Guid? RecipientId, Guid? UserId, string EligibilityJson);
	private sealed record RecipientEvidence(int SchemaVersion, Guid IntentId, NotificationRecipientRefDto SourceRecipient,
		NotificationRecipientRefDto Recipient, CommunicationChannelType Channel, string? ExclusionCode);

	private static readonly HashSet<string> KnownReasons = new(StringComparer.Ordinal)
	{
		NotificationErrorCodes.PrimaryGuardianNotConfigured, NotificationErrorCodes.RecipientIneligible,
		NotificationErrorCodes.SourceUnavailable, NotificationErrorCodes.TemplateInvalid, NotificationErrorCodes.RequiredVariableMissing,
		NotificationErrorCodes.ProviderUnavailable, NotificationErrorCodes.ProviderQuotaExhausted, NotificationErrorCodes.UnknownAcceptance,
		NotificationErrorCodes.ReviewChanged, NotificationErrorCodes.RuntimeSnapshotInvalid, NotificationErrorCodes.RuleConfigurationInvalid,
		NotificationErrorCodes.CancelledBeforeDelivery, NotificationErrorCodes.StaffRetryRequested,
		"SourceExpired", "SourceRevisionChanged", "RuleDisabled", "NoEligibleRecipients", "SomeRecipientsSkipped",
		"DeliveryFailed", "DeliveryConfigurationNeedsReview", "ProviderChanged", "GuardianAccessReviewPending",
		"GuardianRelationshipUnavailable", "VerifiedAccountRequired", "VerifiedEmailRequired", "AccountAssociationAmbiguous", "StudentNotEnrolled", "StudentScopeChanged",
		"StaffAccountInactive", "EmailMissing", "EmailInvalid", "PhoneMissing", "PhoneInvalid", "ChannelUnsupported",
		"ConsentRequired", "ConsentRevoked", "ChannelDisabledByPreference", "QuietHours", "QuietHoursUnavailable",
		"PreferenceConfigurationInvalid", "RecipientAssociationChanged", "RecipientContentAmbiguous", "WhatsAppTemplateNotApproved",
		"WhatsAppDeliveryFailed", "WhatsAppRateLimited", "WhatsAppRequestRejected", "WhatsAppAcceptanceUnknown",
		"WebPushSubscriptionUnavailable", "WebPushSubscriptionExpired", "WebPushRateLimited", "WebPushProviderRejected",
		"WebPushDispatchUnavailable", "SmtpTemporaryRejection", "SmtpPermanentRejection", "SmtpAcceptanceUnknown",
		"DispatchPayloadInvalid", "DispatchPayloadTooLarge",
		"LeaseExpiredBeforeIo", "NotificationOperationUnavailable", "RecipientResolverUnavailable", "RecipientNotSelected", "AttemptLimitReached"
	};

	internal async Task<PagedResultDto<AcademicNotificationHistoryItemDto>> GetPageAsync(Guid tenantId, Guid[] intentIds,
		Guid[] completeOperations, NotificationEventDefinition definition, GetAcademicNotificationHistoryDto input,
		CancellationToken cancellationToken)
	{
		var allDispatches = await TenantDispatchesAsync(tenantId);
		var owned = await OwnedDispatchesAsync(tenantId, intentIds, allDispatches);
		var linkedAttempts = (await attempts.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted && x.NotificationDispatchId.HasValue);
		var attemptRows = from attempt in linkedAttempts
			join dispatch in owned on attempt.NotificationDispatchId equals (Guid?)dispatch.Id
			where attempt.CampaignId == dispatch.CampaignId && attempt.Channel == dispatch.Channel &&
				attempt.RecipientSnapshotId == dispatch.RecipientSnapshotId
			select new HistoryRow
			{
				Id = attempt.Id, OperationId = attempt.CampaignId, DispatchId = dispatch.Id, AttemptId = attempt.Id,
				RecipientSnapshotId = attempt.RecipientSnapshotId, Channel = attempt.Channel,
				Status = attempt.Status == CommunicationDeliveryStatus.Sent ? NotificationDispatchState.ProviderAccepted :
					attempt.Status == CommunicationDeliveryStatus.Delivered ? NotificationDispatchState.Delivered :
					attempt.Status == CommunicationDeliveryStatus.Read ? NotificationDispatchState.Read :
					attempt.Status == CommunicationDeliveryStatus.Failed || attempt.Status == CommunicationDeliveryStatus.Bounced ? NotificationDispatchState.Failed :
					attempt.Status == CommunicationDeliveryStatus.Skipped ? NotificationDispatchState.Skipped :
					attempt.Status == CommunicationDeliveryStatus.Queued
						? (dispatch.Status == NotificationDispatchState.Processing ? NotificationDispatchState.Processing : NotificationDispatchState.Pending)
						: NotificationDispatchState.UnknownAcceptance,
				AttemptNumber = attempt.AttemptNumber, CreatedAt = attempt.CreationTime, QueuedAt = attempt.QueuedAt,
				AcceptedAt = attempt.SentAt, DeliveredAt = attempt.DeliveredAt, ReadAt = attempt.ReadAt, ReasonCode = attempt.ErrorCode
			};

		var withoutAttempt = owned.Where(dispatch => dispatch.Status == NotificationDispatchState.Pending ||
			!linkedAttempts.Any(attempt => attempt.NotificationDispatchId == dispatch.Id && attempt.CampaignId == dispatch.CampaignId &&
				attempt.RecipientSnapshotId == dispatch.RecipientSnapshotId && attempt.Channel == dispatch.Channel &&
				attempt.AttemptNumber == dispatch.AttemptNumber));
		var queuedRows = withoutAttempt.Select(dispatch => new HistoryRow
		{
			Id = dispatch.Id, OperationId = dispatch.CampaignId!.Value, DispatchId = dispatch.Id, AttemptId = (Guid?)null,
			RecipientSnapshotId = dispatch.RecipientSnapshotId, Channel = dispatch.Channel, Status = dispatch.Status,
			AttemptNumber = dispatch.Status == NotificationDispatchState.Pending ? dispatch.AttemptNumber + 1 : dispatch.AttemptNumber,
			CreatedAt = dispatch.CreationTime, QueuedAt = (DateTimeOffset?)null, AcceptedAt = dispatch.AcceptedAt,
			DeliveredAt = (DateTimeOffset?)null, ReadAt = (DateTimeOffset?)null, ReasonCode = dispatch.ErrorCode
		});

		var excludedRows = (await ExcludedAsync(tenantId, completeOperations, allDispatches))
			.Select(row => new HistoryRow
			{
				Id = row.Id, OperationId = row.CampaignId, DispatchId = (Guid?)null, AttemptId = (Guid?)null,
				RecipientSnapshotId = row.Id, Channel = (CommunicationChannelType?)null, Status = NotificationDispatchState.Skipped,
				AttemptNumber = (int?)null, CreatedAt = row.CreationTime, QueuedAt = (DateTimeOffset?)null,
				AcceptedAt = (DateTimeOffset?)null, DeliveredAt = (DateTimeOffset?)null, ReadAt = (DateTimeOffset?)null, ReasonCode = row.BlockedReason
			});
		var timeline = attemptRows.Concat(queuedRows).Concat(excludedRows);
		if (input.State.HasValue) timeline = timeline.Where(x => x.Status == input.State);
		var count = await queries.LongCountAsync(timeline, cancellationToken);
		var page = await queries.ToListAsync(timeline.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
			.Skip(input.SkipCount).Take(input.MaxResultCount), cancellationToken);
		var snapshotIds = page.Select(x => x.RecipientSnapshotId).OfType<Guid>().Distinct().ToArray();
		var labels = await ReadLabelsAsync(tenantId, snapshotIds, cancellationToken);
		return new(count, page.Select(row => Map(row, labels, definition)).ToArray());
	}

	internal async Task<IReadOnlyList<AcademicNotificationDeliveryCountDto>> GetCountsAsync(Guid tenantId, Guid[] intentIds,
		CancellationToken cancellationToken)
	{
		var q = await OwnedDispatchesAsync(tenantId, intentIds, await TenantDispatchesAsync(tenantId));
		return await queries.ToListAsync(q.GroupBy(x => new { x.Channel, x.Status })
			.OrderBy(x => x.Key.Channel).ThenBy(x => x.Key.Status)
			.Select(x => new AcademicNotificationDeliveryCountDto(x.Key.Channel, x.Key.Status, x.Count())), cancellationToken);
	}

	internal async Task<int> CountExcludedAsync(Guid tenantId, Guid[] operationIds, CancellationToken cancellationToken) =>
		await queries.CountAsync(await ExcludedAsync(tenantId, operationIds, await TenantDispatchesAsync(tenantId)), cancellationToken);

	private async Task<IQueryable<NotificationDispatch>> TenantDispatchesAsync(Guid tenantId) =>
		(await dispatches.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted);

	private async Task<IQueryable<NotificationDispatch>> OwnedDispatchesAsync(Guid tenantId, Guid[] intentIds,
		IQueryable<NotificationDispatch> tenantDispatches)
	{
		var owned = (await intents.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.CampaignId.HasValue && Enumerable.Contains(intentIds, x.Id));
		return tenantDispatches.Where(x => owned.Any(intent => intent.Id == x.IntentId && intent.CampaignId == x.CampaignId));
	}

	private async Task<IQueryable<CommunicationRecipientSnapshot>> ExcludedAsync(Guid tenantId, Guid[] operationIds,
		IQueryable<NotificationDispatch> existing) =>
		(await recipients.GetQueryableAsync()).Where(row => row.TenantId == tenantId && !row.IsDeleted && !row.IsEligible &&
			Enumerable.Contains(operationIds, row.CampaignId) && !existing.Any(x => x.CampaignId == row.CampaignId && x.RecipientSnapshotId == row.Id));

	private async Task<Dictionary<Guid, RecipientRow>> ReadLabelsAsync(Guid tenantId, Guid[] ids, CancellationToken cancellationToken)
	{
		if (ids.Length == 0) return [];
		var rows = await queries.ToListAsync((await recipients.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			Enumerable.Contains(ids, x.Id)).Select(x => new RecipientRow(x.Id, x.CampaignId, x.RecipientType,
				x.RecipientId, x.UserId, x.ChannelEligibilityJson)), cancellationToken);
		return rows.ToDictionary(x => x.Id);
	}

	private static AcademicNotificationHistoryItemDto Map(HistoryRow row, Dictionary<Guid, RecipientRow> labels, NotificationEventDefinition definition)
	{
		var label = row.RecipientSnapshotId.HasValue ? labels.GetValueOrDefault(row.RecipientSnapshotId.Value) : null;
		if (label?.OperationId != row.OperationId || label.Kind is not ("Guardian" or "StudentAccount" or "Student") &&
			!definition.RecipientSelectors.Contains(label.Kind)) label = null;
		var channel = row.Channel ?? ExcludedChannel(label);
		return new(row.OperationId, row.DispatchId, row.AttemptId, label?.Id, label?.Kind, label?.RecipientId, label?.UserId, channel,
			row.Status, row.AttemptNumber, row.QueuedAt ?? Utc(row.CreatedAt), row.AcceptedAt, row.DeliveredAt, row.ReadAt, SafeReason(row.ReasonCode));
	}

	private static CommunicationChannelType? ExcludedChannel(RecipientRow? label)
	{
		if (label == null) return null;
		try
		{
			var evidence = NotificationRuntimeJson.Deserialize<RecipientEvidence>(label.EligibilityJson, NotificationErrorCodes.RuntimeSnapshotInvalid);
			return evidence.SchemaVersion == 1 && Enum.IsDefined(evidence.Channel) ? evidence.Channel : null;
		}
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuntimeSnapshotInvalid) { return null; }
	}

	internal static string? SafeReason(string? reason) => reason == null ? null : KnownReasons.Contains(reason) ? reason : "DeliveryDetailUnavailable";

	internal static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
