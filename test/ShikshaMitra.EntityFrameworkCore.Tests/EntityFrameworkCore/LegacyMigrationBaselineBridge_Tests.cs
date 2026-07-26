using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore;

public class LegacyMigrationBaselineBridge_Tests
{
	[Fact]
	public void Legacy_Baseline_Without_Current_Baseline_Requires_Bridge()
	{
		EntityFrameworkCoreShikshaMitraDbSchemaMigrator
			.RequiresLegacyBaselineBridge(
			[
				"20260710051950_AddSmartLearningTaskTypeSettings",
				EntityFrameworkCoreShikshaMitraDbSchemaMigrator.LegacyBaselineMigrationId
			])
			.ShouldBeTrue();
	}

	[Fact]
	public void Fresh_Or_Current_Baseline_Does_Not_Require_Bridge()
	{
		EntityFrameworkCoreShikshaMitraDbSchemaMigrator
			.RequiresLegacyBaselineBridge([])
			.ShouldBeFalse();
		EntityFrameworkCoreShikshaMitraDbSchemaMigrator
			.RequiresLegacyBaselineBridge(
			[
				EntityFrameworkCoreShikshaMitraDbSchemaMigrator.LegacyBaselineMigrationId,
				EntityFrameworkCoreShikshaMitraDbSchemaMigrator.CurrentBaselineMigrationId
			])
			.ShouldBeFalse();
	}

	[Fact]
	public void Unknown_Nonempty_History_Is_Rejected()
	{
		EntityFrameworkCoreShikshaMitraDbSchemaMigrator
			.HasUnsupportedMigrationHistory(["20260101000000_UnknownBaseline"])
			.ShouldBeTrue();
		EntityFrameworkCoreShikshaMitraDbSchemaMigrator
			.HasUnsupportedMigrationHistory([])
			.ShouldBeFalse();
	}

	[Fact]
	public void Bridge_Is_Idempotent_And_Validates_Folded_Schema_Before_Aliasing()
	{
		var sql = EntityFrameworkCoreShikshaMitraDbSchemaMigrator.LegacyBaselineBridgeSql;

		sql.ShouldContain("ADD COLUMN IF NOT EXISTS");
		sql.ShouldContain("CREATE UNIQUE INDEX IF NOT EXISTS");
		sql.ShouldContain("ON CONFLICT (\"MigrationId\") DO NOTHING");
		sql.ShouldContain("\"AppEmployeeApplications\"");
		sql.ShouldContain("\"AppAdmissionCrmPipelineStages\"");
		sql.ShouldContain(EntityFrameworkCoreShikshaMitraDbSchemaMigrator.LegacyBaselineMigrationId);
		sql.ShouldContain(EntityFrameworkCoreShikshaMitraDbSchemaMigrator.CurrentBaselineMigrationId);
	}
}
