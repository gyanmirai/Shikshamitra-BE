using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ShikshaMitra.EntityFrameworkCore.Finance.Fees.Receipts;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Seeders;
using ShikshaMitra.Students;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Finance.Concessions;

[CollectionDefinition(Name, DisableParallelization = true)]
public class ConcessionAutomaticRefundApiTestCollection : ICollectionFixture<ConcessionAutomaticRefundApiTestFactory>
{
	public const string Name = "Concession automatic refund API";
}

public class ConcessionAutomaticRefundApiTestFactory : ShikshaMitraApiTestFactory
{
	public ConcurrentQueue<string> ServerErrors { get; } = new();
	private static string ConnectionString => Environment.GetEnvironmentVariable("SHIKSHAMITRA_REFUND_TEST_DB")
		?? throw new InvalidOperationException("Set SHIKSHAMITRA_REFUND_TEST_DB to a disposable migrated PostgreSQL test database.");

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
			new Dictionary<string, string?>
			{
				[ShikshaMitraSeederProfile.ContextPropertyName] = ShikshaMitraSeederProfile.Minimal,
				["ConnectionStrings:Default"] = ConnectionString,
			}));
		builder.ConfigureTestServices(services =>
		{
			// This flow needs scalar student identity/scope. Retain real EF, filters and
			// explicit relationships while avoiding recursive academic AutoInclude joins.
			services.Replace(ServiceDescriptor.Transient<IRepository<Student, Guid>, ConcessionStudentTestRepository>());
			services.AddTransient<IReceiptNumberSequenceRepository, EfCoreReceiptNumberSequenceRepository>();
			services.Replace(ServiceDescriptor.Singleton<ILoggerFactory>(_ => LoggerFactory.Create(
				logging => logging.AddProvider(new ExceptionLoggerProvider(ServerErrors)))));
		});
	}

	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		var connection = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString);
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			connection.Database?.StartsWith("refund_workflow_test_", StringComparison.OrdinalIgnoreCase) != true)
			throw new InvalidOperationException("Concession refund API tests require a disposable loopback refund_workflow_test_ PostgreSQL database.");
		services.Configure<AbpDbContextOptions>(options => options.Configure(context =>
			context.DbContextOptions.UseNpgsql(ConnectionString)));
	}

	private class ExceptionLoggerProvider(ConcurrentQueue<string> errors) : ILoggerProvider
	{
		public ILogger CreateLogger(string categoryName) => new ExceptionLogger(errors);
		public void Dispose() { }
		private class ExceptionLogger(ConcurrentQueue<string> errors) : ILogger
		{
			public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
			public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
				Func<TState, Exception?, string> formatter)
			{
				if (exception is not null && IsEnabled(logLevel)) errors.Enqueue(exception.ToString());
			}
		}
	}
}
