using System;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using ShikshaMitra.Seeders;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra;
public class ShikshaMitraSeederContributor : IDataSeedContributor, ITransientDependency
{
	private readonly IServiceProvider _serviceProvider;

	public ShikshaMitraSeederContributor(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	public async Task SeedAsync(DataSeedContext context)
	{
		using (var scope = _serviceProvider.CreateScope())
		{
			// Seed tenant first.
			var tenantSeeder = scope.ServiceProvider.GetRequiredService<TenantSeederContributor>();
			await tenantSeeder.SeedAsync(context);

			// Retrieve the tenant ID from the context.
			if (!context.Properties.TryGetValue("TenantId", out var tenantIdObj) || tenantIdObj == null)
			{
				return;
			}
			Guid tenantId = (Guid) tenantIdObj;

			// Resolve ICurrentTenant and change the tenant context.
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			using (currentTenant.Change(tenantId))
			{
				// Now that the tenant is set, run the remaining seeders.
				var rolesSeeder = scope.ServiceProvider.GetRequiredService<RolesSeederContributor>();
				await rolesSeeder.SeedAsync(context);

				var hrSeeder = scope.ServiceProvider.GetRequiredService<HRSeederContributor>();
				await hrSeeder.SeedAsync(context);

				var studentSeeder = scope.ServiceProvider.GetRequiredService<StudentSeederContributor>();
				await studentSeeder.SeedAsync(context);
			}
		}
	}
}
