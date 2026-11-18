using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false), DisableAuditing, ExposeServices(typeof(INotificationDispatcher))]
public sealed class NotificationDispatcher(ICurrentTenant tenant, IUnitOfWorkManager units, IClock clock,
	NotificationDispatchReadiness readiness, IEnumerable<INotificationChannelSender> senders,
	IRepository<NotificationDispatch, Guid> dispatches, IRepository<NotificationIntent, Guid> intents,
	IRepository<CommunicationCampaign, Guid> campaigns, IAsyncQueryableExecuter queries)
	: INotificationDispatcher, ITransientDependency
{
	private static readonly int[] RetryMinutes = [1, 5, 15, 60];
	private sealed record PreparedSend(INotificationChannelSender Sender, NotificationChannelSendRequest Request, DateTimeOffset? ExpiresAt);

	public async Task DispatchAsync(Guid dispatchId, Guid leaseToken, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (units.Current != null) throw new BusinessException(NotificationErrorCodes.StateConflict);
		var prepared = await PrepareAsync(tenantId, dispatchId, leaseToken, cancellationToken);
		if (prepared == null) return;

		NotificationChannelSendResult? result;
		try
		{
			result = await prepared.Sender.SendAsync(prepared.Request, cancellationToken);
		}
		catch (Exception)
		{
			result = null;
		}
		await RecordAsync(tenantId, dispatchId, leaseToken, result, prepared.ExpiresAt);
		cancellationToken.ThrowIfCancellationRequested();
	}

	private async Task<PreparedSend?> PrepareAsync(Guid tenantId, Guid dispatchId, Guid lease, CancellationToken cancellationToken)
	{
		using var unit = units.Begin(requiresNew: true, isTransactional: true);
		var row = await FindAsync(tenantId, dispatchId, cancellationToken);
		var now = Now();
		if (row == null || lease == Guid.Empty || row.Status != NotificationDispatchState.Processing ||
			row.LeaseId != lease || !row.LeaseExpiresAt.HasValue || row.LeaseExpiresAt <= now || row.IoStartedAt.HasValue)
			throw new BusinessException(NotificationErrorCodes.LeaseLost);
		var matching = senders.Where(x => x.Channel == row.Channel).Take(2).ToArray();
		NotificationDispatchReadinessResult decision;
		if (matching.Length != 1)
			decision = new(null, null, null, NotificationErrorCodes.ProviderUnavailable);
		else if (row.AttemptNumber > RetryMinutes.Length + 1)
			decision = new(null, null, null, "AttemptLimitReached");
		else
		{
			var intent = await FindCurrentIntentAsync(tenantId, row, cancellationToken);
			decision = intent == null ? new(null, null, null, "NotificationOperationUnavailable") :
				await readiness.CheckAsync(intent, row, now, cancellationToken);
		}
		cancellationToken.ThrowIfCancellationRequested();
		now = Now();
		PreparedSend? prepared = null;
		var error = decision.ExclusionCode;
		if (error == null && (decision.ExpiresAt <= now || row.ExpiresAt <= now)) error = "SourceExpired";
		if (error != null) row.SkipBeforeIo(lease, now, error);
		else if (decision.DueAt > now) row.DeferBeforeIo(lease, now, decision.DueAt.Value);
		else
		{
			var payload = decision.Payload!;
			var destination = payload.Destination ?? payload.Recipient.UserId?.ToString("N");
			if (string.IsNullOrWhiteSpace(destination))
				row.SkipBeforeIo(lease, now, NotificationErrorCodes.RecipientIneligible);
			else
			{
				row.BeginIo(lease, now);
				prepared = new(matching[0], new(row.Id, row.DeduplicationKey, row.ProviderSettingId ?? Guid.Empty, row.Channel,
					destination, payload.Subject, payload.Body, payload.LinkUrl, payload.LanguageCode, row.AttemptNumber,
					TemplateRevisionId: row.TemplateRevisionId, Recipient: payload.Recipient, WhatsAppVariables: payload.WhatsAppVariables), decision.ExpiresAt);
			}
		}
		await dispatches.UpdateAsync(row, cancellationToken: cancellationToken);
		await unit.CompleteAsync(cancellationToken);
		return prepared;
	}

	private async Task<NotificationIntent?> FindCurrentIntentAsync(Guid tenantId, NotificationDispatch dispatch, CancellationToken cancellationToken)
	{
		var operationId = dispatch.CampaignId;
		if (!operationId.HasValue) return null;
		var intent = await queries.FirstOrDefaultAsync((await intents.GetQueryableAsync()).Where(x => x.Id == dispatch.IntentId &&
			x.TenantId == tenantId && !x.IsDeleted && x.Status == NotificationIntentState.AwaitingDelivery && x.PlanningCompletedAt != null &&
			x.CampaignId == operationId), cancellationToken);
		if (intent == null) return null;
		var operationIsCurrent = await queries.AnyAsync((await campaigns.GetQueryableAsync()).Where(x => x.Id == operationId &&
			x.TenantId == tenantId && !x.IsDeleted && x.CampaignType == NotificationCampaignTypes.Academic &&
			x.SourceEntityId == intent.SourceId && x.SourceEntityType == intent.SourceType && x.Status != CommunicationCampaignStatus.Cancelled),
			cancellationToken);
		return operationIsCurrent ? intent : null;
	}

	private async Task RecordAsync(Guid tenantId, Guid dispatchId, Guid lease, NotificationChannelSendResult? result, DateTimeOffset? expiry)
	{
		for (var attempt = 0; ; attempt++)
		{
			using var unit = units.Begin(requiresNew: true, isTransactional: true);
			try
			{
				var row = await FindAsync(tenantId, dispatchId, CancellationToken.None) ?? throw new BusinessException(NotificationErrorCodes.LeaseLost);
				ApplyResult(row, lease, Now(), result, expiry);
				await dispatches.UpdateAsync(row, cancellationToken: CancellationToken.None);
				await unit.CompleteAsync(CancellationToken.None);
				return;
			}
			catch (AbpDbConcurrencyException) when (attempt < 2)
			{
				// Recovery or a provider receipt can update the dispatch while this result arrives.
			}
		}
	}

	private async Task<NotificationDispatch?> FindAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
		await queries.FirstOrDefaultAsync((await dispatches.GetQueryableAsync()).Where(x => x.Id == id &&
			x.TenantId == tenantId && !x.IsDeleted), cancellationToken);

	private static void ApplyResult(NotificationDispatch row, Guid lease, DateTimeOffset now,
		NotificationChannelSendResult? result, DateTimeOffset? expiry)
	{
		var providerId = SafeProviderId(result?.ProviderMessageId);
		switch (result?.Acceptance)
		{
			case NotificationProviderAcceptance.Accepted:
				row.RecordOutcome(lease, now, row.Channel == CommunicationChannelType.InApp
					? NotificationDispatchState.Delivered : NotificationDispatchState.ProviderAccepted, providerId);
				return;
			case NotificationProviderAcceptance.Rejected when string.IsNullOrEmpty(result.ProviderMessageId):
				var reason = SafeErrorCode(result.ErrorCode);
				row.RecordOutcome(lease, now, NotificationDispatchState.Failed, errorCode: reason);
				if (result.IsTransient && row.Status == NotificationDispatchState.Failed && row.ProviderMessageId == null &&
					!row.AcceptedAt.HasValue && RetryAt(row, now, result.RetryAfter, expiry) is DateTimeOffset retryAt)
					row.ScheduleRetry(lease, now, retryAt, reason);
				return;
			default:
				row.RecordOutcome(lease, now, NotificationDispatchState.UnknownAcceptance, providerId, NotificationErrorCodes.UnknownAcceptance);
				return;
		}
	}

	private static DateTimeOffset? RetryAt(NotificationDispatch row, DateTimeOffset now, TimeSpan? requested, DateTimeOffset? expiry)
	{
		if (row.AttemptNumber is < 1 || row.AttemptNumber > RetryMinutes.Length) return null;
		var jitter = (BinaryPrimitives.ReadUInt16LittleEndian(row.Id.ToByteArray()) + row.AttemptNumber * 17) % 16;
		var delay = TimeSpan.FromMinutes(RetryMinutes[row.AttemptNumber - 1]) + TimeSpan.FromSeconds(jitter);
		if (requested > delay) delay = requested.Value;
		if (delay >= DateTimeOffset.MaxValue - now) return null;
		var due = now.Add(delay);
		return row.ExpiresAt <= due || expiry <= due ? null : due;
	}

	private static string SafeErrorCode(string? code) => code is { Length: > 0 and <= 160 } &&
		code.All(x => char.IsAsciiLetterOrDigit(x) || x is ':' or '.' or '_' or '-') ? code : "ProviderRejected";

	private static string? SafeProviderId(string? id) => id is { Length: > 0 and <= 256 } &&
		id.All(x => char.IsAsciiLetterOrDigit(x) || x is ':' or '.' or '_' or '-' or '+' or '/' or '=') ? id : null;

	private DateTimeOffset Now() => new(clock.Now.ToUniversalTime());
}
