using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Npgsql;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Receiving;
using ShikshaMitra.Library.Shared;
using Shouldly;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Stock_Import_Should_Roll_Back_Replay_And_Retain_Scoped_Provenance()
	{
		const string previous = "AddLibraryStockResultReceiptEvidence";
		const string migration = "PersistLibraryStockImports";
		const string route = "/api/library/receiving/imports";
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(previous);
		await MigrateStockVerificationAsync(database, migration);
		await MigrateStockVerificationAsync(database, previous);
		await MigrateStockVerificationAsync(database, migration);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var view = string.Join(',', ShikshaMitraSubmodulePermissions.Library.Receiving.View,
			ShikshaMitraSubmodulePermissions.Library.AllBooks.View);
		var write = string.Join(',', view, ShikshaMitraSubmodulePermissions.Library.Receiving.Add);
		using var reader = OwningClient(factory, view);
		using var receiver = OwningClient(factory, write);
		using var author = OwningClient(factory, string.Join(',', write, ShikshaMitraSubmodulePermissions.Library.AllBooks.Add));
		var (oldCopyId, _) = await SeedLoanContextAsync(factory);
		var book = Guid.Parse((await database.ScalarAsync($"SELECT \"BookId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{oldCopyId:D}';"))!);
		var uri = $"{route}/options?academicYearId={factory.YearId:D}";
		using var deniedOptions = await reader.GetAsync(uri);
		deniedOptions.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var options = await author.GetFromJsonAsync<LibraryStockImportOptionsDto>(uri, JsonOptions);
		options.ShouldNotBeNull();
		var section = options.Sections.ShouldHaveSingleItem();
		var shelf = section.ShelfDetails.ShouldHaveSingleItem();
		var category = options.Categories.Single(row => row.Name == "PostgreSQL workflow").Id;
		options.CatalogueConfiguration.ShouldNotBeNull();
		var input = new LibraryStockImportInput(
			"SourceRowId,BookId,Accession,TitleKey,Title,Author,LanguageCode,CataloguePrice,ReceivedOn,UnitCost,Currency\n" +
			$"PG-0001,{book:D},,,,,,,2026-11-16,,\n" +
			"PG-0002,,ACC-000007,sample,Opening stock folktales,School Library,hi,0,,12.25,INR\n",
			factory.YearId, section.Id, shelf.Id, category, new(2026, 11, 18), LibraryStockSource.Donation, "School book drive");
		using var deniedPreview = await receiver.PostAsJsonAsync(route + "/preview", input, JsonOptions);
		deniedPreview.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var preview = await PostAsync<LibraryStockImportPreviewDto>(author, route + "/preview", input);
		preview.Errors.ShouldBeEmpty();
		preview.NewTitleCount.ShouldBe(1);
		preview.Rows[0].ReceivedOn.ShouldBe(new DateOnly(2026, 11, 16));
		var command = new LibraryCommitStockImportCommand(input, preview.Revision!, "postgres-import");

		await database.ExecuteAsync("""
			CREATE FUNCTION sm_test_reject_stock_import() RETURNS trigger LANGUAGE plpgsql AS $$
			BEGIN RAISE EXCEPTION 'Intentional stock import rollback test'; END $$;
			CREATE TRIGGER sm_test_reject_stock_import BEFORE INSERT ON "AppLibraryStockImportRows"
				FOR EACH ROW EXECUTE FUNCTION sm_test_reject_stock_import();
			""");
		using var failed = await author.PostAsJsonAsync(route, command, JsonOptions);
		failed.IsSuccessStatusCode.ShouldBeFalse();
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockImportBatches\";")).ShouldBe("0");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockReceipts\";")).ShouldBe("0");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryReceivedCopies\";")).ShouldBe("0");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppBooks\" WHERE \"Title\"='Opening stock folktales';")).ShouldBe("0");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppBookCopies\" WHERE \"AccessionNumber\" LIKE 'ACC-%';")).ShouldBe("0");
		await database.ExecuteAsync("""
			DROP TRIGGER sm_test_reject_stock_import ON "AppLibraryStockImportRows";
			DROP FUNCTION sm_test_reject_stock_import();
			""");
		var pair = await Task.WhenAll(PostAsync<LibraryStockImportBatchDto>(author, route, command),
			PostAsync<LibraryStockImportBatchDto>(author, route, command));
		var accepted = pair[0];
		pair[1].Id.ShouldBe(accepted.Id);
		pair[1].Batch.ShouldBe(accepted.Batch);
		pair[1].Rows.ShouldBe(accepted.Rows);
		accepted.Rows.Select(row => row.SourceRowId).ShouldBe(["PG-0001", "PG-0002"]);
		accepted.Rows.Select(row => row.AccessionNumber).ShouldBe(["ACC-000008", "ACC-000007"]);
		accepted.Rows.Select(row => row.ReceiptId).Distinct().Count().ShouldBe(2);
		var register = await reader.GetFromJsonAsync<PaginatedResponse<LibraryStockImportSummaryDto>>(route, JsonOptions);
		register.ShouldNotBeNull();
		register.Items.ShouldHaveSingleItem().Id.ShouldBe(accepted.Id);
		var detail = await reader.GetFromJsonAsync<LibraryStockImportBatchDto>($"{route}/{accepted.Id:D}", JsonOptions);
		detail.ShouldNotBeNull();
		detail.Rows.ShouldBe(accepted.Rows);
		using var conflict = await author.PostAsJsonAsync(route, command with { Import = input with { SourceName = "Changed" } }, JsonOptions);
		conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		using var duplicate = await author.PostAsJsonAsync(route, command with { IdempotencyKey = "other-attempt" }, JsonOptions);
		duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockImportRows\";")).ShouldBe("2");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockReceiptCommands\";")).ShouldBe("2");
		var immutable = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(
			"UPDATE \"AppLibraryStockImportRows\" SET \"Title\"='Changed';"));
		immutable.SqlState.ShouldBe("23514");
		var truncate = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync("TRUNCATE \"AppLibraryStockImportRows\";"));
		truncate.SqlState.ShouldBe("23514");
		var down = await Should.ThrowAsync<PostgresException>(() => database.RollbackLibrarySchemaAsync(previous));
		down.SqlState.ShouldBe("23514");
	}
}
