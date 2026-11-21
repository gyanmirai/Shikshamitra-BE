using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using Shouldly;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Catalogue_Postgres_Created_Command_Revisions_Should_Remain_Usable_After_Decimal_Storage_Normalization()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.CataloguePrice);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		using var client = OwningClient(factory, string.Join(',', ShikshaMitraSubmodulePermissions.Library.AllBooks.View,
			ShikshaMitraSubmodulePermissions.Library.AllBooks.Add, ShikshaMitraSubmodulePermissions.Library.AllBooks.Update));
		var (copy, _) = await SeedLoanContextAsync(factory);
		var category = Guid.Parse((await database.ScalarAsync($"SELECT b.\"CategoryId\"::text FROM \"AppBooks\" b JOIN \"AppBookCopies\" c ON c.\"BookId\"=b.\"Id\" WHERE c.\"Id\"='{copy:D}';"))!);
		foreach (var (numeric, exact) in new (decimal?, string?)[]
			{ (12.5m, null), (0m, null), (null, "12.5"), (null, "9999999999999999.99"), (null, null) })
		{
			var created = await PostAsync<LibraryBookCreatedResponse>(client, $"/api/library/books?academicYearId={factory.YearId:D}", new CreateBookRequest
			{
				Title = $"Created catalogue {Guid.NewGuid():N}", Author = "Known author", Language = Language.English,
				CategoryId = category, Publisher = "", Edition = "", Subject = "", ApplicableAge = "", Price = numeric, PriceText = exact
			});
			created.MetadataRevision.ShouldNotBeNullOrWhiteSpace(); created.CopiesRevision.ShouldNotBeNullOrWhiteSpace();
			var route = $"/api/library/books/{created.Id}";
			var detail = await client.GetFromJsonAsync<BookDetailDto>(route, JsonOptions);
			detail.ShouldNotBeNull(); detail.MetadataRevision.ShouldBe(created.MetadataRevision);
			detail.CopiesRevision.ShouldBe(created.CopiesRevision);
			var input = CatalogueMetadataInput(detail, $"Reviewed {created.Id}");
			input.ExpectedMetadataRevision = created.MetadataRevision;
			using var save = await client.PutAsJsonAsync(route, input, JsonOptions);
			save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
			var accepted = await save.Content.ReadFromJsonAsync<LibraryBookUpdatedResponse>(JsonOptions);
			accepted.ShouldNotBeNull();
			var current = await client.GetFromJsonAsync<BookDetailDto>(route, JsonOptions);
			current.ShouldNotBeNull(); current.MetadataRevision.ShouldBe(accepted.MetadataRevision);
			current.CopiesRevision.ShouldBe(created.CopiesRevision); current.Price.ShouldBe(detail.Price);
		}
	}
}
