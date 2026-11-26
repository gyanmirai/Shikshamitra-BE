using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.HR.Setup;

public class HrSetupReadinessTestFactory : ShikshaMitraApiTestFactory
{
	protected override bool InitializeTestData => false;

	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		var connectionString = Environment.GetEnvironmentVariable("HR_SETUP_TEST_POSTGRES");
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			base.ConfigureTestDatabase(services);
			return;
		}

		var settings = new NpgsqlConnectionStringBuilder(connectionString);
		if (settings.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			settings.Database?.StartsWith("hr_setup_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("HR setup tests require an empty loopback hr_setup_tests_* database.");

		services.Configure<AbpDbContextOptions>(options => options.Configure(context =>
			context.DbContextOptions.UseNpgsql(connectionString)));
		using var db = new ShikshaMitraDbContext(
			new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connectionString).Options);
		db.GetService<IRelationalDatabaseCreator>().CreateTables();
	}
}

[CollectionDefinition(Name)]
public class HrSetupReadinessTestCollection : ICollectionFixture<HrSetupReadinessTestFactory>
{
	public const string Name = "HR setup readiness clean-school API";
}
