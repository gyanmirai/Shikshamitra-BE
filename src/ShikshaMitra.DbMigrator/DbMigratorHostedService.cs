using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShikshaMitra.Data;
using Serilog;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.DbMigrator;

public class DbMigratorHostedService(IHostApplicationLifetime hostApplicationLifetime, IConfiguration configuration)
	: IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
    {
	    using var application = await AbpApplicationFactory.CreateAsync<ShikshaMitraDbMigratorModule>(options =>
	    {
		    options.Services.ReplaceConfiguration(configuration);
		    options.UseAutofac();
		    options.Services.AddLogging(c => c.AddSerilog());
		    options.AddDataMigrationEnvironment();
	    });
	    await application.InitializeAsync();

	    await application
		    .ServiceProvider
		    .GetRequiredService<ShikshaMitraDbMigrationService>()
		    .MigrateAsync();

	    await application.ShutdownAsync();

	    hostApplicationLifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
