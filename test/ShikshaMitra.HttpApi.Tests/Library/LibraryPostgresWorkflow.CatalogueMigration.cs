using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Catalogue;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Catalogue_Postgres_Migration_Should_Preserve_All_Five_Legacy_Meanings_And_Sealed_Stock_When_No_Configuration_Exists()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.CopyEvidence);
		await database.ExecuteAsync(CatalogueLegacyRows);
		await using (var context = database.CreateContext())
			await context.GetService<IMigrator>().MigrateAsync(LibraryMigrationPostgresFixture.StockReceiving);
		await database.ExecuteAsync(ReceivingSql(Guid.Parse(CatalogueOldCopy), cost: "9999999999999998.99"));
		var original = await database.ScalarAsync(CataloguePreservedState);
		await using (var context = database.CreateContext())
			await context.GetService<IMigrator>().MigrateAsync(LibraryMigrationPostgresFixture.CatalogueConfiguration);
		(await database.ScalarAsync(CataloguePreservedState)).ShouldBe(original);
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryCatalogueConfigurations\";")).ShouldBe("0");
		(await database.ScalarAsync("SELECT string_agg(\"Language\"::text,',' ORDER BY \"Language\") FROM \"AppBooks\";")).ShouldBe("0,1,2,3,4");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppBooks\" WHERE \"LanguageCode\" IS NOT NULL OR \"MaterialCode\" IS NOT NULL;")).ShouldBe("0");
		await using (var context = database.CreateContext())
			await context.GetService<IMigrator>().MigrateAsync(LibraryMigrationPostgresFixture.StockReceiving);
		(await database.ScalarAsync(CataloguePreservedState)).ShouldBe(original);
		await using (var context = database.CreateContext())
			await context.GetService<IMigrator>().MigrateAsync(LibraryMigrationPostgresFixture.CatalogueConfiguration);
		(await database.ScalarAsync(CataloguePreservedState)).ShouldBe(original);
	}

	[LibraryPostgresTheory]
	[InlineData("Configuration")]
	[InlineData("UnknownLanguage")]
	[InlineData("LanguageCode")]
	[InlineData("MaterialCode")]
	public async Task Catalogue_Postgres_Rollback_Should_Refuse_To_Discard_Recorded_Metadata(string fact)
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.CatalogueConfiguration);
		await database.ExecuteAsync(CatalogueLegacyRows);
		var sql = fact switch
		{
			"Configuration" => $$"""
				INSERT INTO "AppLibraryCatalogueConfigurations"
				("Id","TenantId","DefinitionJson","Revision","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
				VALUES ('ca800000-0000-0000-0000-000000000001','{{TestAuthHandler.TestTenantId}}',
				'{{JsonSerializer.Serialize(LibraryCatalogueRules.Defaults()).Replace("'", "''", StringComparison.Ordinal)}}',
				'11111111111111111111111111111111','{}','catalogue-rollback',NOW(),false);
				""",
			"UnknownLanguage" => $"UPDATE \"AppBooks\" SET \"Language\"=NULL WHERE \"Id\"='{CatalogueOldBook}';",
			"LanguageCode" => $"UPDATE \"AppBooks\" SET \"LanguageCode\"='gu' WHERE \"Id\"='{CatalogueOldBook}';",
			"MaterialCode" => $"UPDATE \"AppBooks\" SET \"MaterialCode\"='picture-book' WHERE \"Id\"='{CatalogueOldBook}';",
			_ => throw new ArgumentOutOfRangeException(nameof(fact))
		};
		await database.ExecuteAsync(sql);
		var original = await database.ScalarAsync(CataloguePreservedState);
		await using var context = database.CreateContext();
		var refused = await Should.ThrowAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(LibraryMigrationPostgresFixture.StockReceiving));
		refused.SqlState.ShouldBe("P0001");
		refused.MessageText.ShouldContain("Catalogue rollback refused");
		(await database.ScalarAsync("SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\";")).ShouldBe(LibraryMigrationPostgresFixture.CatalogueConfiguration);
		(await database.ScalarAsync(CataloguePreservedState)).ShouldBe(original);
		if (fact == "Configuration") (await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryCatalogueConfigurations\";")).ShouldBe("1");
		if (fact == "LanguageCode") (await database.ScalarAsync($"SELECT \"LanguageCode\" FROM \"AppBooks\" WHERE \"Id\"='{CatalogueOldBook}';")).ShouldBe("gu");
		if (fact == "MaterialCode") (await database.ScalarAsync($"SELECT \"MaterialCode\" FROM \"AppBooks\" WHERE \"Id\"='{CatalogueOldBook}';")).ShouldBe("picture-book");
	}
}
