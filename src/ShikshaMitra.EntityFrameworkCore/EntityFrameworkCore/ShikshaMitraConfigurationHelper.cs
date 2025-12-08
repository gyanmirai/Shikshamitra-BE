using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace ShikshaMitra.EntityFrameworkCore;

/// <summary>
/// Helper class for building configuration from appsettings.json files.
/// Shared across DbContextFactory and DbSchemaMigrator to ensure consistent configuration loading.
/// </summary>
public static class ShikshaMitraConfigurationHelper
{
	/// <summary>
	/// Builds configuration by loading appsettings.json from the DbMigrator project directory.
	/// Attempts to locate the DbMigrator directory relative to the current working directory.
	/// </summary>
	/// <returns>Built configuration root with loaded settings</returns>
	public static IConfigurationRoot BuildConfiguration()
	{
		var basePath = Path.Combine(Directory.GetCurrentDirectory());
		
		// Try DbMigrator directory first (when running from solution root)
		var dbMigratorPath = Path.Combine(basePath, "src", "ShikshaMitra.DbMigrator");
		if (!Directory.Exists(dbMigratorPath))
		{
			// If not found, try relative path (for EF Core design-time tools)
			dbMigratorPath = Path.Combine(basePath, "../ShikshaMitra.DbMigrator/");
			if (!Directory.Exists(dbMigratorPath))
			{
				// If still not found, we're likely already in DbMigrator directory
				dbMigratorPath = basePath;
			}
		}
		
		var builder = new ConfigurationBuilder()
			.SetBasePath(dbMigratorPath)
			.AddJsonFile("appsettings.json", optional: false)
			.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true);

		return builder.Build();
	}
}
