using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShikshaMitra.Seeders;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra;

[CollectionDefinition(Name, DisableParallelization = true)]
public class FinanceReceiptApiTestCollection : ICollectionFixture<FinanceReceiptApiTestFactory>
{
    public const string Name = "Finance receipt lifecycle API";
}

/// <summary>Same API host, authentication and SQLite setup; seed only receipt prerequisites.</summary>
public class FinanceReceiptApiTestFactory : ShikshaMitraApiTestFactory
{
    protected override bool InitializeTestData => false;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        Task.Run(async () =>
        {
            using (var scope = host.Services.CreateScope())
            using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
            {
                var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
                using (currentTenant.Change(null))
                    await scope.ServiceProvider.GetRequiredService<TenantSeederContributor>().SeedAsync(new DataSeedContext());
                using (currentTenant.Change(Guid.Parse("11111111-1111-1111-1111-111111111111")))
                    await scope.ServiceProvider.GetRequiredService<AcademicSetupSeederContributor>().SeedAsync(new DataSeedContext(currentTenant.Id));
                await uow.CompleteAsync();
            }
            using var lookupScope = host.Services.CreateScope();
            await ResolveSeededLookupsAsync(lookupScope.ServiceProvider, includeHrFixtures: false);
        }).GetAwaiter().GetResult();
        return host;
    }
}
