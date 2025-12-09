using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
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

				// Seed Academic Setup data (must be seeded before HR/Employee data)
				var academicSetupSeeder = scope.ServiceProvider.GetRequiredService<Seeders.AcademicSetupSeederContributor>();
				await academicSetupSeeder.SeedAsync(context);

				// Seed HR module data
				var hrSeeder = scope.ServiceProvider.GetRequiredService<Seeders.HRSeederContributor>();
				await hrSeeder.SeedAsync(context);

				// Seed Student data (after Academic Setup)
				var studentSeeder = scope.ServiceProvider.GetRequiredService<Seeders.StudentSeederContributor>();
				await studentSeeder.SeedAsync(context);
			}
		}
	}
}
}
