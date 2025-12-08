using System;
using System.IO;
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
        /* Use the same approach as the DbContextFactory to build configuration and create DbContext.
         * This ensures we get the connection string correctly.
         */
        
        var configuration = BuildConfiguration();
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
    
    private static IConfigurationRoot BuildConfiguration()
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory());
        
        // Try DbMigrator directory first (when running from solution root)
        var dbMigratorPath = Path.Combine(basePath, "src", "ShikshaMitra.DbMigrator");
        if (!Directory.Exists(dbMigratorPath))
        {
            // If not found, we're likely already in DbMigrator directory
            dbMigratorPath = basePath;
        }
        
        var builder = new ConfigurationBuilder()
            .SetBasePath(dbMigratorPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true);

        return builder.Build();
    }
}
