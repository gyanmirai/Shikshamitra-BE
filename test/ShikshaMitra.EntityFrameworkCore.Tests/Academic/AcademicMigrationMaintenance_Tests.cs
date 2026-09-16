using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Academic;

public class AcademicMigrationMaintenance_Tests
{
	private static readonly string[] Available =
	[
		"20260801000000_baseline",
		EntityFrameworkCoreShikshaMitraDbSchemaMigrator.AcademicPrefixMigrationId,
		EntityFrameworkCoreShikshaMitraDbSchemaMigrator.AcademicNameNormalizationMigrationId
	];

	[Theory]
	[InlineData("20260801000000_baseline")]
	[InlineData(EntityFrameworkCoreShikshaMitraDbSchemaMigrator.LegacyBaselineMigrationId)]
	public void Existing_Online_Database_Cannot_Apply_The_Breaking_Rename(string baseline)
	{
		var error = Should.Throw<InvalidOperationException>(() =>
			EntityFrameworkCoreShikshaMitraDbSchemaMigrator.EnsureAcademicRenameMaintenance(Available, [baseline], false));
		error.Message.ShouldContain("stop all old API and worker instances");
	}

	[Fact]
	public void Fresh_Install_And_Already_Upgraded_Databases_Do_Not_Require_Maintenance()
	{
		EntityFrameworkCoreShikshaMitraDbSchemaMigrator.EnsureAcademicRenameMaintenance(Available, [], false);
		EntityFrameworkCoreShikshaMitraDbSchemaMigrator.EnsureAcademicRenameMaintenance(Available, Available, false);
		EntityFrameworkCoreShikshaMitraDbSchemaMigrator.EnsureAcademicRenameMaintenance(Available, [Available[0]], true);
		EntityFrameworkCoreShikshaMitraDbSchemaMigrator.EnsureAcademicRenameMaintenance([Available[0]], [Available[0]], false);
	}

	[Fact]
	public void Database_With_The_First_Prefix_Migration_Still_Requires_Maintenance_For_Normalization()
	{
		var error = Should.Throw<InvalidOperationException>(() =>
			EntityFrameworkCoreShikshaMitraDbSchemaMigrator.EnsureAcademicRenameMaintenance(
				Available, [Available[0], EntityFrameworkCoreShikshaMitraDbSchemaMigrator.AcademicPrefixMigrationId], false));
		error.Message.ShouldContain("coordinated maintenance deployment");
	}
}
