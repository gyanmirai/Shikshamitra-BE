using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore;

public class LegacyMigrationBaselineBridge_Tests
{
	[Fact]
	public void Legacy_Baseline_Without_Current_Baseline_Requires_Bridge()
	{
		const string currentMigrationId = "20260810165956_Initial";

		EntityFrameworkCoreShikshaMitraDbSchemaMigrator
			.RequiresLegacyBaselineBridge(
			[
				"LegacyMigrationBeforeBaseline",
				EntityFrameworkCoreShikshaMitraDbSchemaMigrator.LegacyBaselineMigrationId
			], currentMigrationId)
			.ShouldBeTrue();
	}

	[Fact]
	public void Fresh_Or_Current_Baseline_Does_Not_Require_Bridge()
	{
		const string currentMigrationId = "20260810165956_Initial";

		EntityFrameworkCoreShikshaMitraDbSchemaMigrator
			.RequiresLegacyBaselineBridge([], currentMigrationId)
			.ShouldBeFalse();
		EntityFrameworkCoreShikshaMitraDbSchemaMigrator
			.RequiresLegacyBaselineBridge(
			[
				EntityFrameworkCoreShikshaMitraDbSchemaMigrator.LegacyBaselineMigrationId,
				currentMigrationId
			], currentMigrationId)
			.ShouldBeFalse();
	}

	[Fact]
	public void Unknown_Nonempty_History_Is_Rejected()
	{
		const string currentMigrationId = "20260810165956_Initial";

		EntityFrameworkCoreShikshaMitraDbSchemaMigrator
			.HasUnsupportedMigrationHistory(["20260101000000_UnknownBaseline"], currentMigrationId)
			.ShouldBeTrue();
		EntityFrameworkCoreShikshaMitraDbSchemaMigrator
			.HasUnsupportedMigrationHistory([], currentMigrationId)
			.ShouldBeFalse();
	}

	[Fact]
	public void Bridge_Is_Idempotent_And_Validates_Folded_Schema_Before_Aliasing()
	{
		const string currentMigrationId = "20260810165956_Initial";
		var sql = EntityFrameworkCoreShikshaMitraDbSchemaMigrator.BuildLegacyBaselineBridgeSql(currentMigrationId);

		sql.ShouldContain("ADD COLUMN IF NOT EXISTS");
		sql.ShouldContain("ON CONFLICT (\"MigrationId\") DO NOTHING");
		sql.ShouldContain("DO $bridge$");
		sql.ShouldContain("\"AppEmployeeApplications\"");
		sql.ShouldContain(EntityFrameworkCoreShikshaMitraDbSchemaMigrator.LegacyBaselineMigrationId);
		sql.ShouldContain(currentMigrationId);
	}

	[Fact]
	public void Current_Baseline_Is_Discovered_From_The_Migration_Assembly()
	{
		EntityFrameworkCoreShikshaMitraDbSchemaMigrator
			.GetCurrentBaselineMigrationId(["ConsolidatedBaseline", "FutureMigration"])
			.ShouldBe("ConsolidatedBaseline");
	}
}
