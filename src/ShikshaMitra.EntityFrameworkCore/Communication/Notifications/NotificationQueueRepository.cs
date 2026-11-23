using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.EntityFrameworkCore.Communication.Notifications;

public sealed class NotificationQueueRepository(IDbContextProvider<ShikshaMitraDbContext> contexts,
	ICurrentTenant currentTenant) : INotificationQueueRepository, ITransientDependency
{
	public async Task<NotificationQueueHealth> ReadHealthAsync(DateTimeOffset now, CancellationToken cancellationToken)
	{
		var tenantId = RequireTenant();
		var context = await contexts.GetDbContextAsync();
		return await NotificationPostgresQueueHealth.ReadAsync(context.Database.GetDbConnection(), tenantId, now, cancellationToken);
	}

	public Task<IReadOnlyList<NotificationQueueClaim>> ClaimIntentsAsync(DateTimeOffset now, int batchSize,
		TimeSpan leaseDuration, CancellationToken cancellationToken) =>
		ClaimAsync(NotificationQueueKind.Intent, now, batchSize, leaseDuration, cancellationToken);

	public Task<IReadOnlyList<NotificationQueueClaim>> ClaimDispatchesAsync(DateTimeOffset now, int batchSize,
		TimeSpan leaseDuration, CancellationToken cancellationToken, int? maxActivePerChannel = null,
		IReadOnlyList<CommunicationChannelType>? pausedChannels = null, NotificationTemplateTestPilot? pilot = null) =>
		ClaimAsync(NotificationQueueKind.Dispatch, now, batchSize, leaseDuration, cancellationToken, maxActivePerChannel, pausedChannels, pilot);

	public async Task<bool> RenewDispatchAsync(Guid dispatchId, Guid leaseId, DateTimeOffset now, TimeSpan leaseDuration,
		CancellationToken cancellationToken)
	{
		var tenantId = RequireTenant();
		var context = await contexts.GetDbContextAsync();
		return await NotificationPostgresQueueClaims.RenewDispatchAsync(context.Database.GetDbConnection(),
			context.Database.CurrentTransaction?.GetDbTransaction(), tenantId, dispatchId, leaseId, now, leaseDuration, cancellationToken);
	}

	private async Task<IReadOnlyList<NotificationQueueClaim>> ClaimAsync(NotificationQueueKind kind,
		DateTimeOffset now, int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken, int? maxActivePerChannel = null,
		IReadOnlyList<CommunicationChannelType>? pausedChannels = null, NotificationTemplateTestPilot? pilot = null)
	{
		var tenantId = RequireTenant();
		var context = await contexts.GetDbContextAsync();
		return await NotificationPostgresQueueClaims.ClaimAsync(context.Database.GetDbConnection(),
			context.Database.CurrentTransaction?.GetDbTransaction(), tenantId, kind, now,
			batchSize, leaseDuration, cancellationToken, maxActivePerChannel, pausedChannels, pilot);
	}

	private Guid RequireTenant() => currentTenant.Id is Guid tenantId && tenantId != Guid.Empty
		? tenantId : throw new BusinessException(NotificationErrorCodes.TenantRequired);
}
