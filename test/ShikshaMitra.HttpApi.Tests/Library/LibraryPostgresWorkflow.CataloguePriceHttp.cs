using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using Shouldly;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Catalogue_Price_Postgres_Http_Should_Keep_Unknown_Zero_And_Full_Exact_Prices_Distinct()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.CataloguePrice);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		using var client = OwningClient(factory, string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.AllBooks.View, ShikshaMitraSubmodulePermissions.Library.AllBooks.Add));
		var (copy, _) = await SeedLoanContextAsync(factory);
		var category = Guid.Parse((await database.ScalarAsync($"SELECT b.\"CategoryId\"::text FROM \"AppBooks\" b JOIN \"AppBookCopies\" c ON c.\"BookId\"=b.\"Id\" WHERE c.\"Id\"='{copy:D}';"))!);
		using var viewer = OwningClient(factory, ShikshaMitraSubmodulePermissions.Library.AllBooks.View);
		foreach (var exact in new string?[] { null, "0.00", "9999999999999999.99" })
		{
			var created = await PostAsync<GenericCreatedResponse>(client, $"/api/library/books?academicYearId={factory.YearId:D}", new CreateBookRequest
			{
				Title = $"Exact catalogue {Guid.NewGuid():N}", Author = "Known author", Publisher = "", Edition = "", Subject = "", ApplicableAge = "",
				CategoryId = category, Language = Language.English, LanguageCode = "en", Price = null, PriceText = exact, Pages = 10
			});
			var id = Guid.Parse(created.Id);
			var detail = await viewer.GetFromJsonAsync<BookDetailDto>($"/api/library/books/{id:D}", JsonOptions);
			detail.ShouldNotBeNull(); detail.PriceText.ShouldBe(exact);
			var stored = await database.ScalarAsync($"SELECT \"Price\"::text FROM \"AppBooks\" WHERE \"Id\"='{id:D}';");
			stored.ShouldBe(exact);
			if (exact == null) detail.Price.ShouldBeNull();
			else detail.Price.ShouldBe(decimal.Parse(exact, System.Globalization.CultureInfo.InvariantCulture));
			detail.Copies.ShouldBeEmpty();
		}
	}
}
