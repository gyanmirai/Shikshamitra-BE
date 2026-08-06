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
	internal const string CurrentBaselineMigrationId = "20260804061356_v1";

	internal static bool RequiresLegacyBaselineBridge(IEnumerable<string> appliedMigrations)
	{
		var applied = appliedMigrations.ToHashSet(StringComparer.Ordinal);
		return applied.Contains(LegacyBaselineMigrationId) &&
		       !applied.Contains(CurrentBaselineMigrationId);
	}

	internal static bool HasUnsupportedMigrationHistory(IEnumerable<string> appliedMigrations)
	{
		var applied = appliedMigrations.ToHashSet(StringComparer.Ordinal);
		return applied.Count > 0 &&
		       !applied.Contains(LegacyBaselineMigrationId) &&
		       !applied.Contains(CurrentBaselineMigrationId);
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

		var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray();
		if (HasUnsupportedMigrationHistory(appliedMigrations))
		{
			throw new InvalidOperationException(
				$"The database has migration history but neither recognized baseline " +
				$"'{LegacyBaselineMigrationId}' nor '{CurrentBaselineMigrationId}'. " +
				"Automatic migration was stopped before changing the schema.");
		}

		if (RequiresLegacyBaselineBridge(appliedMigrations))
		{
			var logger = serviceProvider
				.GetRequiredService<ILogger<EntityFrameworkCoreShikshaMitraDbSchemaMigrator>>();
			logger.LogWarning(
				"Reconciling legacy EF baseline {LegacyBaselineMigrationId} to current baseline {CurrentBaselineMigrationId}.",
				LegacyBaselineMigrationId,
				CurrentBaselineMigrationId);
			await dbContext.Database.ExecuteSqlRawAsync(LegacyBaselineBridgeSql);
		}

		await dbContext.Database.MigrateAsync();
	}

	internal const string LegacyBaselineBridgeSql =
		"""
		DO $bridge$
		BEGIN
		    IF to_regclass('"AbpUsers"') IS NULL
		       OR to_regclass('"AbpRoles"') IS NULL
		       OR to_regclass('"AppEmployees"') IS NULL
		       OR to_regclass('"AppEmployeeApplications"') IS NULL
		       OR to_regclass('"AppEmployeeLeavePolicies"') IS NULL
		       OR to_regclass('"AppAcademicCalendarDays"') IS NULL
		       OR to_regclass('"AppAdmissionCrmCycleConfigurations"') IS NULL
		       OR to_regclass('"AppAdmissionCrmPipelineStages"') IS NULL THEN
		        RAISE EXCEPTION 'Legacy migration baseline cannot be reconciled because one or more required baseline tables are missing.';
		    END IF;
		END
		$bridge$;

		ALTER TABLE "AppAcademicCalendarDays"
		ADD COLUMN IF NOT EXISTS "AutoScanImpacts" boolean NOT NULL DEFAULT TRUE;

		ALTER TABLE "AppAdmissionCrmCycleConfigurations"
		ADD COLUMN IF NOT EXISTS "EntranceExamRequired" boolean NOT NULL DEFAULT TRUE,
		ADD COLUMN IF NOT EXISTS "JourneyLocked" boolean NOT NULL DEFAULT FALSE,
		ADD COLUMN IF NOT EXISTS "JourneyLockedAt" timestamp without time zone NULL,
		ADD COLUMN IF NOT EXISTS "JourneyLockedByUserId" uuid NULL,
		ADD COLUMN IF NOT EXISTS "JourneyLockReason" character varying(1000) NULL;

		ALTER TABLE "AppAdmissionCrmPipelineStages"
		ADD COLUMN IF NOT EXISTS "BaseSystemStage" integer NOT NULL DEFAULT 0,
		ADD COLUMN IF NOT EXISTS "StageKey" character varying(64) NOT NULL DEFAULT '';

		UPDATE "AppAdmissionCrmPipelineStages"
		SET "StageKey" = CASE "SystemStage"
		        WHEN 0 THEN 'NewInquiry'
		        WHEN 1 THEN 'FollowUp'
		        WHEN 2 THEN 'ExamReady'
		        WHEN 3 THEN 'ExamScheduled'
		        WHEN 4 THEN 'ExamCompleted'
		        WHEN 5 THEN 'Eligible'
		        WHEN 6 THEN 'Waitlisted'
		        WHEN 7 THEN 'Rejected'
		        WHEN 8 THEN 'Stale'
		        WHEN 9 THEN 'Archived'
		        ELSE "SystemStage"::text
		    END,
		    "BaseSystemStage" = "SystemStage"
		WHERE "StageKey" = '';

		DROP INDEX IF EXISTS "IX_AppAdmissionCrmPipelineStages_TenantId_AcademicYearId_Syste~";

		CREATE UNIQUE INDEX IF NOT EXISTS "IX_AppAdmissionCrmPipelineStages_TenantId_AcademicYearId_StageKey"
		ON "AppAdmissionCrmPipelineStages" ("TenantId", "AcademicYearId", "StageKey")
		WHERE "IsDeleted" = FALSE;

		CREATE INDEX IF NOT EXISTS "IX_AppAdmissionCrmPipelineStages_TenantId_AcademicYearId_SystemStage"
		ON "AppAdmissionCrmPipelineStages" ("TenantId", "AcademicYearId", "SystemStage");

		DO $bridge$
		BEGIN
		    IF NOT EXISTS (
		        SELECT 1
		        FROM information_schema.columns
		        WHERE table_schema = 'public'
		          AND table_name = 'AppAcademicCalendarDays'
		          AND column_name = 'AutoScanImpacts'
		    ) OR NOT EXISTS (
		        SELECT 1
		        FROM information_schema.columns
		        WHERE table_schema = 'public'
		          AND table_name = 'AppAdmissionCrmPipelineStages'
		          AND column_name = 'StageKey'
		    ) THEN
		        RAISE EXCEPTION 'Legacy migration baseline reconciliation did not produce the required folded schema.';
		    END IF;

		    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
		    SELECT '20260804061356_v1', "ProductVersion"
		    FROM "__EFMigrationsHistory"
		    WHERE "MigrationId" = '20260712225012_v1'
		    ON CONFLICT ("MigrationId") DO NOTHING;

		    IF NOT EXISTS (
		        SELECT 1
		        FROM "__EFMigrationsHistory"
		        WHERE "MigrationId" = '20260804061356_v1'
		    ) THEN
		        RAISE EXCEPTION 'Legacy migration baseline reconciliation could not record the current baseline.';
		    END IF;
		END
		$bridge$;
		""";
}
