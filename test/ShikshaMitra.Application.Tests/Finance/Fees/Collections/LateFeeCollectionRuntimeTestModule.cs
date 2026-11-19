using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Seeders;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.TenantManagement;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Fees.Collections;

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public class LateFeeCollectionRuntimeTestModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		// These fixtures seed due dates and historical payments with DateTime.UtcNow.
		// Keep the application clock on the same calendar date after local midnight.
		Configure<AbpClockOptions>(options => options.Kind = DateTimeKind.Utc);
	}

	public override void OnApplicationInitialization(ApplicationInitializationContext context) =>
		AsyncHelper.RunSync(() => SeedTenantAsync(context));

	public override Task OnApplicationInitializationAsync(ApplicationInitializationContext context) => SeedTenantAsync(context);

	private static async Task SeedTenantAsync(ApplicationInitializationContext context)
	{
		// Receipts read the real tenant's school identity. Seed that prerequisite
		// through the same tenant contributor used by the finance API fixture.
		using var scope = context.ServiceProvider.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null);
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		await scope.ServiceProvider.GetRequiredService<TenantSeederContributor>().SeedAsync(new DataSeedContext());
		// The contributor injects IUnitOfWork directly; flush this fixture's
		// ambient unit of work before querying the inserted tenant.
		await uow.SaveChangesAsync();
		await scope.ServiceProvider.GetRequiredService<ITenantRepository>().GetAsync(TenantSeederContributor.DdvsTenantId);
		await uow.CompleteAsync();
	}
}
