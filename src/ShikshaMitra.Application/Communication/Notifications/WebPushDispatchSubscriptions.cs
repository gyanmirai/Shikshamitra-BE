using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

internal sealed record WebPushDispatchTargets(bool IsConfigured, ILookup<Guid, Guid> Subscriptions);

[RemoteService(false), DisableAuditing]
public sealed class WebPushDispatchSubscriptions(ICurrentTenant tenant, IRepository<WebPushSubscription, Guid> subscriptions,
	IAsyncQueryableExecuter queries, WebPushKeyConfiguration configuration) : ITransientDependency
{
	internal async Task<WebPushDispatchTargets> LoadAsync(Guid[] users, CancellationToken cancellationToken, Guid? subscriptionId = null)
	{
		var keys = users.Length > 0 ? configuration.Read() : null;
		if (keys == null) return new(false, Array.Empty<Guid>().ToLookup(x => x));
		var eligible = (await ActiveAsync(keys.Version)).Where(x => Enumerable.Contains(users, x.UserId));
		if (subscriptionId is Guid selected) eligible = eligible.Where(x => x.Id == selected);
		var rows = await queries.ToListAsync(eligible.Select(x => new { x.UserId, x.Id }), cancellationToken);
		return new(true, rows.ToLookup(x => x.UserId, x => x.Id));
	}

	internal async Task<bool> IsAvailableAsync(Guid user, Guid id, CancellationToken cancellationToken) =>
		configuration.Read() is { } keys && await FindAsync(user, id, keys.Version, cancellationToken) != null;

	internal async Task<WebPushSubscription?> FindAsync(Guid user, Guid id, string version, CancellationToken cancellationToken) =>
		await queries.FirstOrDefaultAsync((await ActiveAsync(version)).Where(x => x.Id == id && x.UserId == user), cancellationToken);

	internal Task UpdateAsync(WebPushSubscription row, CancellationToken cancellationToken) =>
		subscriptions.UpdateAsync(row, cancellationToken: cancellationToken);

	private async Task<IQueryable<WebPushSubscription>> ActiveAsync(string version)
	{
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		return (await subscriptions.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			!x.IsDeleted && x.IsEnabled && x.VapidKeyVersion == version);
	}
}
