using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Performance;

public sealed class AcademicPerformancePostgresFactAttribute : FactAttribute
{
	public AcademicPerformancePostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ACADEMIC_PERF_TEST_POSTGRES")))
			Skip = "Set ACADEMIC_PERF_TEST_POSTGRES to a new empty loopback academic_tests_* PostgreSQL database.";
	}
}

public sealed class AcademicPerformancePostgresTestFactory : ShikshaMitraApiTestFactory
{
	private bool _preparingFixture = true;
	public AcademicCommandMetrics Metrics { get; } = new();
	public string ConnectionString { get; private set; } = string.Empty;

	protected override IHost CreateHost(IHostBuilder builder)
	{
		var host = base.CreateHost(builder);
		_preparingFixture = false;
		return host;
	}

	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ACADEMIC_PERF_TEST_POSTGRES"));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			connection.Database?.StartsWith("academic_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("Academic benchmarks require a disposable loopback academic_tests_* database.");
		ConnectionString = connection.ConnectionString;
		services.Configure<AbpDbContextOptions>(options => options.Configure(context =>
			context.DbContextOptions.UseNpgsql(ConnectionString, provider => provider.UseQuerySplittingBehavior(
				_preparingFixture ? QuerySplittingBehavior.SplitQuery : QuerySplittingBehavior.SingleQuery)).AddInterceptors(Metrics)));
		using var database = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql(ConnectionString).Options);
		// Refuse an existing schema. This harness never resets or deletes a database.
		database.GetService<IRelationalDatabaseCreator>().CreateTables();
	}
}
