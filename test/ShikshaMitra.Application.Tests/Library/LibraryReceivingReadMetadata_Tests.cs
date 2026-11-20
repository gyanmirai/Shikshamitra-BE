using System;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Receiving;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReceivingReadMetadata_Tests : ShikshaMitraApplicationTestBase<LibraryReceivingPreviewTestModule>
{
	[Fact]
	public async Task Receiving_Catalogue_Metadata_Should_Identify_Real_Provenance_And_Keep_Unrecorded_Copies_Removable()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(); var receipt = await ReceiveAsync(context);
		GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update).Returns(true);
		var catalogue = GetRequiredService<IBookAppService>();

		var detail = await catalogue.GetAsync(context.BookId);
		var received = detail.Copies.Single(copy => copy.CopyId == receipt.Lines.Single().Copies.Single().CopyId);
		received.ReceivingReceiptId.ShouldBe(receipt.Id); received.CanRemove.ShouldBeFalse();
		received.RemovalBlockedReason.ShouldNotBeNullOrWhiteSpace();
		var original = detail.Copies.Single(copy => copy.CopyId == context.CopyId);
		original.ReceivingReceiptId.ShouldBeNull(); original.CanRemove.ShouldBeTrue(); original.RemovalBlockedReason.ShouldBeNull();
		var paged = await catalogue.GetCopiesAsync(context.BookId);
		paged.Items.Single(copy => copy.Id == received.CopyId).ReceivingReceiptId.ShouldBe(receipt.Id);
		paged.Items.Single(copy => copy.Id == received.CopyId).CanRemove.ShouldBeFalse();
		paged.Items.Single(copy => copy.Id == context.CopyId).CanRemove.ShouldBeTrue();
		GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update).Returns(false);
		(await catalogue.GetAsync(context.BookId)).Copies.ShouldAllBe(copy => !copy.CanRemove);
	}

	[Fact]
	public async Task Receiving_Detail_Should_Return_Current_Authorized_Labels_Without_Rewriting_Its_Original_Receiving_Facts()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(); var receipt = await ReceiveAsync(context);
		await WithUnitOfWorkAsync(async () =>
		{
			var book = await GetRequiredService<IRepository<Book, Guid>>().GetAsync(context.BookId);
			book.Title = "Corrected current title";
			await GetRequiredService<IRepository<Book, Guid>>().UpdateAsync(book, true);
		});
		GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Receiving.Add).Returns(false);

		var detail = await GetRequiredService<ILibraryReceivingAppService>().GetAsync(receipt.Id);

		var line = detail.Lines.Single();
		line.CurrentBookTitle.ShouldBe("Corrected current title"); line.CurrentSectionName.ShouldBe("Main Library"); line.CurrentShelfName.ShouldBe("A1");
		line.BookId.ShouldBe(context.BookId); line.SectionId.ShouldBe(context.SectionId); line.ShelfId.ShouldBe(context.ShelfId);
		detail.ReceivedOn.ShouldBe(receipt.ReceivedOn); detail.RecordedAt.ShouldBe(receipt.RecordedAt);
		detail.SourceName.ShouldBe(receipt.SourceName); line.UnitCost.ShouldBeNull(); line.Currency.ShouldBeNull();
		line.Copies.Single().CopyId.ShouldBe(receipt.Lines.Single().Copies.Single().CopyId);
	}

	private Task<LibraryPolicyTestContext> SeedAsync() => WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
	private async Task<LibraryStockReceiptDto> ReceiveAsync(LibraryPolicyTestContext context)
	{
		var app = GetRequiredService<ILibraryReceivingAppService>();
		var input = new LibraryReceivingInput { ReceivedOn = new(2026, 11, 18), Source = LibraryStockSource.Donation, SourceName = "Actual donor",
			Lines = [new() { BookId = context.BookId, SectionId = context.SectionId, ShelfId = context.ShelfId, Quantity = 1 }] };
		var preview = await app.PreviewAsync(input); preview.CanReceive.ShouldBeTrue();
		return await app.ReceiveAsync(new() { Receipt = input, Revision = preview.Revision!, IdempotencyKey = Guid.NewGuid().ToString("N") });
	}
}
