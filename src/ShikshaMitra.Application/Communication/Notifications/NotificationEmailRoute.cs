using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

internal sealed record NotificationEmailProvider(Guid Id, bool ConsentRequired);

[RemoteService(false)]
public sealed class NotificationEmailRoute(ICurrentTenant tenant, IRepository<ChannelProviderSetting, Guid> providers,
	IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<NotificationEmailProvider?> FindAsync(CancellationToken cancellationToken)
	{
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty)
			throw new BusinessException(NotificationErrorCodes.TenantRequired);
		var rows = await queries.ToListAsync((await providers.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Channel == CommunicationChannelType.Email && x.IsEnabled &&
				x.Status == CommunicationProviderStatus.Enabled)
			.OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.CreationTime).ThenBy(x => x.Id)
			.Select(x => new { x.Id, x.ProviderName, x.ConsentRequired }), cancellationToken);
		var provider = rows.FirstOrDefault(x => string.Equals(x.ProviderName.Trim(), "SMTP", StringComparison.OrdinalIgnoreCase));
		return provider == null ? null : new(provider.Id, provider.ConsentRequired);
	}
}
