using Xunit;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings;

// Setup must work for a clean school, with no dependency on demo academics or seeded identities.
public class InitialSetupTestFactory : ShikshaMitraApiTestFactory
{
	protected override bool InitializeTestData => false;
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		// The legacy tenant API explicitly invokes IDataSeeder. Use only one synthetic year,
		// never the demo catalogue, for its creation-path regression test.
		builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Transient<IDataSeeder, SyntheticTenantYearSeeder>()));
	}
}

public class SyntheticTenantYearSeeder(
	ICurrentTenant tenant, AcademicYearManager manager, IRepository<AcademicYear, Guid> years) : IDataSeeder
{
	public async Task SeedAsync(DataSeedContext context)
	{
		if (context.TenantId == null) return;
		using (tenant.Change(context.TenantId))
			await years.InsertAsync(await manager.CreateAsync("Synthetic legacy year", new DateTime(2026, 6, 1), new DateTime(2027, 5, 31)), autoSave: true);
	}
}

[CollectionDefinition(Name)]
public class InitialSetupTestCollection : ICollectionFixture<InitialSetupTestFactory>
{
	public const string Name = "Initial setup clean-school API";
}
