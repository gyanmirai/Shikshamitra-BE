using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.EntityFrameworkCore.Communication.Notifications;

public sealed class NotificationQueueRepository(IDbContextProvider<ShikshaMitraDbContext> contexts,
	ICurrentTenant currentTenant) : INotificationQueueRepository, ITransientDependency
{
	public Task<IReadOnlyList<NotificationQueueClaim>> ClaimIntentsAsync(DateTimeOffset now, int batchSize,
		TimeSpan leaseDuration, CancellationToken cancellationToken) =>
		ClaimAsync(NotificationQueueKind.Intent, now, batchSize, leaseDuration, cancellationToken);

	public Task<IReadOnlyList<NotificationQueueClaim>> ClaimDispatchesAsync(DateTimeOffset now, int batchSize,
		TimeSpan leaseDuration, CancellationToken cancellationToken) =>
		ClaimAsync(NotificationQueueKind.Dispatch, now, batchSize, leaseDuration, cancellationToken);

	private async Task<IReadOnlyList<NotificationQueueClaim>> ClaimAsync(NotificationQueueKind kind,
		DateTimeOffset now, int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken)
	{
		var tenantId = currentTenant.Id;
		if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
			throw new BusinessException(NotificationErrorCodes.TenantRequired);
		var context = await contexts.GetDbContextAsync();
		return await NotificationPostgresQueueClaims.ClaimAsync(context.Database.GetDbConnection(),
			context.Database.CurrentTransaction?.GetDbTransaction(), tenantId.Value, kind, now,
			batchSize, leaseDuration, cancellationToken);
	}
}
