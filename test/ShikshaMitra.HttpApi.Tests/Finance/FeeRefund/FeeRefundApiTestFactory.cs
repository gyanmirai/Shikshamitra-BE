using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.TestHost;
using ShikshaMitra.Seeders;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.EntityFrameworkCore.Finance.Fees.Receipts;
using ShikshaMitra.Students.Records;
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
    public StudentRecordsInsertBarrier Barrier { get; } = new();
    public ConcurrentQueue<string> ServerErrors { get; } = new();
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
        builder.ConfigureTestServices(services =>
        {
            // Host uses Serilog without provider forwarding. Capture only test exception diagnostics.
            services.Replace(ServiceDescriptor.Singleton<ILoggerFactory>(_ =>
                LoggerFactory.Create(logging => logging.AddProvider(new RefundExceptionLoggerProvider(ServerErrors)))));
            services.AddTransient<IReceiptNumberSequenceRepository, EfCoreReceiptNumberSequenceRepository>();
        });
    }

    protected override void ConfigureTestDatabase(IServiceCollection services)
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString);
        if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database == null ||
            !connection.Database.StartsWith("refund_workflow_test_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refund API tests require a disposable loopback refund_workflow_test_ PostgreSQL database.");
        services.Configure<AbpDbContextOptions>(options =>
            options.Configure(context => context.DbContextOptions.UseNpgsql(ConnectionString)
                .AddInterceptors(Barrier, new StudentRecordsTransactionFailureObserver(Barrier))));
    }

    private sealed class RefundExceptionLoggerProvider(ConcurrentQueue<string> errors) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new RefundExceptionLogger(errors, categoryName);
        public void Dispose() { }

        private sealed class RefundExceptionLogger(ConcurrentQueue<string> errors, string categoryName) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (exception != null && IsEnabled(logLevel)) errors.Enqueue($"{categoryName}\n{exception}");
            }
        }
    }
}
