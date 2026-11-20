using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Receiving;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCatalogueDeletionApi_Tests(LibraryApiTestFactory factory)
	: ShikshaMitraApiTestBase(factory), IClassFixture<LibraryApiTestFactory>
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Catalogue_Title_Deletion_Http_Should_Respect_Its_Own_Action_Without_An_Update_Grant(bool deleteGranted)
	{
		var id = await SeedUnusedTitleAsync();
		using var client = DeleteClient(deleteGranted);
		var route = $"/api/library/books/{id:D}";
		var detail = await client.GetFromJsonAsync<BookDetailDto>(route, JsonOptions);
		detail.ShouldNotBeNull(); detail.CanDelete.ShouldBe(deleteGranted);
		detail.Copies.ShouldHaveSingleItem(); detail.Copies[0].CanRemove.ShouldBeFalse();
		if (deleteGranted)
		{
			var update = new UpdateBookRequest { Title = "An unauthorized metadata edit", Author = detail.Author,
				CategoryId = detail.Category.Id, Language = (Language?)detail.Language, LanguageCode = detail.LanguageCode,
				Publisher = detail.Publisher, Edition = detail.Edition, Subject = detail.Subject, ApplicableAge = detail.ApplicableAge,
				PriceText = detail.PriceText, ExpectedMetadataRevision = detail.MetadataRevision };
			using var deniedEdit = await client.PutAsJsonAsync(route, update, JsonOptions);
			deniedEdit.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await deniedEdit.Content.ReadAsStringAsync());
		}
		using var deletion = await client.DeleteAsync(route);
		deletion.StatusCode.ShouldBe(deleteGranted ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
			await deletion.Content.ReadAsStringAsync());
		if (!deleteGranted)
		{
			detail.DeletionBlockedReason.ShouldNotBeNullOrWhiteSpace();
			(await client.GetFromJsonAsync<BookDetailDto>(route, JsonOptions)).ShouldNotBeNull();
		}
	}

	[Fact]
	public async Task Catalogue_Title_Deletion_Http_Should_Recheck_Receiving_After_An_Earlier_Allowed_Detail_Read()
	{
		var id = await SeedUnusedTitleAsync();
		using var client = DeleteClient(true);
		var route = $"/api/library/books/{id:D}";
		var opened = await client.GetFromJsonAsync<BookDetailDto>(route, JsonOptions);
		opened.ShouldNotBeNull(); opened.CanDelete.ShouldBeTrue();
		using var receiver = ClientWithPermissions(string.Join(',', ShikshaMitraSubmodulePermissions.Library.Receiving.View,
			ShikshaMitraSubmodulePermissions.Library.Receiving.Add, ShikshaMitraSubmodulePermissions.Library.AllBooks.View));
		var input = new LibraryReceivingInput { ReceivedOn = new(2026, 11, 18), Source = LibraryStockSource.Donation,
			Lines = [new() { BookId = id, SectionId = opened.Copies[0].Section.Id, ShelfId = opened.Copies[0].Shelf.Id, Quantity = 1 }] };
		using var previewResponse = await receiver.PostAsJsonAsync("/api/library/receiving/preview", input, JsonOptions);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var preview = await previewResponse.Content.ReadFromJsonAsync<LibraryReceivingPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull(); preview.CanReceive.ShouldBeTrue();
		using var received = await receiver.PostAsJsonAsync("/api/library/receiving", new LibraryReceiveCommand
			{ Receipt = input, Revision = preview.Revision!, IdempotencyKey = Guid.NewGuid().ToString("N") }, JsonOptions);
		received.IsSuccessStatusCode.ShouldBeTrue(await received.Content.ReadAsStringAsync());
		using var rejected = await client.DeleteAsync(route);
		rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await rejected.Content.ReadAsStringAsync());
		var retained = await client.GetFromJsonAsync<BookDetailDto>(route, JsonOptions);
		retained.ShouldNotBeNull(); retained.CanDelete.ShouldBeFalse(); retained.DeletionBlockedReason.ShouldNotBeNullOrWhiteSpace();
		retained.Copies.Count.ShouldBe(2);
	}
}
