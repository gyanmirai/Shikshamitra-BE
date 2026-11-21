using System;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Receiving;
using ShikshaMitra.Library.Shared;
using Shouldly;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Receiving_Http_Should_Commit_And_Replay_Exact_New_Copy_Provenance()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.CataloguePrice);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		using var client = OwningClient(factory, string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.Receiving.View, ShikshaMitraSubmodulePermissions.Library.Receiving.Add,
			ShikshaMitraSubmodulePermissions.Library.AllBooks.View));
		var (oldCopyId, _) = await SeedLoanContextAsync(factory);
		var bookId = Guid.Parse((await database.ScalarAsync($"SELECT \"BookId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{oldCopyId:D}';"))!);
		var sectionId = Guid.Parse((await database.ScalarAsync($"SELECT \"SectionId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{oldCopyId:D}';"))!);
		var shelfId = Guid.Parse((await database.ScalarAsync($"SELECT \"ShelfId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{oldCopyId:D}';"))!);
		var input = new LibraryReceivingInput
		{
			ReceivedOn = new(2026, 11, 18), Source = LibraryStockSource.Donation, SourceName = "Community donor",
			Lines =
			[
				new() { BookId = bookId, SectionId = sectionId, ShelfId = shelfId, Quantity = 1 },
				new() { BookId = bookId, SectionId = sectionId, ShelfId = shelfId, Quantity = 1, UnitCost = "9999999999999998.99", Currency = "INR" }
			]
		};
		var plan = await PostAsync<LibraryReceivingPreviewDto>(client, "/api/library/receiving/preview", input);
		plan.CanReceive.ShouldBeTrue();
		var command = new LibraryReceiveCommand { Receipt = input, Revision = plan.Revision!, IdempotencyKey = Guid.NewGuid().ToString("N") };
		var receipt = await PostAsync<LibraryStockReceiptDto>(client, "/api/library/receiving", command);
		var replay = await PostAsync<LibraryStockReceiptDto>(client, "/api/library/receiving", command);
		var detail = await client.GetFromJsonAsync<LibraryStockReceiptDto>($"/api/library/receiving/{receipt.Id}", JsonOptions);
		detail.ShouldNotBeNull();
		replay.Id.ShouldBe(receipt.Id); detail.Id.ShouldBe(receipt.Id);
		replay.RecordedAt.UtcTicks.ShouldBe(receipt.RecordedAt.UtcTicks); detail.RecordedAt.UtcTicks.ShouldBe(receipt.RecordedAt.UtcTicks);
		receipt.RecordedAt.Offset.ShouldBe(TimeSpan.Zero); detail.RecordedAt.Offset.ShouldBe(TimeSpan.Zero);
		replay.RecordedBy.ShouldBe(receipt.RecordedBy); detail.RecordedBy.ShouldBe(receipt.RecordedBy);
		detail.CopyCount.ShouldBe(2); detail.ReceivedOn.ShouldBe(input.ReceivedOn);
		detail.Lines.Count.ShouldBe(2);
		detail.Lines[0].UnitCost.ShouldBeNull(); detail.Lines[0].Currency.ShouldBeNull();
		detail.Lines[1].UnitCost.ShouldBe("9999999999999998.99"); detail.Lines[1].Currency.ShouldBe("INR");
		var copyIds = detail.Lines.SelectMany(line => line.Copies).Select(copy => copy.CopyId).ToArray();
		copyIds.Distinct().Count().ShouldBe(2); copyIds.ShouldNotContain(oldCopyId);
		replay.Lines.SelectMany(line => line.Copies).Select(copy => copy.CopyId).ShouldBe(copyIds);
		using var reader = OwningClient(factory, string.Join(',', ShikshaMitraSubmodulePermissions.Library.Receiving.View,
			ShikshaMitraSubmodulePermissions.Library.AllBooks.View));
		var register = await reader.GetFromJsonAsync<PaginatedResponse<LibraryStockReceiptSummaryDto>>(
			$"/api/library/receiving?copyId={copyIds[0]}&pageSize=1", JsonOptions);
		register.ShouldNotBeNull(); register.TotalCount.ShouldBe(1); register.Items.Single().Id.ShouldBe(receipt.Id);
		(await database.ScalarAsync($"SELECT count(*) FROM \"AppBookCopies\" WHERE \"BookId\"='{bookId:D}';")).ShouldBe("3");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockReceiptCommands\";")).ShouldBe("1");
		(await database.ScalarAsync($"SELECT count(*) FROM \"AppLibraryReceivedCopies\" WHERE \"CopyId\"='{oldCopyId:D}';")).ShouldBe("0");
		(await database.ScalarAsync("SELECT \"UnitCost\"::text FROM \"AppLibraryStockReceiptLines\" WHERE \"UnitCost\" IS NOT NULL;")).ShouldBe("9999999999999998.99");
	}
}
