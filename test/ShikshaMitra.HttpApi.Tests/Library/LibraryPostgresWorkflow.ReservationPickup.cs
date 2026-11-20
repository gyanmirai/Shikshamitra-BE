using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Npgsql;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Receiving;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Setting.Sections.Dtos;
using ShikshaMitra.Library.Shared;
using Shouldly;
using ReceivingActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Receiving;
using SectionActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Settings_SectionManagement;
using CirculationActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.IssueReturn;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Pickup_Should_Allocate_An_Arriving_Copy_At_The_Recorded_Section()
	{
		const string prior = "PersistLibraryCampusTransfers";
		const string migration = "PersistLibraryReservationPickup";
		const string route = "/api/library/reservations";
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(prior);
		await MigrateStockVerificationAsync(database, migration);
		await MigrateStockVerificationAsync(database, prior);
		await MigrateStockVerificationAsync(database, migration);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var view = ShikshaMitraSubmodulePermissions.Library.AllBooks.View;
		using var reader = OwningClient(factory, view);
		using var operatorClient = OwningClient(factory, string.Join(',', view, CirculationActions.Reserve,
			SectionActions.View, SectionActions.Add, ReceivingActions.View, ReceivingActions.Add));
		var (copy, student) = await SeedLoanContextAsync(factory);
		var tenant = Guid.Parse(TestAuthHandler.TestTenantId);
		var campus = await SeedTransferCampusAsync(factory, tenant, "PICKUP");
		var year = factory.YearId;
		var sectionInfo = await PostAsync<GenericCreatedResponse>(operatorClient, $"/api/library/sections?academicYearId={year:D}",
			new SectionCreateRequest { SectionName = "Collection desk", NumberOfShelf = 1, ShelfItems = ["Readers"] });
		var section = Guid.Parse(sectionInfo.Id);
		using var binding = await operatorClient.PutAsJsonAsync($"/api/library/sections/{section:D}/campus?academicYearId={year:D}",
			new BindLibrarySectionCampusRequest(campus), JsonOptions);
		binding.EnsureSuccessStatusCode();
		using var denied = await reader.GetAsync($"{route}/pickup-locations?academicYearId={year:D}");
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var choices = await operatorClient.GetFromJsonAsync<LibraryReservationPickupDto[]>($"{route}/pickup-locations?academicYearId={year:D}", JsonOptions);
		choices.ShouldNotBeNull();
		choices.Single().SectionId.ShouldBe(section);
		var book = Guid.Parse((await database.ScalarAsync($"""SELECT "BookId"::text FROM "AppBookCopies" WHERE "Id"='{copy:D}';"""))!);
		var shelf = Guid.Parse((await database.ScalarAsync($"""SELECT "Id"::text FROM "AppLibraryShelves" WHERE "SectionId"='{section:D}';"""))!);
		var day = new DateOnly(2026, 11, 17);
		var request = new LibraryReservationCreateRequest { AcademicYearId = year, BookId = book, MemberId = student,
			RequestedDate = day, RequestExpiryDate = day.AddDays(14), PickupSectionId = section };
		(await PostAsync<LibraryReservationPreviewDto>(operatorClient, route + "/preview", request)).CanCreate.ShouldBeTrue();
		var saved = await PostAsync<LibraryReservationDto>(operatorClient, route, request);
		saved.PickupSectionId.ShouldBe(section);
		saved.PickupCampusId.ShouldBe(campus);
		saved.State.ShouldBe((int)LibraryReservationState.Waiting);
		saved.Hold.ShouldBeNull();
		(await database.ScalarAsync($"""SELECT "Status"::text FROM "AppBookCopies" WHERE "Id"='{copy:D}';""")).ShouldBe("0");
		(await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(
			$"""UPDATE "AppBookReservations" SET "PickupLabel"='Changed' WHERE "Id"='{saved.Id:D}';"""))).SqlState.ShouldBe("23514");

		var receiving = new LibraryReceivingInput { ReceivedOn = day, Source = LibraryStockSource.Donation,
			Lines = [new() { BookId = book, SectionId = section, ShelfId = shelf, Quantity = 1, Accessions = ["PG-PICKUP-02"] }] };
		var proposed = await PostAsync<LibraryReceivingPreviewDto>(operatorClient, "/api/library/receiving/preview", receiving);
		var stock = await PostAsync<LibraryStockReceiptDto>(operatorClient, "/api/library/receiving", new LibraryReceiveCommand
			{ Receipt = receiving, Revision = proposed.Revision!, IdempotencyKey = "native-pickup-arrival" });
		var ready = await operatorClient.GetFromJsonAsync<LibraryReservationDto>($"{route}/{saved.Id:D}?academicYearId={year:D}", JsonOptions);
		ready.ShouldNotBeNull();
		ready.State.ShouldBe((int)LibraryReservationState.Ready);
		ready.Hold.ShouldNotBeNull();
		ready.Hold.CopyId.ShouldBe(stock.Lines.Single().Copies.Single().CopyId);
		ready.PickupLabel.ShouldBe(saved.PickupLabel);
		(await Should.ThrowAsync<PostgresException>(() => MigrateStockVerificationAsync(database, prior))).SqlState.ShouldBe("23514");
	}
}
