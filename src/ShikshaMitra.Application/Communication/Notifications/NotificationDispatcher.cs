using System;
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
	NotificationDispatchReadiness readiness, IEnumerable<INotificationChannelSender> senders, NotificationOutcomeLock outcomeLock,
	IRepository<NotificationDispatch, Guid> dispatches, IRepository<NotificationIntent, Guid> intents,
	IRepository<CommunicationCampaign, Guid> campaigns, IAsyncQueryableExecuter queries, NotificationDeliveryProjection delivery)
	: INotificationDispatcher, ITransientDependency
{
	private sealed record PreparedSend(INotificationChannelSender Sender, NotificationChannelSendRequest Request, DateTimeOffset? ExpiresAt);

	public async Task DispatchAsync(Guid dispatchId, Guid leaseToken, CancellationToken cancellationToken, NotificationTemplateTestPilot? pilot = null)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (units.Current != null) throw new BusinessException(NotificationErrorCodes.StateConflict);
		var prepared = await PrepareAsync(tenantId, dispatchId, leaseToken, cancellationToken, pilot);
		if (prepared == null) return;

		NotificationChannelSendResult? result;
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			result = await prepared.Sender.SendAsync(prepared.Request, cancellationToken);
		}
		catch (Exception)
		{
			result = null;
		}
		await RecordAsync(tenantId, dispatchId, leaseToken, result, prepared.ExpiresAt, allowAutomaticRetry: pilot == null);
		cancellationToken.ThrowIfCancellationRequested();
	}

	private async Task<PreparedSend?> PrepareAsync(Guid tenantId, Guid dispatchId, Guid lease, CancellationToken cancellationToken,
		NotificationTemplateTestPilot? pilot)
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
		else if (row.AttemptNumber > NotificationDispatch.MaximumAttempts)
			decision = new(null, null, null, "AttemptLimitReached");
		else
		{
			var intent = await FindCurrentIntentAsync(tenantId, row, cancellationToken);
			decision = intent == null || (pilot != null &&
				(!pilot.HasSupportedChannel || pilot.TenantId != tenantId || row.CampaignId != pilot.OperationId ||
					row.Channel != pilot.Channel || row.AttemptNumber != 1 ||
					intent.SourceType != NotificationCampaignTypes.AcademicTemplateTestSource))
				? new(null, null, null, "NotificationOperationUnavailable")
				: await readiness.CheckAsync(intent, row, now, cancellationToken);
		}
		cancellationToken.ThrowIfCancellationRequested();
		now = Now();
		PreparedSend? prepared = null;
		var error = decision.ExclusionCode;
		if (error == null && pilot != null &&
			(decision.Payload?.Recipient is not { UserId: { } recipientId, StudentId: null } || recipientId != pilot.RecipientUserId))
			error = NotificationErrorCodes.RecipientIneligible;
		if (error == null && (decision.ExpiresAt <= now || row.ExpiresAt <= now)) error = "SourceExpired";
		if (error != null) row.SkipBeforeIo(lease, now, error);
		else if (decision.DueAt > now)
		{
			if (pilot == null) row.DeferBeforeIo(lease, now, decision.DueAt.Value);
			else row.SkipBeforeIo(lease, now, "QuietHours");
		}
		else
		{
			var payload = decision.Payload!;
			var destination = payload.Destination ?? payload.Recipient.UserId?.ToString("N");
			if (string.IsNullOrWhiteSpace(destination))
				row.SkipBeforeIo(lease, now, NotificationErrorCodes.RecipientIneligible);
			else
			{
				var reservation = await delivery.ReserveAsync(row, cancellationToken);
				now = Now();
				if (reservation.AttemptId is Guid attemptId)
				{
					if (decision.ExpiresAt <= now || row.ExpiresAt <= now) throw new BusinessException(NotificationErrorCodes.LeaseLost);
					row.BeginIo(lease, now, attemptId);
					prepared = new(matching[0], new(row.Id, row.DeduplicationKey, row.ProviderSettingId ?? Guid.Empty, row.Channel,
						destination, payload.Subject, payload.Body, payload.LinkUrl, payload.LanguageCode, row.AttemptNumber,
						WhatsAppTemplateBindingId: decision.WhatsAppTemplateBindingId,
						TemplateRevisionId: row.TemplateRevisionId, Recipient: payload.Recipient, WhatsAppVariables: payload.WhatsAppVariables,
						ExpiresAt: decision.ExpiresAt), decision.ExpiresAt);
				}
				else if (pilot == null && reservation.RetryAt is DateTimeOffset retryAt && retryAt > now &&
					!(decision.ExpiresAt <= retryAt) && !(row.ExpiresAt <= retryAt))
					row.DeferBeforeIo(lease, now, retryAt, NotificationErrorCodes.ProviderQuotaExhausted);
				else
					row.SkipBeforeIo(lease, now, reservation.ExclusionCode ?? NotificationErrorCodes.ProviderUnavailable);
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

	private async Task RecordAsync(Guid tenantId, Guid dispatchId, Guid lease, NotificationChannelSendResult? result,
		DateTimeOffset? expiry, bool allowAutomaticRetry)
	{
		for (var attempt = 0; ; attempt++)
		{
			using var unit = units.Begin(requiresNew: true, isTransactional: true);
			try
			{
				var row = await FindAsync(tenantId, dispatchId, CancellationToken.None) ?? throw new BusinessException(NotificationErrorCodes.LeaseLost);
				if (row.CampaignId is Guid operationId) await outcomeLock.AcquireAsync(operationId, CancellationToken.None);
				var prior = row.Status;
				var now = Now();
				ApplyResult(row, lease, now, result, expiry, allowAutomaticRetry);
				await delivery.RecordAsync(row, now, CancellationToken.None);
				if (prior == NotificationDispatchState.UnknownAcceptance && row.Status != prior)
					await NotificationOutcomeProjector.ReopenIntentAsync(intents, queries, tenantId, row, CancellationToken.None);
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
		NotificationChannelSendResult? result, DateTimeOffset? expiry, bool allowAutomaticRetry)
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
				if (result.IsTransient && allowAutomaticRetry)
					NotificationDispatchRetryPolicy.ScheduleAfterRejection(row, lease, now, result.RetryAfter, expiry, reason);
				return;
			default:
				row.RecordOutcome(lease, now, NotificationDispatchState.UnknownAcceptance, providerId, NotificationErrorCodes.UnknownAcceptance);
				return;
		}
	}

	private static string SafeErrorCode(string? code) => code is { Length: > 0 and <= 160 } &&
		code.All(x => char.IsAsciiLetterOrDigit(x) || x is ':' or '.' or '_' or '-') ? code : "ProviderRejected";

	private static string? SafeProviderId(string? id) => id is { Length: > 0 and <= 256 } &&
		id.All(x => char.IsAsciiLetterOrDigit(x) || x is ':' or '.' or '_' or '-' or '+' or '/' or '=') ? id : null;

	private DateTimeOffset Now() => new(clock.Now.ToUniversalTime());
}
