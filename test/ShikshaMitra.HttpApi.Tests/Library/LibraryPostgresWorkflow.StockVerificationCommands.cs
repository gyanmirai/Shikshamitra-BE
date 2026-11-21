using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Npgsql;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.InventoryVerification;
using ShikshaMitra.Library.Shared;
using Shouldly;
using StockActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.InventoryVerification;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Stock_Verification_Should_Open_Scan_And_Replay_With_Scoped_Immutable_Commands()
	{
		const string migration = "20261119074137_PersistLibraryStockVerificationCommands";
		const string foundation = "20261119071848_PersistLibraryStockVerification";
		const string route = "/api/library/stock-verification";
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(migration);
		await MigrateStockVerificationAsync(database, foundation);
		await MigrateStockVerificationAsync(database, migration);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		using var writer = OwningClient(factory, string.Join(',', StockActions.View, StockActions.Add, StockActions.Update,
			ShikshaMitraSubmodulePermissions.Library.AllBooks.View));
		using var reader = OwningClient(factory, string.Join(',', StockActions.View, ShikshaMitraSubmodulePermissions.Library.AllBooks.View));
		var (copy, _) = await SeedLoanContextAsync(factory);
		var section = Guid.Parse((await database.ScalarAsync($"SELECT \"SectionId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{copy:D}';"))!);
		var shelf = Guid.Parse((await database.ScalarAsync($"SELECT \"ShelfId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{copy:D}';"))!);
		var accession = (await database.ScalarAsync($"SELECT \"AccessionNumber\" FROM \"AppBookCopies\" WHERE \"Id\"='{copy:D}';"))!;
		var input = new LibraryStockCountInput("Shelf verification", section, shelf, new DateOnly(2026, 11, 19));
		var preview = await PostAsync<LibraryStockCountPreviewDto>(writer, route + "/preview", input);
		preview.ExpectedCopyCount.ShouldBe(1);
		var open = new LibraryOpenStockCountCommand(input, preview.Revision, "same-stock-count-key");
		var opened = await Task.WhenAll(PostAsync<LibraryStockVerificationSessionDto>(writer, route, open),
			PostAsync<LibraryStockVerificationSessionDto>(writer, route, open));
		opened.Select(row => row.Id).Distinct().ShouldHaveSingleItem();
		var session = opened[0];
		session.AcademicYearId.ShouldBe(factory.YearId);
		var detail = $"{route}/{session.Id}";
		var baseline = await reader.GetFromJsonAsync<PaginatedResponse<LibraryStockBaselineDto>>(detail + "/baseline", JsonOptions);
		baseline.ShouldNotBeNull();
		baseline.Items.ShouldHaveSingleItem().CopyId.ShouldBe(copy);
		var scan = new LibraryRecordStockObservationCommand(accession, section, shelf, input.SchoolDate, "same-stock-scan-key");
		var recorded = await PostAsync<LibraryStockObservationDto>(writer, detail + "/observations", scan);
		recorded.CopyId.ShouldBe(copy);
		(await PostAsync<LibraryStockObservationDto>(writer, detail + "/observations", scan)).Id.ShouldBe(recorded.Id);
		using var permissionDenied = await reader.PostAsJsonAsync(detail + "/observations", scan, JsonOptions);
		permissionDenied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var conflictingRetry = await writer.PostAsJsonAsync(detail + "/observations", scan with { Scan = "000-UNKNOWN" }, JsonOptions);
		conflictingRetry.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var observed = await reader.GetFromJsonAsync<PaginatedResponse<LibraryStockObservationDto>>(detail + "/observations", JsonOptions);
		observed.ShouldNotBeNull();
		observed.Items.ShouldHaveSingleItem().Sequence.ShouldBe(1);
		var unknown = await PostAsync<LibraryStockObservationDto>(writer, detail + "/observations",
			scan with { Scan = "000-UNKNOWN", IdempotencyKey = "unknown-stock-scan-key" });
		var review = await reader.GetFromJsonAsync<PaginatedResponse<LibraryStockReconciliationRowDto>>(detail + "/reconciliation", JsonOptions);
		review.ShouldNotBeNull();
		var seen = review.Items.ShouldHaveSingleItem();
		seen.Finding.ShouldBe(LibraryStockFinding.Observed);
		seen.ObservationSequence.ShouldBe(recorded.Sequence);
		seen.RequiresReview.ShouldBeFalse();
		var outside = await reader.GetFromJsonAsync<PaginatedResponse<LibraryStockReconciliationRowDto>>(detail + "/reconciliation?source=2", JsonOptions);
		outside.ShouldNotBeNull();
		outside.TotalCount.ShouldBe(0);
		var unidentified = await reader.GetFromJsonAsync<PaginatedResponse<LibraryStockReconciliationRowDto>>(detail + "/reconciliation?source=3", JsonOptions);
		unidentified.ShouldNotBeNull();
		unidentified.Items.ShouldHaveSingleItem().Id.ShouldBe(unknown.Id);
		using var invalidSource = await reader.GetAsync(detail + "/reconciliation?source=4");
		invalidSource.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockVerificationCommands\";")).ShouldBe("3");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockVerificationSessions\";")).ShouldBe("1");
		var ledger = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync("DELETE FROM \"AppLibraryStockVerificationCommands\";"));
		ledger.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
		var year = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync("UPDATE \"AppLibraryStockVerificationSessions\" SET \"AcademicYearId\"=NULL;"));
		year.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
		var rollback = await Should.ThrowAsync<PostgresException>(() => database.RollbackLibrarySchemaAsync(foundation));
		rollback.SqlState.ShouldBe("P0001");
	}
}
