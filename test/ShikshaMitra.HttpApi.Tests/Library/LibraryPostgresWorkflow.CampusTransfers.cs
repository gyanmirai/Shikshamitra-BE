using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.CampusTransfers;
using ShikshaMitra.Library.InventoryVerification;
using ShikshaMitra.Library.Receiving;
using ShikshaMitra.Library.Setting.Sections.Dtos;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using TransferActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.CampusTransfers;
using SectionActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Settings_SectionManagement;
using CountActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.InventoryVerification;
using ReceivingActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Receiving;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Campus_Transfer_Should_Preserve_Sealed_Dispatch_And_Actual_Receipt()
	{
		const string prior = "PersistLibraryClassroomCustody";
		const string campuses = "BindLibrarySectionsToCampuses";
		const string migration = "PersistLibraryCampusTransfers";
		const string route = "/api/library/campus-transfers";
		const string countRoute = "/api/library/stock-verification";
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(prior);
		await MigrateStockVerificationAsync(database, migration);
		await MigrateStockVerificationAsync(database, prior);
		await MigrateStockVerificationAsync(database, migration);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var view = string.Join(',', TransferActions.View, ShikshaMitraSubmodulePermissions.Library.AllBooks.View);
		using var reader = OwningClient(factory, string.Join(',', view, SectionActions.View));
		using var operatorClient = OwningClient(factory, string.Join(',', view, TransferActions.Dispatch, TransferActions.Receive,
			SectionActions.View, SectionActions.Add, CountActions.View, CountActions.Add, CountActions.Update,
			CountActions.Approve, ReceivingActions.View, ReceivingActions.Add));
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var north = await SeedTransferCampusAsync(factory, tenantId, "NORTH");
		var south = await SeedTransferCampusAsync(factory, tenantId, "SOUTH");
		var foreign = await SeedTransferCampusAsync(factory, Guid.NewGuid(), "FOREIGN");
		var (copy, _) = await SeedLoanContextAsync(factory);
		var book = Guid.Parse((await database.ScalarAsync($"""SELECT "BookId"::text FROM "AppBookCopies" WHERE "Id"='{copy:D}';"""))!);
		var source = Guid.Parse((await database.ScalarAsync($"""SELECT "SectionId"::text FROM "AppBookCopies" WHERE "Id"='{copy:D}';"""))!);
		var sourceShelf = Guid.Parse((await database.ScalarAsync($"""SELECT "ShelfId"::text FROM "AppBookCopies" WHERE "Id"='{copy:D}';"""))!);
		var year = factory.YearId;
		var sections = $"/api/library/sections?academicYearId={year:D}";
		var destinationRecord = await PostAsync<GenericCreatedResponse>(operatorClient, sections,
			new SectionCreateRequest { SectionName = "New Branch", NumberOfShelf = 1, ShelfItems = ["Readers"] });
		var destination = Guid.Parse(destinationRecord.Id);
		var destinationShelf = Guid.Parse((await database.ScalarAsync($"""SELECT "Id"::text FROM "AppLibraryShelves" WHERE "SectionId"='{destination:D}';"""))!);
		var assignment = $"/api/library/sections/{source:D}/campus?academicYearId={year:D}";
		using var denied = await reader.PutAsJsonAsync(assignment, new BindLibrarySectionCampusRequest(north), JsonOptions);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var otherTenant = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(
			$"""UPDATE "AppLibrarySections" SET "CampusId"='{foreign:D}' WHERE "Id"='{source:D}';"""));
		otherTenant.SqlState.ShouldBe("23514");
		using var assignedSource = await operatorClient.PutAsJsonAsync(assignment, new BindLibrarySectionCampusRequest(north), JsonOptions);
		assignedSource.EnsureSuccessStatusCode();
		using var assignedDestination = await operatorClient.PutAsJsonAsync(
			$"/api/library/sections/{destination:D}/campus?academicYearId={year:D}", new BindLibrarySectionCampusRequest(south), JsonOptions);
		assignedDestination.EnsureSuccessStatusCode();
		var move = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(
			$"""UPDATE "AppBookCopies" SET "SectionId"='{destination:D}', "ShelfId"='{destinationShelf:D}' WHERE "Id"='{copy:D}';"""));
		move.SqlState.ShouldBe("23514");

		var day = new DateOnly(2026, 11, 17);
		var receiving = new LibraryReceivingInput { ReceivedOn = day, Source = LibraryStockSource.Donation,
			Lines = [new() { BookId = book, SectionId = source, ShelfId = sourceShelf, Quantity = 1, Accessions = ["PG-TRANSFER-02"] }] };
		var proposed = await PostAsync<LibraryReceivingPreviewDto>(operatorClient, "/api/library/receiving/preview", receiving);
		var received = await PostAsync<LibraryStockReceiptDto>(operatorClient, "/api/library/receiving",
			new LibraryReceiveCommand { Receipt = receiving, Revision = proposed.Revision!, IdempotencyKey = "transfer-stock" });
		var spare = received.Lines.Single().Copies.Single().CopyId;
		var input = new LibraryCampusDispatchInput(source, destination, destinationShelf, day, "School reading exchange", [copy, spare]);
		using var cannotDispatch = await reader.PostAsJsonAsync(route + "/preview", input, JsonOptions);
		cannotDispatch.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var preview = await PostAsync<LibraryCampusTransferPreview>(operatorClient, route + "/preview", input);
		preview.Errors.ShouldBeEmpty();
		var command = new LibraryCampusDispatchCommand(input, preview.Revision!, "native-campus-dispatch");
		var replay = await Task.WhenAll(PostAsync<LibraryCampusTransferHeader>(operatorClient, route, command),
			PostAsync<LibraryCampusTransferHeader>(operatorClient, route, command));
		replay[0].ShouldBe(replay[1]);
		var transferRoute = $"{route}/{replay[0].Id:D}";
		(await database.ScalarAsync($"""SELECT "SectionId"::text FROM "AppBookCopies" WHERE "Id"='{copy:D}';""")).ShouldBe(source.ToString());
		var shelfRemoved = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(
			$"""DELETE FROM "AppLibraryShelves" WHERE "Id"='{destinationShelf:D}';"""));
		shelfRemoved.SqlState.ShouldBe("23514");

		var countInput = new LibraryStockCountInput("Verify dispatched set", source, sourceShelf, day);
		var baseline = await PostAsync<LibraryStockCountPreviewDto>(operatorClient, countRoute + "/preview", countInput);
		var count = await PostAsync<LibraryStockVerificationSessionDto>(operatorClient, countRoute,
			new LibraryOpenStockCountCommand(countInput, baseline.Revision, "transfer-stock-count"));
		var session = $"{countRoute}/{count.Id:D}";
		await PostAsync<LibraryStockTransitionDto>(operatorClient, session + "/review", new LibraryBeginStockReviewCommand(0, "transfer-count-review"));
		var publication = await operatorClient.GetFromJsonAsync<LibraryStockApprovalPreviewDto>(session + "/approval-preview", JsonOptions);
		publication.ShouldNotBeNull();
		publication.CanFinalize.ShouldBeTrue();
		await PostAsync<LibraryStockTransitionDto>(operatorClient, session + "/finalize",
			new LibraryFinalizeStockCountCommand(publication.Revision, "transfer-count-finalize"));
		(await database.ScalarAsync("""SELECT count(*) FROM "AppLibraryStockVerificationResults" WHERE "Finding"=17;""")).ShouldBe("2");

		var receipt = new LibraryCampusReceiveInput(day.AddDays(1), "Only the first parcel arrived", [new(copy, BookCopyStatus.Damaged)]);
		using var cannotReceive = await reader.PostAsJsonAsync(transferRoute + "/receipts/preview", receipt, JsonOptions);
		cannotReceive.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var receivePlan = await PostAsync<LibraryCampusTransferPreview>(operatorClient, transferRoute + "/receipts/preview", receipt);
		receivePlan.Errors.ShouldBeEmpty();
		var receiptCommand = new LibraryCampusReceiveCommand(receipt, receivePlan.Revision!, "native-campus-receipt");
		var saved = await PostAsync<LibraryCampusTransferReceiptDto>(operatorClient, transferRoute + "/receipts", receiptCommand);
		(await PostAsync<LibraryCampusTransferReceiptDto>(operatorClient, transferRoute + "/receipts", receiptCommand)).Id.ShouldBe(saved.Id);
		var detail = await reader.GetFromJsonAsync<LibraryCampusTransferDetail>(transferRoute, JsonOptions);
		detail.ShouldNotBeNull();
		detail.PendingCount.ShouldBe(1);
		detail.Copies.Single(row => row.ReceiptId == null).CopyId.ShouldBe(spare);
		(await database.ScalarAsync($"""SELECT "SectionId"::text FROM "AppBookCopies" WHERE "Id"='{copy:D}';""")).ShouldBe(destination.ToString());
		(await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(
			"""UPDATE "AppLibraryCampusTransferItems" SET "Title"='Changed';"""))).SqlState.ShouldBe("23514");
		(await Should.ThrowAsync<PostgresException>(() => database.RollbackLibrarySchemaAsync(campuses))).SqlState.ShouldBe("23514");
	}

	private static async Task<Guid> SeedTransferCampusAsync(LibraryPostgresApiTestFactory factory, Guid tenant, string code)
	{
		using var scope = factory.Services.CreateScope();
		using var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var campus = new AdmissionCampus(Guid.NewGuid(), tenant, code, code + " test campus");
		await scope.ServiceProvider.GetRequiredService<IRepository<AdmissionCampus, Guid>>().InsertAsync(campus);
		await unit.CompleteAsync();
		return campus.Id;
	}
}
