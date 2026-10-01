using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Seeders;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Finance.FeeRefunds;

[CollectionDefinition(Name)]
public sealed class FeeRefundApiTestCollection : ICollectionFixture<FeeRefundApiTestFactory>
{
    public const string Name = "Fee Refund API";
}

public sealed class FeeRefundApiTestFactory : ShikshaMitraApiTestFactory
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("SHIKSHAMITRA_REFUND_TEST_DB")
        ?? throw new InvalidOperationException("Set SHIKSHAMITRA_REFUND_TEST_DB to a migrated disposable PostgreSQL refund test database.");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ShikshaMitraSeederProfile.ContextPropertyName] = ShikshaMitraSeederProfile.Minimal,
                ["ConnectionStrings:Default"] = ConnectionString
            }));
    }

    protected override void ConfigureTestDatabase(IServiceCollection services)
    {
        var database = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString).Database;
        if (database == null || !database.StartsWith("refund_workflow_test_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refund API tests require a disposable refund_workflow_test_ PostgreSQL database.");
        services.Configure<AbpDbContextOptions>(options =>
            options.Configure(context => context.DbContextOptions.UseNpgsql(ConnectionString)));
    }
}
