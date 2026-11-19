using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Seeders;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

[CollectionDefinition(Name)]
public sealed class FinanceRoutingApiTestCollection : ICollectionFixture<FinanceRoutingApiTestFactory>
{
    public const string Name = "Finance Routing API";
}

/// <summary>Uses an isolated SQLite database and foundational seeds for routing contract tests.</summary>
public sealed class FinanceRoutingApiTestFactory : ShikshaMitraApiTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ShikshaMitraSeederProfile.ContextPropertyName] = ShikshaMitraSeederProfile.Minimal
            }));
    }
}
