using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Catalogue;
using ShikshaMitra.Library.Shared;
using Shouldly;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Catalogue_Postgres_Http_Should_Persist_Explicit_Metadata_And_Serialize_Concurrent_Configuration_Saves()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.CataloguePrice);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		using var client = OwningClient(factory, string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.View,
			ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.Update,
			ShikshaMitraSubmodulePermissions.Library.AllBooks.View, ShikshaMitraSubmodulePermissions.Library.AllBooks.Add));
		var (oldCopy, _) = await SeedLoanContextAsync(factory);
		var oldBook = await database.ScalarAsync($"SELECT \"BookId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{oldCopy:D}';");
		var category = Guid.Parse((await database.ScalarAsync($"SELECT \"CategoryId\"::text FROM \"AppBooks\" WHERE \"Id\"='{oldBook}';"))!);
		var original = await database.ScalarAsync(CataloguePreservedState);
		var grants = await database.ScalarAsync("SELECT count(*) FROM \"AbpPermissionGrants\";");
		const string route = "/api/library/catalogue/configuration";
		var current = await client.GetFromJsonAsync<LibraryCatalogueConfigurationDto>(route, JsonOptions);
		current.ShouldNotBeNull(); current.Source.ShouldBe("Defaults"); current.CanManageConfiguration.ShouldBeTrue();
		current.RequiredFields.Remove("author");
		current.Languages.Add(new() { Code = "bn", Label = "বাংলা" });
		current.Materials.Add(new() { Code = "picture-book", Label = "Picture book" });
		using var save = await client.PutAsJsonAsync(route, new LibraryCatalogueConfigurationRequest
		{
			ExpectedRevision = current.Revision, Definition = current
		}, JsonOptions);
		save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
		var saved = await save.Content.ReadFromJsonAsync<LibraryCatalogueConfigurationDto>(JsonOptions);
		saved.ShouldNotBeNull(); saved.Source.ShouldBe("School");
		(await database.ScalarAsync(CataloguePreservedState)).ShouldBe(original);
		(await database.ScalarAsync($"SELECT \"Revision\" FROM \"AppLibraryCatalogueConfigurations\" WHERE \"TenantId\"='{TestAuthHandler.TestTenantId}';")).ShouldBe(saved.Revision);
		(await database.ScalarAsync("SELECT \"CreatorId\"::text FROM \"AppLibraryCatalogueConfigurations\";")).ShouldBe(TestAuthHandler.TestUserId);
		var created = await PostAsync<GenericCreatedResponse>(client, $"/api/library/books?academicYearId={factory.YearId:D}", new CreateBookRequest
		{
			Title = "Native Bengali picture book", Author = null, Publisher = "", Edition = "", Subject = "", ApplicableAge = "",
			CategoryId = category, Language = null, LanguageCode = "bn", MaterialCode = "picture-book",
			ExpectedCatalogueConfigurationRevision = saved.Revision, Price = 12.50m, Pages = 12, NumberOfCopies = 0
		});
		Guid.TryParse(created.Id, out var createdId).ShouldBeTrue();
		(await database.ScalarAsync($"SELECT count(*) FROM \"AppBooks\" WHERE \"Id\"='{createdId:D}' AND \"Language\" IS NULL AND \"LanguageCode\"='bn' AND \"MaterialCode\"='picture-book' AND \"Author\"='';")).ShouldBe("1");
		(await database.ScalarAsync($"SELECT \"Price\"::text FROM \"AppBooks\" WHERE \"Id\"='{createdId:D}';")).ShouldBe("12.50");
		using var viewer = OwningClient(factory, ShikshaMitraSubmodulePermissions.Library.AllBooks.View);
		var detail = await viewer.GetFromJsonAsync<BookDetailDto>($"/api/library/books/{createdId:D}", JsonOptions);
		detail.ShouldNotBeNull(); detail.Language.ShouldBeNull(); detail.LanguageLabel.ShouldBe("বাংলা");
		detail.MaterialLabel.ShouldBe("Picture book"); detail.Copies.ShouldBeEmpty();
		var left = CloneDefinition(saved); var right = CloneDefinition(saved);
		left.Languages.Single(choice => choice.Code == "bn").Label = "বাংলা A";
		right.Languages.Single(choice => choice.Code == "bn").Label = "বাংলা B";
		var responses = await Task.WhenAll(
			client.PutAsJsonAsync(route, new LibraryCatalogueConfigurationRequest { ExpectedRevision = saved.Revision, Definition = left }, JsonOptions),
			client.PutAsJsonAsync(route, new LibraryCatalogueConfigurationRequest { ExpectedRevision = saved.Revision, Definition = right }, JsonOptions));
		try
		{
			responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
			responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
			var winner = await responses.Single(response => response.IsSuccessStatusCode).Content.ReadFromJsonAsync<LibraryCatalogueConfigurationDto>(JsonOptions);
			winner.ShouldNotBeNull(); winner.Revision.ShouldNotBe(saved.Revision);
			var stored = await client.GetFromJsonAsync<LibraryCatalogueConfigurationDto>(route, JsonOptions);
			stored.ShouldNotBeNull(); stored.Revision.ShouldBe(winner.Revision);
			stored.Languages.Single(choice => choice.Code == "bn").Label.ShouldBe(winner.Languages.Single(choice => choice.Code == "bn").Label);
		}
		finally { foreach (var response in responses) response.Dispose(); }
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryCatalogueConfigurations\";")).ShouldBe("1");
		(await database.ScalarAsync("SELECT count(*) FROM \"AbpPermissionGrants\";")).ShouldBe(grants);
	}

	private static LibraryCatalogueDefinition CloneDefinition(LibraryCatalogueDefinition value) =>
		JsonSerializer.Deserialize<LibraryCatalogueDefinition>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
}
