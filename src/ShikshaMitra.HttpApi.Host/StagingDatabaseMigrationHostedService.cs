using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Data;

namespace ShikshaMitra;

public sealed class StagingDatabaseMigrationHostedService(
	IServiceScopeFactory serviceScopeFactory,
	IConfiguration configuration,
	ILogger<StagingDatabaseMigrationHostedService> logger)
	: BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!configuration.GetValue<bool>("DatabaseMigrations:RunOnStartup"))
		{
			return;
		}

		try
		{
			await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
			logger.LogInformation("Running configured database migrations in the app worker.");

			await using var scope = serviceScopeFactory.CreateAsyncScope();
			await scope.ServiceProvider
				.GetRequiredService<ShikshaMitraDbMigrationService>()
				.MigrateAsync();

			logger.LogInformation("Configured database migrations completed in the app worker.");
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			logger.LogInformation("Database migration worker stopped during application shutdown.");
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Database migration worker failed.");
		}
	}
}
