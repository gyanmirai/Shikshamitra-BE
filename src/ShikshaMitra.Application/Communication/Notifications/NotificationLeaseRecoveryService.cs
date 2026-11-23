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
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationRecoveryResult(int Intents, int Dispatches, int IntentLeases, int DispatchLeases);

[RemoteService(false), DisableAuditing]
public sealed class NotificationLeaseRecoveryService(ICurrentTenant tenant, IUnitOfWorkManager units,
	IRepository<NotificationIntent, Guid> intents, IRepository<NotificationDispatch, Guid> dispatches, IAsyncQueryableExecuter queries,
	NotificationDeliveryProjection delivery, IRepository<CommunicationRecipientSnapshot, Guid> recipients, IDataFilter filters)
	: ITransientDependency
{
	public async Task<NotificationRecoveryResult> RecoverAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
	{
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (batchSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(batchSize));
		for (var attempt = 0; ; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using var unit = units.Begin(requiresNew: true, isTransactional: true);
			try
			{
				var intentsRecovered = await RecoverIntentsAsync(tenantId, now.ToUniversalTime(), batchSize, cancellationToken);
				var dispatchesRecovered = await RecoverDispatchesAsync(tenantId, now.ToUniversalTime(), batchSize, cancellationToken);
				await unit.CompleteAsync(cancellationToken);
				return new(intentsRecovered.Updated, dispatchesRecovered.Updated, intentsRecovered.Leases, dispatchesRecovered.Leases);
			}
			catch (AbpDbConcurrencyException) when (attempt < 2)
			{
				// Another worker can finish a lease or sweep the same row first.
			}
		}
	}

	internal async Task RecoverTemplateTestPilotAsync(NotificationTemplateTestPilot pilot, DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		if (tenant.Id != pilot.TenantId || pilot.TenantId == Guid.Empty)
			throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (!pilot.HasSupportedChannel) return;
		using var unit = units.Begin(requiresNew: true, isTransactional: true);
		List<NotificationDispatch> candidates;
		using (filters.Disable<ISoftDelete>())
			candidates = await queries.ToListAsync((await dispatches.GetQueryableAsync())
				.Where(x => x.TenantId == pilot.TenantId && x.CampaignId == pilot.OperationId).Take(2), cancellationToken);
		if (candidates is not [var row] || row.IsDeleted || row.Channel != pilot.Channel || !row.ExpiresAt.HasValue ||
			row.ProviderSettingId.HasValue != (pilot.Channel == CommunicationChannelType.Email)) return;
		var recoverable = row.Status == NotificationDispatchState.Processing && row.AttemptNumber == 1 && row.LeaseExpiresAt <= now ||
			row.Status == NotificationDispatchState.Pending && row.AttemptNumber == 0 && row.ExpiresAt <= now;
		if (!recoverable) return;
		if (!await queries.AnyAsync((await intents.GetQueryableAsync()).Where(x => x.Id == row.IntentId &&
			x.TenantId == pilot.TenantId && x.CampaignId == pilot.OperationId && !x.IsDeleted &&
			x.SourceType == NotificationCampaignTypes.AcademicTemplateTestSource), cancellationToken)) return;
		if (!await queries.AnyAsync((await recipients.GetQueryableAsync()).Where(x => x.Id == row.RecipientSnapshotId &&
			x.TenantId == pilot.TenantId && x.CampaignId == pilot.OperationId && !x.IsDeleted &&
			x.UserId == pilot.RecipientUserId && x.RecipientType == "TestAccount" && x.IsEligible), cancellationToken)) return;
		var recipient = NotificationRuntimeJson.Deserialize<NotificationRecipientRefDto>(row.RecipientReferenceJson,
			NotificationErrorCodes.RuntimeSnapshotInvalid);
		if (recipient.UserId != pilot.RecipientUserId || recipient.StudentId.HasValue) return;
		if (!row.RecoverExpiredLease(now) && !row.ExpirePending(now)) return;
		await PersistRecoveredDispatchAsync(row, now, cancellationToken);
		await unit.CompleteAsync(cancellationToken);
	}

	private async Task<(int Updated, int Leases)> RecoverIntentsAsync(Guid tenantId, DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
	{
		var rows = await queries.ToListAsync((await intents.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			(x.Status == NotificationIntentState.Processing && x.LeaseExpiresAt <= now ||
				(x.Status == NotificationIntentState.Ready || x.Status == NotificationIntentState.Scheduled) &&
				x.LeaseId == null && x.ExpiresAt <= now))
			.OrderBy(x => x.Status == NotificationIntentState.Processing ? x.LeaseExpiresAt : x.ExpiresAt)
			.ThenBy(x => x.Id).Take(batchSize), cancellationToken);
		var count = 0;
		var leases = 0;
		foreach (var row in rows)
		{
			var recovered = row.RecoverExpiredLease(now);
			if (!recovered && !row.ExpirePending(now)) continue;
			await intents.UpdateAsync(row, cancellationToken: cancellationToken);
			count++;
			if (recovered) leases++;
		}
		return (count, leases);
	}

	private async Task<(int Updated, int Leases)> RecoverDispatchesAsync(Guid tenantId, DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
	{
		var rows = await queries.ToListAsync((await dispatches.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			(x.Status == NotificationDispatchState.Processing && x.LeaseExpiresAt <= now ||
				x.Status == NotificationDispatchState.Pending && x.LeaseId == null && x.IoStartedAt == null && x.ExpiresAt <= now))
			.OrderBy(x => x.Status == NotificationDispatchState.Processing ? x.LeaseExpiresAt : x.ExpiresAt)
			.ThenBy(x => x.Id).Take(batchSize), cancellationToken);
		var count = 0;
		var leases = 0;
		foreach (var row in rows)
		{
			var recovered = row.RecoverExpiredLease(now);
			if (!recovered && !row.ExpirePending(now)) continue;
			await PersistRecoveredDispatchAsync(row, now, cancellationToken);
			count++;
			if (recovered) leases++;
		}
		return (count, leases);
	}

	private async Task PersistRecoveredDispatchAsync(NotificationDispatch row, DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		if (row.Status == NotificationDispatchState.UnknownAcceptance) await delivery.RecordAsync(row, now, cancellationToken);
		await dispatches.UpdateAsync(row, cancellationToken: cancellationToken);
	}
}
