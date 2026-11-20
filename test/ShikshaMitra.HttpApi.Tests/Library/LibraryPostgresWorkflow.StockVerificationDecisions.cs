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
	public async Task Migrated_Postgres_Stock_Verification_Should_Persist_Decisions_And_Protect_Their_Audit_Trail()
	{
		const string previous = "PersistLibraryStockVerificationTransitions";
		const string migration = "PersistLibraryStockVerificationDecisions";
		const string route = "/api/library/stock-verification";
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(previous);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		using var writer = OwningClient(factory, string.Join(',', StockActions.View, StockActions.Add, StockActions.Update,
			ShikshaMitraSubmodulePermissions.Library.AllBooks.View));
		using var reader = OwningClient(factory, string.Join(',', StockActions.View, ShikshaMitraSubmodulePermissions.Library.AllBooks.View));
		var (copy, _) = await SeedLoanContextAsync(factory);
		var section = System.Guid.Parse((await database.ScalarAsync($"SELECT \"SectionId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{copy:D}';"))!);
		var input = new LibraryStockCountInput("Review reading room", section, null, new System.DateOnly(2026, 11, 19));
		var preview = await PostAsync<LibraryStockCountPreviewDto>(writer, route + "/preview", input);
		var opened = await PostAsync<LibraryStockVerificationSessionDto>(writer, route, new LibraryOpenStockCountCommand(input, preview.Revision, "open-decision-count"));
		var detail = $"{route}/{opened.Id}";
		var review = new LibraryBeginStockReviewCommand(0, "start-review");
		(await PostAsync<LibraryStockTransitionDto>(writer, detail + "/review", review)).Status.ShouldBe(LibraryStockVerificationStatus.InReview);
		await MigrateStockVerificationAsync(database, migration);
		await MigrateStockVerificationAsync(database, previous);
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockVerificationCommands\";")).ShouldBe("2");
		await MigrateStockVerificationAsync(database, migration);
		await database.MigrateToCurrentAsync();

		var current = await reader.GetFromJsonAsync<PaginatedResponse<LibraryStockReconciliationRowDto>>(detail + "/reconciliation", JsonOptions);
		current.ShouldNotBeNull();
		var item = current.Items.ShouldHaveSingleItem();
		item.Finding.ShouldBe(LibraryStockFinding.NotSeen);
		var command = new LibrarySaveStockDecisionCommand(LibraryStockReconciliationSource.Expected, copy, item.EvidenceRevision,
			null, LibraryStockDecisionOutcome.FollowUp, "Look on the returns trolley", "save-first-decision");
		using var denied = await reader.PostAsJsonAsync(detail + "/decisions", command, JsonOptions);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var concurrent = await Task.WhenAll(PostAsync<LibraryStockDecisionDto>(writer, detail + "/decisions", command),
			PostAsync<LibraryStockDecisionDto>(writer, detail + "/decisions", command));
		concurrent[0].Id.ShouldBe(concurrent[1].Id);
		concurrent[0].Version.ShouldBe(1);
		var revised = command with { IdempotencyKey = "review-finding-again", ExpectedDecisionId = concurrent[0].Id, Reason = "Check the classroom set" };
		var next = await PostAsync<LibraryStockDecisionDto>(writer, detail + "/decisions", revised);
		next.Version.ShouldBe(2);
		var decisions = await reader.GetFromJsonAsync<PaginatedResponse<LibraryStockDecisionDto>>(detail + "/decisions", JsonOptions);
		decisions.ShouldNotBeNull();
		decisions.TotalCount.ShouldBe(2);
		decisions.Items[0].Id.ShouldBe(next.Id);
		(await PostAsync<LibraryStockTransitionDto>(writer, detail + "/cancel", new LibraryCancelStockCountCommand(
			0, LibraryStockVerificationStatus.InReview, "Room unavailable", "cancel-after-deciding"))).Status.ShouldBe(LibraryStockVerificationStatus.Cancelled);
		(await PostAsync<LibraryStockDecisionDto>(writer, detail + "/decisions", command)).Id.ShouldBe(concurrent[0].Id);

		var changed = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync("UPDATE \"AppLibraryStockVerificationDecisions\" SET \"Reason\"='changed';"));
		changed.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
		var missing = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync($$"""
			INSERT INTO "AppLibraryStockVerificationCommands"
			  ("Id", "TenantId", "SessionId", "ActorId", "Kind", "IdempotencyKey", "PayloadFingerprint", "RecordedAt")
			SELECT 'bd100000-0000-0000-0000-000000000009', "TenantId", "Id", "OpenedBy", 5, 'missing-decision', repeat('A', 64), "OpenedAt"
			FROM "AppLibraryStockVerificationSessions" WHERE "Id"='{{opened.Id:D}}';
			"""));
		missing.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
		var refusal = await Should.ThrowAsync<PostgresException>(() => MigrateStockVerificationAsync(database, previous));
		refusal.SqlState.ShouldBe("P0001");
	}
}
