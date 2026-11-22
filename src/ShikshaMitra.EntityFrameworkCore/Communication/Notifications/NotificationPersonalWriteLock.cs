using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.EntityFrameworkCore.Communication.Notifications;

[ExposeServices(typeof(INotificationPersonalWriteLock))]
public sealed class NotificationPersonalWriteLock(IDbContextProvider<ShikshaMitraDbContext> contexts, ICurrentTenant tenant)
	: INotificationPersonalWriteLock, ITransientDependency
{
	public async Task AcquireAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken)
	{
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new InvalidOperationException("Personal notification writes require a tenant.");
		var db = await contexts.GetDbContextAsync();
		// SQLite request fixtures are serial; real concurrency is tested on PostgreSQL.
		if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite") return;
		await NotificationPostgresPersonalLocks.AcquireAsync(db.Database.GetDbConnection(), db.Database.CurrentTransaction?.GetDbTransaction(),
			tenantId, keys, cancellationToken);
	}
}
