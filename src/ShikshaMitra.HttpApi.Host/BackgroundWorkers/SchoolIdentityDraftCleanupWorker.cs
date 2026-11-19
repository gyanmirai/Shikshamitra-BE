using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.BlobStoring;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

public class SchoolIdentityDraftCleanupWorker : AsyncPeriodicBackgroundWorkerBase
{
	public SchoolIdentityDraftCleanupWorker(AbpAsyncTimer timer, IServiceScopeFactory factory) : base(timer, factory)
	{
		Timer.Period = 60 * 60 * 1000;
	}

	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext context)
	{
		var services = context.ServiceProvider;
		var tenants = services.GetRequiredService<ITenantRepository>();
		var currentTenant = services.GetRequiredService<ICurrentTenant>();
		var units = services.GetRequiredService<IUnitOfWorkManager>();
		var clock = services.GetRequiredService<IClock>();
		var logger = services.GetRequiredService<ILogger<SchoolIdentityDraftCleanupWorker>>();
		foreach (var tenant in await tenants.GetListAsync())
		{
			try
			{
				using var tenantScope = currentTenant.Change(tenant.Id);
				var assets = services.GetRequiredService<ISchoolIdentityAssetRepository>();
				var cutoff = clock.Now.AddDays(-7);
				System.Collections.Generic.List<SchoolIdentityAsset> expired;
				using (var read = units.Begin(requiresNew: true, isTransactional: false))
				{
					var query = await assets.GetQueryableAsync();
					expired = await services.GetRequiredService<IAsyncQueryableExecuter>().ToListAsync(query
						.Where(asset => asset.TenantId == tenant.Id && !asset.IsRetained
							&& (asset.IsCleanupPending || asset.CreationTime <= cutoff))
						.OrderBy(asset => asset.CreationTime).Take(100));
					await read.CompleteAsync();
				}
				foreach (var asset in expired)
				{
					try
					{
						if (!asset.IsCleanupPending)
						{
							using var reservation = units.Begin(requiresNew: true, isTransactional: true);
							if (!await assets.TryClaimExpiredDraftAsync(asset.Id, cutoff)) continue;
							await reservation.CompleteAsync();
						}
						// Keep the reservation committed if external blob deletion or metadata cleanup fails.
						using var cleanup = units.Begin(requiresNew: true, isTransactional: true);
						await services.GetRequiredService<IBlobContainer>().DeleteAsync(asset.BlobName);
						await assets.CompleteDraftCleanupAsync(asset.Id);
						await cleanup.CompleteAsync();
					}
					catch (Exception failure)
					{
						logger.LogError(failure, "School identity draft cleanup will retry asset {AssetId} for tenant {TenantId}.", asset.Id, tenant.Id);
					}
				}
			}
			catch (Exception failure)
			{
				logger.LogError(failure, "School identity draft cleanup failed for tenant {TenantId}.", tenant.Id);
			}
		}
	}
}
