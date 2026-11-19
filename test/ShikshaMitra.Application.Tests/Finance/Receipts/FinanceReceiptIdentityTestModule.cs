using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Seeders;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance;

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public class FinanceReceiptIdentityTestModule : AbpModule
{
	public override void OnApplicationInitialization(ApplicationInitializationContext context) =>
		AsyncHelper.RunSync(() => SeedTenantAsync(context));

	public override Task OnApplicationInitializationAsync(ApplicationInitializationContext context) => SeedTenantAsync(context);

	private static async Task SeedTenantAsync(ApplicationInitializationContext context)
	{
		// Real receipt generation reads the school tenant; academic and payer rows alone are insufficient.
		using var scope = context.ServiceProvider.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null);
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		await scope.ServiceProvider.GetRequiredService<TenantSeederContributor>().SeedAsync(new DataSeedContext());
		await uow.SaveChangesAsync();
		await scope.ServiceProvider.GetRequiredService<ITenantRepository>().GetAsync(TenantSeederContributor.DdvsTenantId);
		await uow.CompleteAsync();
	}
}
