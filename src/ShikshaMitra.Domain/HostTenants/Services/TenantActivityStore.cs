using System;
using System.Threading.Tasks;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.ObjectMapping;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.HostTenants;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(ITenantStore), typeof(TenantStore), IncludeSelf = true)]
public class TenantActivityStore(
	ITenantRepository repository, IObjectMapper<AbpTenantManagementDomainModule> mapper,
	ICurrentTenant currentTenant, IDistributedCache<TenantConfigurationCacheItem> cache,
	TenantActivityChecker checker) : TenantStore(repository, mapper, currentTenant, cache)
{
	public override async Task<TenantConfiguration> FindAsync(Guid id) => await WithStatusAsync(await base.FindAsync(id));
	public override async Task<TenantConfiguration> FindAsync(string normalizedName) => await WithStatusAsync(await base.FindAsync(normalizedName));
	private async Task<TenantConfiguration> WithStatusAsync(TenantConfiguration configuration)
	{
		if (configuration == null) return null!;
		// Never trust a distributed cached status: revocation must work across host instances.
		return new TenantConfiguration(configuration.Id, configuration.Name)
		{
			NormalizedName = configuration.NormalizedName,
			ConnectionStrings = configuration.ConnectionStrings,
			IsActive = await checker.IsActiveAsync(configuration.Id)
		};
	}
}
