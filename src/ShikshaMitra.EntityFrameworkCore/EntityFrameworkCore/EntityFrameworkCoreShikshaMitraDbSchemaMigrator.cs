using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Data;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.EntityFrameworkCore;

public class EntityFrameworkCoreShikshaMitraDbSchemaMigrator(
	ILogger<EntityFrameworkCoreShikshaMitraDbSchemaMigrator> logger)
	: IShikshaMitraDbSchemaMigrator, ITransientDependency
{
	public async Task MigrateAsync()
    {
        /* Use the shared configuration helper to build configuration and create DbContext.
         * This ensures we get the connection string correctly.
         */
        
        var configuration = ShikshaMitraConfigurationHelper.BuildConfiguration();
        var connectionString = configuration.GetConnectionString("Default");
        
        logger.LogInformation($"Connection string retrieved: {(!string.IsNullOrEmpty(connectionString) ? "Yes" : "No (EMPTY)")}");
        
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'Default' is null or empty. " +
                "Please ensure appsettings.json exists in the DbMigrator project and contains a valid connection string.");
        }
        
        var builder = new DbContextOptionsBuilder<ShikshaMitraDbContext>()
            .UseNpgsql(connectionString);

        await using var dbContext = new ShikshaMitraDbContext(builder.Options);
        await dbContext.Database.MigrateAsync();
    }
}
