using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace ShikshaMitra.DbMigrator;

static class Program
{
	static async Task<int> Main(string[] args)
	{
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Information()
			.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
			.MinimumLevel.Override("Volo.Abp", LogEventLevel.Warning)
#if DEBUG
				.MinimumLevel.Override("ShikshaMitra", LogEventLevel.Debug)
#else
				.MinimumLevel.Override("ShikshaMitra", LogEventLevel.Information)
#endif
				.Enrich.FromLogContext()
			.WriteTo.Async(c => c.File("Logs/logs.txt"))
			.WriteTo.Async(c => c.Console())
			.CreateLogger();

		try
		{
			if (Array.IndexOf(args, "--check-bootstrap-configuration") >= 0)
			{
				using var host = CreateHostBuilder(args).Build();
				var configuration = host.Services.GetRequiredService<IConfiguration>();
				var environment = host.Services.GetRequiredService<IHostEnvironment>();
				var hasEmail = !string.IsNullOrWhiteSpace(configuration["ShikshaMitra:HostAdmin:Email"]);
				var hasPassword = !string.IsNullOrWhiteSpace(configuration["ShikshaMitra:HostAdmin:Password"]);
				Log.Information("Bootstrap configuration check: Environment={Environment}, EmailConfigured={EmailConfigured}, PasswordConfigured={PasswordConfigured}. No migrations or seeders were run.",
					environment.EnvironmentName, hasEmail, hasPassword);
				return hasEmail && hasPassword ? 0 : 1;
			}

			await CreateHostBuilder(args).RunConsoleAsync();
			return 0;
		}
		catch (Exception exception)
		{
			Log.Fatal(exception, "Database migrator terminated unexpectedly.");
			return 1;
		}
		finally
		{
			Log.CloseAndFlush();
		}
	}

	public static IHostBuilder CreateHostBuilder(string[] args) =>
		Host.CreateDefaultBuilder(args)
			.UseContentRoot(AppContext.BaseDirectory)
			.AddAppSettingsSecretsJson()
			.ConfigureLogging((context, logging) => logging.ClearProviders())
			.ConfigureServices((hostContext, services) =>
			{
				if (string.IsNullOrWhiteSpace(hostContext.Configuration.GetConnectionString("Default")))
				{
					throw new InvalidOperationException(
						"Configure ConnectionStrings__Default through a secure configuration provider before running DbMigrator.");
				}

				services.AddHostedService<DbMigratorHostedService>();
			});
}
