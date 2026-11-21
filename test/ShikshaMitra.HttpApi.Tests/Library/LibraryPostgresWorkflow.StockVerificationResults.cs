using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Npgsql;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.InventoryVerification;
using ShikshaMitra.Library.Receiving;
using ShikshaMitra.Library.Shared;
using Shouldly;
using StockActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.InventoryVerification;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Stock_Verification_Should_Publish_An_Immutable_Approved_Result()
	{
		const string previous = "PersistLibraryStockVerificationDecisions";
		const string migration = "PersistLibraryStockVerificationResults";
		const string receiptEvidence = "AddLibraryStockResultReceiptEvidence";
		const string route = "/api/library/stock-verification";
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(previous);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var view = string.Join(',', StockActions.View, ShikshaMitraSubmodulePermissions.Library.AllBooks.View);
		using var reader = OwningClient(factory, view);
		using var writer = OwningClient(factory, string.Join(',', view, StockActions.Add, StockActions.Update,
			ShikshaMitraSubmodulePermissions.Library.Receiving.View, ShikshaMitraSubmodulePermissions.Library.Receiving.Add));
		using var approver = OwningClient(factory, string.Join(',', view, StockActions.Approve));
		var (copy, _) = await SeedLoanContextAsync(factory);
		var section = Guid.Parse((await database.ScalarAsync($"SELECT \"SectionId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{copy:D}';"))!);
		var shelf = Guid.Parse((await database.ScalarAsync($"SELECT \"ShelfId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{copy:D}';"))!);
		var accession = (await database.ScalarAsync($"SELECT \"AccessionNumber\" FROM \"AppBookCopies\" WHERE \"Id\"='{copy:D}';"))!;
		var book = Guid.Parse((await database.ScalarAsync($"SELECT \"BookId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{copy:D}';"))!);
		var input = new LibraryStockCountInput("Verify the collection", section, shelf, new DateOnly(2026, 11, 19));
		var preview = await PostAsync<LibraryStockCountPreviewDto>(writer, route + "/preview", input);
		var opened = await PostAsync<LibraryStockVerificationSessionDto>(writer, route, new LibraryOpenStockCountCommand(input, preview.Revision, "open-result"));
		var detail = $"{route}/{opened.Id}";
		var receipt = new LibraryReceivingInput
		{
			ReceivedOn = input.SchoolDate!.Value.AddDays(-1),
			Source = LibraryStockSource.Donation,
			SourceName = "School community",
			Lines = [new() { BookId = book, SectionId = section, ShelfId = shelf, Quantity = 1, Accessions = ["NEW-RECEIPT-COUNT"] }]
		};
		var staged = await PostAsync<LibraryReceivingPreviewDto>(writer, "/api/library/receiving/preview", receipt);
		staged.CanReceive.ShouldBeTrue();
		await PostAsync<LibraryStockReceiptDto>(writer, "/api/library/receiving",
			new LibraryReceiveCommand { Receipt = receipt, Revision = staged.Revision!, IdempotencyKey = "receive-result" });
		await PostAsync<LibraryStockObservationDto>(writer, detail + "/observations",
			new LibraryRecordStockObservationCommand(accession, section, shelf, input.SchoolDate, "recognized-result-scan"));
		await PostAsync<LibraryStockObservationDto>(writer, detail + "/observations",
			new LibraryRecordStockObservationCommand("UNKNOWN-PUBLISHED", section, shelf, input.SchoolDate, "unknown-result-scan"));
		await PostAsync<LibraryStockObservationDto>(writer, detail + "/observations",
			new LibraryRecordStockObservationCommand("NEW-RECEIPT-COUNT", section, shelf, input.SchoolDate, "received-result-scan"));
		await PostAsync<LibraryStockTransitionDto>(writer, detail + "/review", new LibraryBeginStockReviewCommand(3, "review-result"));
		var pending = await writer.GetFromJsonAsync<PaginatedResponse<LibraryStockReconciliationRowDto>>(detail + "/reconciliation?source=3", JsonOptions);
		pending.ShouldNotBeNull();
		var finding = pending.Items.ShouldHaveSingleItem();
		var decision = await PostAsync<LibraryStockDecisionDto>(writer, detail + "/decisions", new LibrarySaveStockDecisionCommand(
			LibraryStockReconciliationSource.UnknownScans, finding.Id, finding.EvidenceRevision, null,
			LibraryStockDecisionOutcome.FollowUp, "Check the unidentified label.", "decision-result"));
		var outside = await writer.GetFromJsonAsync<PaginatedResponse<LibraryStockReconciliationRowDto>>(detail + "/reconciliation?source=2", JsonOptions);
		outside.ShouldNotBeNull();
		var received = outside.Items.ShouldHaveSingleItem();
		received.ReceivedAfterOpening.ShouldBeTrue();
		var receiptDecision = await PostAsync<LibraryStockDecisionDto>(writer, detail + "/decisions", new LibrarySaveStockDecisionCommand(
			LibraryStockReconciliationSource.OutsideBaseline, received.Id, received.EvidenceRevision, null,
			LibraryStockDecisionOutcome.EvidenceVerified, "Matched the donation receipt.", "decision-receipt"));
		await MigrateStockVerificationAsync(database, receiptEvidence);
		await MigrateStockVerificationAsync(database, previous);
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockVerificationDecisions\";")).ShouldBe("2");
		await MigrateStockVerificationAsync(database, migration);
		await MigrateStockVerificationAsync(database, receiptEvidence);
		await database.MigrateToCurrentAsync();

		using var denied = await writer.GetAsync(detail + "/approval-preview");
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var ready = await approver.GetFromJsonAsync<LibraryStockApprovalPreviewDto>(detail + "/approval-preview", JsonOptions);
		ready.ShouldNotBeNull();
		ready.CanFinalize.ShouldBeTrue();
		ready.FindingCount.ShouldBe(3);
		var approve = new LibraryFinalizeStockCountCommand(ready.Revision, "publish-result");
		using var deniedCommand = await writer.PostAsJsonAsync(detail + "/finalize", approve, JsonOptions);
		deniedCommand.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var completed = await Task.WhenAll(PostAsync<LibraryStockTransitionDto>(approver, detail + "/finalize", approve),
			PostAsync<LibraryStockTransitionDto>(approver, detail + "/finalize", approve));
		completed[0].Status.ShouldBe(LibraryStockVerificationStatus.Finalized);
		completed[0].ShouldBe(completed[1]);
		await database.ExecuteAsync($"UPDATE \"AppBookCopies\" SET \"Status\"=4 WHERE \"Id\"='{copy:D}';");
		var result = await reader.GetFromJsonAsync<LibraryStockPublishedResultDto>(detail + "/result", JsonOptions);
		result.ShouldNotBeNull();
		result.ObservationCount.ShouldBe(3);
		result.FindingCount.ShouldBe(3);
		result.DiscrepancyCount.ShouldBe(2);
		result.Findings.Items.ShouldHaveSingleItem().CurrentStatus.ShouldBe(BookCopyStatus.Available);
		var unknown = await reader.GetFromJsonAsync<LibraryStockPublishedResultDto>(detail + "/result?source=3", JsonOptions);
		unknown.ShouldNotBeNull();
		unknown.Findings.Items.ShouldHaveSingleItem().Decision!.Id.ShouldBe(decision.Id);
		var added = await reader.GetFromJsonAsync<LibraryStockPublishedResultDto>(detail + "/result?source=2", JsonOptions);
		added.ShouldNotBeNull();
		added.Findings.Items.ShouldHaveSingleItem().ReceivedAfterOpening.ShouldBeTrue();
		added.Findings.Items[0].Decision!.Id.ShouldBe(receiptDecision.Id);
		var fabricatedReceipt = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync($$"""
			INSERT INTO "AppLibraryStockVerificationResults"
			    ("Id", "TenantId", "SessionId", "CommandId", "Source", "RowId", "Position", "AccessionNumber",
			     "Finding", "EvidenceRevision", "DecisionId", "ReceivedAfterOpening")
			SELECT '9fc00000-0000-0000-0000-000000000001'::uuid, "TenantId", "SessionId", "CommandId", 2, '{{finding.Id:D}}'::uuid,
			    4, 'UNKNOWN-PUBLISHED', 14, "EvidenceRevision", "DecisionId", TRUE
			FROM "AppLibraryStockVerificationResults" WHERE "SessionId"='{{opened.Id:D}}' AND "ReceivedAfterOpening";
			"""));
		fabricatedReceipt.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
		fabricatedReceipt.MessageText.ShouldContain("receipt recorded");
		var changed = await Should.ThrowAsync<PostgresException>(() =>
			database.ExecuteAsync("UPDATE \"AppLibraryStockVerificationResults\" SET \"AccessionNumber\"='changed';"));
		changed.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
		var refused = await Should.ThrowAsync<PostgresException>(() => database.RollbackLibrarySchemaAsync(previous));
		refused.SqlState.ShouldBe("P0001");
		refused.MessageText.ShouldContain("published receipt evidence");
	}
}
