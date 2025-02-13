using System;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using ShikshaMitra.Seeders;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.MultiTenancy;
using System.Collections.Generic;

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
			// Seed tenants first.
			var tenantSeeder = scope.ServiceProvider.GetRequiredService<Seeders.TenantSeederContributor>();
			await tenantSeeder.SeedAsync(context);

			// Retrieve tenant IDs from the context.
			if (!context.Properties.TryGetValue("TenantIds", out var tenantIdsObj) || tenantIdsObj == null)
			{
				return;
			}
			var tenantIds = tenantIdsObj as List<Guid>;
			if (tenantIds == null || tenantIds.Count == 0)
			{
				return;
			}

			// Resolve ICurrentTenant to change the tenant context.
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

			// Iterate through each tenant ID and seed remaining data.
			foreach (var tenantId in tenantIds)
			{
				using (currentTenant.Change(tenantId))
				{
					// Now that the tenant is set, run the remaining seeders.
					var rolesSeeder = scope.ServiceProvider.GetRequiredService<Seeders.RolesSeederContributor>();
					await rolesSeeder.SeedAsync(context);

					var hrSeeder = scope.ServiceProvider.GetRequiredService<Seeders.HRSeederContributor>();
					await hrSeeder.SeedAsync(context);

					var studentSeeder = scope.ServiceProvider.GetRequiredService<Seeders.StudentSeederContributor>();
					await studentSeeder.SeedAsync(context);
				}
			}
		}
	}
}
