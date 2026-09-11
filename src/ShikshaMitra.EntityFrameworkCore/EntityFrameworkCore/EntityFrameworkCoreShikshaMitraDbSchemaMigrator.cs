using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Data;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.EntityFrameworkCore;

public class EntityFrameworkCoreShikshaMitraDbSchemaMigrator(IServiceProvider serviceProvider)
	: IShikshaMitraDbSchemaMigrator, ITransientDependency
{
	internal const string LegacyBaselineMigrationId = "20260712225012_v1";
	internal static string GetCurrentBaselineMigrationId(IEnumerable<string> availableMigrations)
	{
		// EF returns migrations in application order; the consolidated baseline is the first one.
		return availableMigrations.FirstOrDefault()
			?? throw new InvalidOperationException("No Entity Framework migrations were found.");
	}

	internal static bool RequiresLegacyBaselineBridge(
		IEnumerable<string> appliedMigrations,
		string currentBaselineMigrationId)
	{
		var applied = appliedMigrations.ToHashSet(StringComparer.Ordinal);
		return applied.Contains(LegacyBaselineMigrationId) &&
		       !applied.Contains(currentBaselineMigrationId);
	}

	internal static bool HasUnsupportedMigrationHistory(
		IEnumerable<string> appliedMigrations,
		string currentBaselineMigrationId)
	{
		var applied = appliedMigrations.ToHashSet(StringComparer.Ordinal);
		return applied.Count > 0 &&
		       !applied.Contains(LegacyBaselineMigrationId) &&
		       !applied.Contains(currentBaselineMigrationId);
	}

	public async Task MigrateAsync()
	{
		/* We intentionally resolve the ShikshaMitraDbContext
		 * from IServiceProvider (instead of directly injecting it)
		 * to properly get the connection string of the current tenant in the
		 * current scope.
		 */

		var dbContext = serviceProvider
			.GetRequiredService<ShikshaMitraDbContext>();

		// Ensure the public schema exists before running migrations.
		// PostgreSQL requires a valid schema in the search_path to create tables
		// (including the __EFMigrationsHistory table).
		await dbContext.Database.ExecuteSqlRawAsync(
			"CREATE SCHEMA IF NOT EXISTS public;");

		var availableMigrations = dbContext.Database.GetMigrations().ToArray();
		var currentBaselineMigrationId = GetCurrentBaselineMigrationId(availableMigrations);
		var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray();
		if (HasUnsupportedMigrationHistory(appliedMigrations, currentBaselineMigrationId))
		{
			throw new InvalidOperationException(
				$"The database has migration history but neither recognized baseline " +
				$"'{LegacyBaselineMigrationId}' nor the current EF baseline '{currentBaselineMigrationId}'. " +
				"Automatic migration was stopped before changing the schema.");
		}

		if (RequiresLegacyBaselineBridge(appliedMigrations, currentBaselineMigrationId))
		{
			var logger = serviceProvider
				.GetRequiredService<ILogger<EntityFrameworkCoreShikshaMitraDbSchemaMigrator>>();
			logger.LogWarning(
				"Reconciling legacy EF baseline {LegacyBaselineMigrationId} to current baseline {CurrentBaselineMigrationId}.",
				LegacyBaselineMigrationId,
				currentBaselineMigrationId);
			await dbContext.Database.ExecuteSqlRawAsync(
				BuildLegacyBaselineBridgeSql(currentBaselineMigrationId));
		}

		await dbContext.Database.MigrateAsync();
	}

	internal static string BuildLegacyBaselineBridgeSql(string currentBaselineMigrationId)
	{
		var escapedCurrentBaselineMigrationId = currentBaselineMigrationId.Replace("'", "''", StringComparison.Ordinal);

		return
		"""
		DO $bridge$
		BEGIN
		    IF to_regclass('"AbpUsers"') IS NULL
		       OR to_regclass('"AbpRoles"') IS NULL
		       OR to_regclass('"AppEmployees"') IS NULL
		       OR to_regclass('"AppEmployeeApplications"') IS NULL
		       OR to_regclass('"AppEmployeeLeavePolicies"') IS NULL
		       OR to_regclass('"AppAcademicCalendarDays"') IS NULL THEN
		        RAISE EXCEPTION 'Legacy migration baseline cannot be reconciled because one or more required baseline tables are missing.';
		    END IF;
		END
		$bridge$;

		ALTER TABLE "AppAcademicCalendarDays"
		ADD COLUMN IF NOT EXISTS "AutoScanImpacts" boolean NOT NULL DEFAULT TRUE;

		DO $bridge$
		BEGIN
		    IF NOT EXISTS (
		        SELECT 1
		        FROM information_schema.columns
		        WHERE table_schema = 'public'
		          AND table_name = 'AppAcademicCalendarDays'
		          AND column_name = 'AutoScanImpacts'
		    ) THEN
		        RAISE EXCEPTION 'Legacy migration baseline reconciliation did not produce the required folded schema.';
		    END IF;

		    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
		    SELECT '__CURRENT_BASELINE_MIGRATION_ID__', "ProductVersion"
		    FROM "__EFMigrationsHistory"
		    WHERE "MigrationId" = '20260712225012_v1'
		    ON CONFLICT ("MigrationId") DO NOTHING;

		    IF NOT EXISTS (
		        SELECT 1
		        FROM "__EFMigrationsHistory"
				WHERE "MigrationId" = '__CURRENT_BASELINE_MIGRATION_ID__'
		    ) THEN
		        RAISE EXCEPTION 'Legacy migration baseline reconciliation could not record the current baseline.';
		    END IF;
		END
		$bridge$;
		""".Replace("__CURRENT_BASELINE_MIGRATION_ID__", escapedCurrentBaselineMigrationId, StringComparison.Ordinal);
	}
}
