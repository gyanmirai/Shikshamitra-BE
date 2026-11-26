using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using ShikshaMitra.Controllers.Communication.Notifications;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ShikshaMitra;
public class Program
{
	// WebApplicationFactory<Program> needs a non-static entry-point marker.
	public Program() { }

	public async static Task<int> Main(string[] args)
	{
		var logger = new LoggerConfiguration()
#if DEBUG
			.MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
			.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
			.MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
			.MinimumLevel.Override("OpenIddict", LogEventLevel.Information)
			.MinimumLevel.Override("Volo.Abp.Authorization", LogEventLevel.Information)
			.Filter.ByExcluding(logEvent => logEvent.Properties.ContainsKey("QueryString") &&
				logEvent.Properties.TryGetValue("Path", out var path) && path is ScalarValue { Value: string requestPath } &&
				requestPath.TrimEnd('/').Equals(WhatsAppNotificationWebhookController.RoutePath, StringComparison.OrdinalIgnoreCase))
			.Enrich.FromLogContext()
			.WriteTo.Async(c => c.Console());

		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")))
		{
			logger.WriteTo.Async(c => c.File("Logs/logs.txt"));
		}
		else if (Environment.GetEnvironmentVariable("HOME") is { Length: > 0 } appServiceHome &&
			Path.IsPathFullyQualified(appServiceHome))
		{
			// App Service replaces the deployment directory while the previous worker shuts down.
			var logPath = Path.Combine(appServiceHome, "LogFiles", "Application", "shikshamitra-.log");
			logger.WriteTo.Async(c => c.File(logPath, rollingInterval: RollingInterval.Day,
				fileSizeLimitBytes: 10 * 1024 * 1024, rollOnFileSizeLimit: true, retainedFileCountLimit: 10, shared: true));
		}

		Log.Logger = logger.CreateLogger();

		try
		{
			Log.Information("Starting ShikshaMitra.HttpApi.Host.");
			var builder = WebApplication.CreateBuilder(args);
			builder.Host.AddAppSettingsSecretsJson()
				.UseAutofac()
				.UseSerilog();
			await builder.AddApplicationAsync<ShikshaMitraHttpApiHostModule>();
			var app = builder.Build();
			await app.InitializeApplicationAsync();
			await app.RunAsync();
			return 0;
		}
		catch (Exception ex)
		{
			if (ex is HostAbortedException)
			{
				throw;
			}

			Log.Fatal(ex, "Host terminated unexpectedly!");
			return 1;
		}
		finally
		{
			Log.CloseAndFlush();
		}
	}
}
