using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shouldly;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Catalogue_Price_Postgres_Migration_Should_Preserve_Existing_Prices_And_Allow_A_Safe_Roundtrip()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.CatalogueConfiguration);
		await database.ExecuteAsync(CatalogueLegacyRows);
		await database.ExecuteAsync($"UPDATE \"AppBooks\" SET \"Price\"=0 WHERE \"Id\"='{CatalogueOldBook}';");
		await database.ExecuteAsync("UPDATE \"AppBooks\" SET \"Price\"=-12.50 WHERE \"Id\"='ca100000-0000-0000-0000-000000000001';");
		await database.ExecuteAsync("UPDATE \"AppBooks\" SET \"Price\"=9999999999999999.99 WHERE \"Id\"='ca100000-0000-0000-0000-000000000004';");
		var original = await database.ScalarAsync(CataloguePreservedState);
		await using (var context = database.CreateContext())
			await context.GetService<IMigrator>().MigrateAsync(LibraryMigrationPostgresFixture.CataloguePrice);
		(await database.ScalarAsync(CataloguePreservedState)).ShouldBe(original);
		(await database.ScalarAsync($"SELECT \"Price\"::text FROM \"AppBooks\" WHERE \"Id\"='{CatalogueOldBook}';")).ShouldBe("0.00");
		(await database.ScalarAsync("SELECT \"Price\"::text FROM \"AppBooks\" WHERE \"Id\"='ca100000-0000-0000-0000-000000000004';")).ShouldBe("9999999999999999.99");
		await using (var context = database.CreateContext())
			await context.GetService<IMigrator>().MigrateAsync(LibraryMigrationPostgresFixture.CatalogueConfiguration);
		(await database.ScalarAsync(CataloguePreservedState)).ShouldBe(original);
		await using (var context = database.CreateContext())
			await context.GetService<IMigrator>().MigrateAsync(LibraryMigrationPostgresFixture.CataloguePrice);
		(await database.ScalarAsync(CataloguePreservedState)).ShouldBe(original);
	}

	[LibraryPostgresFact]
	public async Task Catalogue_Price_Postgres_Rollback_Should_Refuse_To_Replace_An_Unknown_Price_With_Zero()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.CatalogueConfiguration);
		await database.ExecuteAsync(CatalogueLegacyRows);
		await using (var context = database.CreateContext()) await context.GetService<IMigrator>().MigrateAsync(LibraryMigrationPostgresFixture.CataloguePrice);
		await database.ExecuteAsync($"UPDATE \"AppBooks\" SET \"Price\"=NULL WHERE \"Id\"='{CatalogueOldBook}';");
		var original = await database.ScalarAsync(CataloguePreservedState);
		var history = await database.ScalarAsync("SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\";");
		await using var rollback = database.CreateContext();
		var refused = await Should.ThrowAsync<PostgresException>(() => rollback.GetService<IMigrator>().MigrateAsync(LibraryMigrationPostgresFixture.CatalogueConfiguration));
		refused.SqlState.ShouldBe("P0001");
		refused.MessageText.ShouldContain("Catalogue price rollback refused");
		(await database.ScalarAsync(CataloguePreservedState)).ShouldBe(original);
		(await database.ScalarAsync("SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\";")).ShouldBe(history);
	}
}
