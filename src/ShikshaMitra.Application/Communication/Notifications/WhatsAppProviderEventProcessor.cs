using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false), DisableAuditing]
public sealed class WhatsAppProviderEventProcessor(ICurrentTenant tenant, IUnitOfWorkManager units, IClock clock,
	INotificationPersonalWriteLock transactions, NotificationOutcomeLock outcomes, NotificationDeliveryProjection delivery,
	IRepository<NotificationProviderEvent, Guid> events, IRepository<NotificationDispatch, Guid> dispatches,
	IRepository<NotificationIntent, Guid> intents, IRepository<WhatsAppTemplateBinding, Guid> bindings,
	IAsyncQueryableExecuter queries, IDataProtectionProvider protection, ILogger<WhatsAppProviderEventProcessor> logger) : ITransientDependency
{
	private sealed record Candidate(Guid EventId, Guid ProviderId, string MessageId);
	private sealed record Operation(Guid DispatchId, Guid CampaignId);

	public async Task<int> ProcessBatchAsync(int batchSize, CancellationToken cancellationToken)
	{
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (units.Current != null) throw new BusinessException(NotificationErrorCodes.StateConflict);
		if (batchSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(batchSize));

		var candidates = await FindCandidatesAsync(tenantId, batchSize, cancellationToken);
		var processed = 0;
		foreach (var candidate in candidates)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				if (await ApplyAsync(tenantId, candidate, cancellationToken)) processed++;
			}
			catch (Exception) when (!cancellationToken.IsCancellationRequested)
			{
				logger.LogWarning("WhatsApp receipt {EventId} for school {TenantId} could not be applied.", candidate.EventId, tenantId);
			}
		}
		return processed;
	}

	private async Task<IReadOnlyList<Candidate>> FindCandidatesAsync(Guid tenantId, int batchSize, CancellationToken cancellationToken)
	{
		using var read = units.Begin(requiresNew: true, isTransactional: false);
		var dispatched = await FindDispatchesAsync(tenantId);
		var pending = (await events.GetQueryableAsync()).Where(e => e.TenantId == tenantId && !e.IsDeleted && e.ProcessedAt == null &&
			e.ProviderEventKey.StartsWith("wa:") && e.ProviderMessageId != null &&
			dispatched.Any(d => d.ProviderSettingId == e.ProviderSettingId && d.ProviderMessageId == e.ProviderMessageId));
		var candidates = await queries.ToListAsync(pending.OrderBy(x => x.Id).Take(batchSize)
			.Select(e => new Candidate(e.Id, e.ProviderSettingId, e.ProviderMessageId!)), cancellationToken);
		await read.CompleteAsync(cancellationToken);
		return candidates;
	}

	private async Task<bool> ApplyAsync(Guid tenantId, Candidate candidate, CancellationToken cancellationToken)
	{
		using var write = units.Begin(requiresNew: true, isTransactional: true);
		await transactions.AcquireAsync([$"whatsapp-receipt:{candidate.EventId:N}"], cancellationToken);
		var pending = await queries.FirstOrDefaultAsync((await events.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			x.Id == candidate.EventId && x.ProviderSettingId == candidate.ProviderId && x.ProviderMessageId == candidate.MessageId &&
			!x.IsDeleted && x.ProcessedAt == null), cancellationToken);
		if (pending == null) return false;

		var matches = (await FindDispatchesAsync(tenantId)).Where(x => x.ProviderSettingId == candidate.ProviderId &&
			x.ProviderMessageId == candidate.MessageId);
		var operations = await queries.ToListAsync(matches.Select(x => new Operation(x.Id, x.CampaignId!.Value)).Take(2), cancellationToken);
		if (operations.Count != 1) return false;
		var operation = operations[0];
		await outcomes.AcquireAsync(operation.CampaignId, cancellationToken);
		var dispatch = await queries.FirstOrDefaultAsync(matches.Where(x => x.Id == operation.DispatchId &&
			x.CampaignId == operation.CampaignId), cancellationToken);
		if (dispatch == null) return false;

		var protector = WhatsAppWebhookAppService.ReceiptProtector(protection, tenantId, candidate.ProviderId, pending.ProviderEventKey);
		var receipt = NotificationRuntimeJson.Deserialize<WhatsAppDeliveryReceipt>(protector.Unprotect(pending.ProtectedPayload),
			NotificationErrorCodes.StateConflict);
		if (receipt.MessageId != candidate.MessageId || !await MatchesAccountAsync(tenantId, dispatch, receipt, cancellationToken))
			return false;

		var status = DeliveryState(receipt.Status);
		var observed = receipt.ObservedAt > pending.ReceivedAt ? pending.ReceivedAt : receipt.ObservedAt;
		var changed = dispatch.ObserveWhatsAppReceipt(candidate.ProviderId, receipt.MessageId, status, observed);
		if (changed)
		{
			var projected = dispatch.AcceptedAt > observed ? dispatch.AcceptedAt.Value : observed;
			await delivery.RecordAsync(dispatch, projected, cancellationToken);
			await dispatches.UpdateAsync(dispatch, cancellationToken: cancellationToken);
			await NotificationOutcomeProjector.ReopenIntentAsync(intents, queries, tenantId, dispatch, cancellationToken);
		}
		pending.MarkProcessed(new DateTimeOffset(clock.Now.ToUniversalTime()));
		await events.UpdateAsync(pending, cancellationToken: cancellationToken);
		await write.CompleteAsync(cancellationToken);
		return true;
	}

	private async Task<IQueryable<NotificationDispatch>> FindDispatchesAsync(Guid tenantId) =>
		(await dispatches.GetQueryableAsync()).Where(d => d.TenantId == tenantId && d.Channel == CommunicationChannelType.WhatsApp &&
			!d.IsDeleted && d.ProviderSettingId != null && d.ProviderMessageId != null && d.IoStartedAt != null && d.IoLeaseId != null &&
			d.CampaignId != null && d.DeliveryAttemptId != null);

	private async Task<bool> MatchesAccountAsync(Guid tenantId, NotificationDispatch dispatch, WhatsAppDeliveryReceipt receipt,
		CancellationToken cancellationToken)
	{
		var binding = await queries.FirstOrDefaultAsync((await bindings.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			x.TemplateRevisionId == dispatch.TemplateRevisionId && x.ProviderSettingId == dispatch.ProviderSettingId && !x.IsDeleted),
			cancellationToken);
		return binding != null && WhatsAppApprovedTemplates.ReadParameters(binding, receipt.PhoneNumberId, receipt.BusinessAccountId) != null;
	}

	private static NotificationDispatchState DeliveryState(WhatsAppDeliveryReceiptStatus status) => status switch
	{
		WhatsAppDeliveryReceiptStatus.Sent => NotificationDispatchState.ProviderAccepted,
		WhatsAppDeliveryReceiptStatus.Delivered => NotificationDispatchState.Delivered,
		WhatsAppDeliveryReceiptStatus.Read => NotificationDispatchState.Read,
		WhatsAppDeliveryReceiptStatus.Failed => NotificationDispatchState.Failed,
		_ => throw new BusinessException(NotificationErrorCodes.StateConflict)
	};
}
