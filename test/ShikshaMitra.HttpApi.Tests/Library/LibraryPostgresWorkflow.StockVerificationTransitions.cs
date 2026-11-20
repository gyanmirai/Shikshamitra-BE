using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Npgsql;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.InventoryVerification;
using Shouldly;
using StockActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.InventoryVerification;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Stock_Verification_Should_Seal_The_Review_And_Cancellation_Commands()
	{
		const string previous = "20261119074137_PersistLibraryStockVerificationCommands";
		const string migration = "PersistLibraryStockVerificationTransitions";
		const string route = "/api/library/stock-verification";
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(previous);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		using var writer = OwningClient(factory, string.Join(',', StockActions.View, StockActions.Add, StockActions.Update,
			ShikshaMitraSubmodulePermissions.Library.AllBooks.View));
		using var reader = OwningClient(factory, string.Join(',', StockActions.View, ShikshaMitraSubmodulePermissions.Library.AllBooks.View));
		var (copy, _) = await SeedLoanContextAsync(factory);
		var section = Guid.Parse((await database.ScalarAsync($"SELECT \"SectionId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{copy:D}';"))!);
		var input = new LibraryStockCountInput("Review the reading room", section, null, new DateOnly(2026, 11, 19));
		var preview = await PostAsync<LibraryStockCountPreviewDto>(writer, route + "/preview", input);
		var opened = await PostAsync<LibraryStockVerificationSessionDto>(writer, route, new LibraryOpenStockCountCommand(input, preview.Revision, "existing-open"));
		var detail = $"{route}/{opened.Id}";
		var scan = new LibraryRecordStockObservationCommand("UNIDENTIFIED", section, null, input.SchoolDate, "existing-scan");
		var recorded = await PostAsync<LibraryStockObservationDto>(writer, detail + "/observations", scan);
		await MigrateStockVerificationAsync(database, migration);
		await MigrateStockVerificationAsync(database, previous);
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockVerificationCommands\";")).ShouldBe("2");
		await MigrateStockVerificationAsync(database, migration);
		await database.MigrateToCurrentAsync();

		var review = new LibraryBeginStockReviewCommand(1, "review-stock-count");
		using var denied = await reader.PostAsJsonAsync(detail + "/review", review, JsonOptions);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var stale = await writer.PostAsJsonAsync(detail + "/review", review with { ExpectedObservationCount = 0 }, JsonOptions);
		stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var reviewed = await Task.WhenAll(PostAsync<LibraryStockTransitionDto>(writer, detail + "/review", review),
			PostAsync<LibraryStockTransitionDto>(writer, detail + "/review", review));
		reviewed[0].Status.ShouldBe(LibraryStockVerificationStatus.InReview);
		reviewed[1].ShouldBe(reviewed[0]);
		using var late = await writer.PostAsJsonAsync(detail + "/observations", scan with { IdempotencyKey = "late-scan" }, JsonOptions);
		late.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await PostAsync<LibraryStockObservationDto>(writer, detail + "/observations", scan)).Id.ShouldBe(recorded.Id);

		var cancel = new LibraryCancelStockCountCommand(1, LibraryStockVerificationStatus.InReview, "Room closed for maintenance", "cancel-stock-count");
		using var deniedCancel = await reader.PostAsJsonAsync(detail + "/cancel", cancel, JsonOptions);
		deniedCancel.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var cancelled = await PostAsync<LibraryStockTransitionDto>(writer, detail + "/cancel", cancel);
		cancelled.Status.ShouldBe(LibraryStockVerificationStatus.Cancelled);
		(await PostAsync<LibraryStockTransitionDto>(writer, detail + "/cancel", cancel)).ShouldBe(cancelled);
		(await PostAsync<LibraryStockTransitionDto>(writer, detail + "/review", review)).ShouldBe(reviewed[0]);
		var retained = await reader.GetFromJsonAsync<LibraryStockVerificationSessionDto>(detail, JsonOptions);
		retained.ShouldNotBeNull();
		retained.CancellationReason.ShouldBe(cancel.Reason);
		retained.ObservationCount.ShouldBe(1);
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockVerificationCommands\";")).ShouldBe("4");

		var second = await PostAsync<LibraryStockVerificationSessionDto>(writer, route, new LibraryOpenStockCountCommand(input, preview.Revision, "second-open"));
		var forged = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync($$"""
			INSERT INTO "AppLibraryStockVerificationCommands"
			  ("Id", "TenantId", "SessionId", "ActorId", "Kind", "IdempotencyKey", "PayloadFingerprint", "RecordedAt")
			SELECT 'bd100000-0000-0000-0000-000000000007', "TenantId", "Id", "OpenedBy", 3, 'forged-review', repeat('A', 64), "OpenedAt"
			FROM "AppLibraryStockVerificationSessions" WHERE "Id"='{{second.Id:D}}';
			"""));
		forged.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
		var unlogged = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync($$"""
			UPDATE "AppLibraryStockVerificationSessions" SET "Status"=2, "ReviewStartedBy"="OpenedBy", "ReviewStartedAt"="OpenedAt"
			WHERE "Id"='{{second.Id:D}}';
			"""));
		unlogged.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
		var refusal = await Should.ThrowAsync<PostgresException>(() => database.RollbackLibrarySchemaAsync(previous));
		refusal.SqlState.ShouldBe("P0001");
	}
}
