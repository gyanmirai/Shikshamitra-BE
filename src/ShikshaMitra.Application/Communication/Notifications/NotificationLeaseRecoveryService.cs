using System;
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

public sealed record NotificationRecoveryResult(int Intents, int Dispatches);

[RemoteService(false), DisableAuditing]
public sealed class NotificationLeaseRecoveryService(ICurrentTenant tenant, IUnitOfWorkManager units,
	IRepository<NotificationIntent, Guid> intents, IRepository<NotificationDispatch, Guid> dispatches, IAsyncQueryableExecuter queries,
	NotificationDeliveryProjection delivery)
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
				var intentCount = await RecoverIntentsAsync(tenantId, now.ToUniversalTime(), batchSize, cancellationToken);
				var dispatchCount = await RecoverDispatchesAsync(tenantId, now.ToUniversalTime(), batchSize, cancellationToken);
				await unit.CompleteAsync(cancellationToken);
				return new(intentCount, dispatchCount);
			}
			catch (AbpDbConcurrencyException) when (attempt < 2)
			{
				// Another worker can finish a lease or sweep the same row first.
			}
		}
	}

	private async Task<int> RecoverIntentsAsync(Guid tenantId, DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
	{
		var rows = await queries.ToListAsync((await intents.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			(x.Status == NotificationIntentState.Processing && x.LeaseExpiresAt <= now ||
				(x.Status == NotificationIntentState.Ready || x.Status == NotificationIntentState.Scheduled) &&
				x.LeaseId == null && x.ExpiresAt <= now))
			.OrderBy(x => x.Status == NotificationIntentState.Processing ? x.LeaseExpiresAt : x.ExpiresAt)
			.ThenBy(x => x.Id).Take(batchSize), cancellationToken);
		var count = 0;
		foreach (var row in rows)
		{
			if (!row.RecoverExpiredLease(now) && !row.ExpirePending(now)) continue;
			await intents.UpdateAsync(row, cancellationToken: cancellationToken);
			count++;
		}
		return count;
	}

	private async Task<int> RecoverDispatchesAsync(Guid tenantId, DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
	{
		var rows = await queries.ToListAsync((await dispatches.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			(x.Status == NotificationDispatchState.Processing && x.LeaseExpiresAt <= now ||
				x.Status == NotificationDispatchState.Pending && x.LeaseId == null && x.IoStartedAt == null && x.ExpiresAt <= now))
			.OrderBy(x => x.Status == NotificationDispatchState.Processing ? x.LeaseExpiresAt : x.ExpiresAt)
			.ThenBy(x => x.Id).Take(batchSize), cancellationToken);
		var count = 0;
		foreach (var row in rows)
		{
			if (!row.RecoverExpiredLease(now) && !row.ExpirePending(now)) continue;
			if (row.Status == NotificationDispatchState.UnknownAcceptance) await delivery.RecordAsync(row, now, cancellationToken);
			await dispatches.UpdateAsync(row, cancellationToken: cancellationToken);
			count++;
		}
		return count;
	}
}
