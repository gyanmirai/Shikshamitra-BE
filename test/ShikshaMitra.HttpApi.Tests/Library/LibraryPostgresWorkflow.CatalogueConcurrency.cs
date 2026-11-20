using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Security.UserScope;
using Shouldly;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Catalogue_Postgres_Contending_Editors_Should_Preserve_Added_Copies_And_Descriptions_With_Exact_Command_Revisions()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.CataloguePrice);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var actions = string.Join(',', ShikshaMitraSubmodulePermissions.Library.AllBooks.View, ShikshaMitraSubmodulePermissions.Library.AllBooks.Update);
		using var left = OwningClient(factory, actions);
		var (copy, _) = await SeedLoanContextAsync(factory);
		var book = Guid.Parse((await database.ScalarAsync($"SELECT \"BookId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{copy:D}';"))!);
		var secondActor = await SeedCatalogueOperatorAsync(factory);
		using var right = OwningClient(factory, actions);
		right.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, secondActor.ToString("D"));
		var route = $"/api/library/books/{book:D}";
		var opened = await left.GetFromJsonAsync<BookDetailDto>(route, JsonOptions);
		opened.ShouldNotBeNull(); opened.MetadataRevision.ShouldNotBeNullOrWhiteSpace(); opened.CopiesRevision.ShouldNotBeNullOrWhiteSpace();
		var firstCopies = CatalogueCopiesInput(opened); var secondCopies = CatalogueCopiesInput(opened);
		foreach (var (input, number) in new[] { (firstCopies, 2), (secondCopies, 3) })
			input.Copies.Add(new() { CopyNumber = number, SectionId = opened.Copies[0].Section.Id, ShelfId = opened.Copies[0].Shelf.Id });
		var accepted = await ContendCatalogueAsync(database, left, right, route + "/copies", firstCopies, secondCopies);
		var current = await left.GetFromJsonAsync<BookDetailDto>(route, JsonOptions);
		current.ShouldNotBeNull(); current.Copies.Count.ShouldBe(2); current.Copies.ShouldContain(item => item.CopyId == copy);
		current.CopiesRevision.ShouldBe(accepted.Result.CopiesRevision);
		current.MetadataRevision.ShouldBe(opened.MetadataRevision);
		using (var stale = await right.PutAsJsonAsync(route + "/copies", CatalogueCopiesInput(opened), JsonOptions))
			stale.StatusCode.ShouldBe(HttpStatusCode.Conflict, await stale.Content.ReadAsStringAsync());
		(await database.ScalarAsync($"SELECT count(*) FROM \"AppBookCopies\" WHERE \"BookId\"='{book:D}';")).ShouldBe("2");
		var oldCopiesRevision = current.CopiesRevision;
		var metadata = await ContendCatalogueAsync(database, left, right, route,
			CatalogueMetadataInput(current, "Accepted left description"), CatalogueMetadataInput(current, "Accepted right description"));
		current = await left.GetFromJsonAsync<BookDetailDto>(route, JsonOptions);
		current.ShouldNotBeNull(); current.MetadataRevision.ShouldBe(metadata.Result.MetadataRevision);
		current.CopiesRevision.ShouldBe(oldCopiesRevision);
		current.Title.ShouldBe(metadata.RightWon ? "Accepted right description" : "Accepted left description");
		(await database.ScalarAsync($"SELECT \"LastModifierId\"::text FROM \"AppBooks\" WHERE \"Id\"='{book:D}';"))
			.ShouldBe(metadata.RightWon ? secondActor.ToString("D") : TestAuthHandler.TestUserId);
		using var viewer = OwningClient(factory, ShikshaMitraSubmodulePermissions.Library.AllBooks.View);
		using (var denied = await viewer.PutAsJsonAsync(route + "/copies", CatalogueCopiesInput(current), JsonOptions))
			denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await denied.Content.ReadAsStringAsync());
		await database.ExecuteAsync($"UPDATE \"AbpUsers\" SET \"IsActive\"=false WHERE \"Id\"='{secondActor:D}';");
		using (var disabled = await right.PutAsJsonAsync(route + "/copies", CatalogueCopiesInput(current), JsonOptions))
		{
			disabled.IsSuccessStatusCode.ShouldBeFalse(await disabled.Content.ReadAsStringAsync());
			using var problem = System.Text.Json.JsonDocument.Parse(await disabled.Content.ReadAsStringAsync());
			problem.RootElement.GetProperty("errorCode").GetString().ShouldBe(UserScopeErrorCodes.AccessDenied);
		}
		(await database.ScalarAsync($"SELECT count(*) FROM \"AppBookCopies\" WHERE \"BookId\"='{book:D}';")).ShouldBe("2");
	}

	private static PutBookCopiesRequest CatalogueCopiesInput(BookDetailDto detail) => new()
	{
		ExpectedCopiesRevision = detail.CopiesRevision,
		Copies = detail.Copies.Select(copy => new BookCopyItem { CopyId = copy.CopyId, CopyNumber = copy.CopyNumber,
			AccessionNumber = copy.AccessionNo, SectionId = copy.Section.Id, ShelfId = copy.Shelf.Id }).ToList()
	};

	private static UpdateBookRequest CatalogueMetadataInput(BookDetailDto detail, string title) => new()
	{
		ExpectedMetadataRevision = detail.MetadataRevision, Title = title, Author = detail.Author, Isbn = detail.Isbn,
		Publisher = detail.Publisher, Edition = detail.Edition, Subject = detail.Subject, CategoryId = detail.Category.Id,
		Language = (Language?)detail.Language, LanguageCode = detail.LanguageCode, MaterialCode = detail.MaterialCode,
		ApplicableAge = detail.ApplicableAge, Pages = detail.Pages, Price = null, PriceText = detail.PriceText, CoverImage = detail.CoverImage
	};
}
