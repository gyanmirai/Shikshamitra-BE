using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HostTenants;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationWorkerTenantPage(IReadOnlyList<Guid> TenantIds, int NextOffset);

[RemoteService(false)]
public sealed class NotificationWorkerTenants(ICurrentTenant tenant, IUnitOfWorkManager units, IHostTenantQueryRepository schools)
	: ITransientDependency
{
	public async Task<NotificationWorkerTenantPage> ReadPageAsync(int offset, int pageSize, CancellationToken cancellationToken)
	{
		RequireHost();
		if (offset < 0 || pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
		cancellationToken.ThrowIfCancellationRequested();
		using var unit = units.Begin(requiresNew: true, isTransactional: false);
		var (count, rows) = await schools.GetPageAsync(null, true, "name", offset, pageSize);
		var ids = rows.Select(x => x.Id).ToArray();
		var next = (long)offset + rows.Count;
		await unit.CompleteAsync(cancellationToken);
		return new(ids, rows.Count == 0 || next >= count || next > int.MaxValue ? 0 : (int)next);
	}

	public async Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken)
	{
		RequireHost();
		cancellationToken.ThrowIfCancellationRequested();
		using var unit = units.Begin(requiresNew: true, isTransactional: false);
		var active = await schools.IsActiveAsync(tenantId);
		await unit.CompleteAsync(cancellationToken);
		return active;
	}

	private void RequireHost()
	{
		if (tenant.Id.HasValue || units.Current != null) throw new BusinessException(NotificationErrorCodes.StateConflict);
	}
}
