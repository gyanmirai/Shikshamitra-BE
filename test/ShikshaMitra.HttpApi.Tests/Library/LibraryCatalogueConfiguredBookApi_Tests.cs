using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Catalogue;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Library;

// A separate factory keeps this school's saved override out of the shared default-school API cases.
public class LibraryCatalogueConfiguredBookApi_Tests(LibraryApiTestFactory factory)
	: ShikshaMitraApiTestBase(factory), IClassFixture<LibraryApiTestFactory>
{
	[Fact]
	public async Task Configured_Catalogue_Http_Should_Accept_Anonymous_Custom_Metadata_And_Show_It_With_View_Alone()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		const string configurationRoute = "/api/library/catalogue/configuration";
		using var initial = await Client.GetAsync(configurationRoute);
		initial.StatusCode.ShouldBe(HttpStatusCode.OK, await initial.Content.ReadAsStringAsync());
		var current = await initial.Content.ReadFromJsonAsync<LibraryCatalogueConfigurationDto>(JsonOptions);
		current.ShouldNotBeNull(); current.Source.ShouldBe("Defaults");
		current.RequiredFields.Remove("author");
		current.Languages.Add(new() { Code = "bn", Label = "বাংলা" });
		current.Materials.Add(new() { Code = "picture-book", Label = "Picture book" });
		using var updated = await Client.PutAsJsonAsync(configurationRoute, new LibraryCatalogueConfigurationRequest
		{
			ExpectedRevision = current.Revision, Definition = current
		}, JsonOptions);
		updated.StatusCode.ShouldBe(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
		var saved = await updated.Content.ReadFromJsonAsync<LibraryCatalogueConfigurationDto>(JsonOptions);
		saved.ShouldNotBeNull(); saved.Source.ShouldBe("School");
		var category = await SeedCategoryAsync();
		var title = "Anonymous Bengali picture book";
		var input = new
		{
			title, author = (string?)null, language = (int?)null, languageCode = "bn", materialCode = "picture-book",
			expectedCatalogueConfigurationRevision = saved.Revision, categoryId = category,
			publisher = "", edition = "", subject = "", applicableAge = "", price = 12.50m, pages = 12,
			numberOfCopies = 0, copies = Array.Empty<object>()
		};
		using var created = await Client.PostAsJsonAsync($"/api/library/books?academicYearId={Factory.SeededAcademicYearId:D}", input, JsonOptions);
		created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
		var result = await created.Content.ReadFromJsonAsync<GenericCreatedResponse>(JsonOptions);
		result.ShouldNotBeNull();
		using var viewer = Factory.CreateClient();
		viewer.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		viewer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.Library.AllBooks.View);
		using var read = await viewer.GetAsync($"/api/library/books/{result.Id}");
		read.StatusCode.ShouldBe(HttpStatusCode.OK, await read.Content.ReadAsStringAsync());
		using var detail = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
		detail.RootElement.GetProperty("author").GetString().ShouldBeEmpty();
		detail.RootElement.GetProperty("language").ValueKind.ShouldBe(JsonValueKind.Null);
		detail.RootElement.GetProperty("languageCode").GetString().ShouldBe("bn");
		detail.RootElement.GetProperty("languageLabel").GetString().ShouldBe("বাংলা");
		detail.RootElement.GetProperty("materialCode").GetString().ShouldBe("picture-book");
		detail.RootElement.GetProperty("materialLabel").GetString().ShouldBe("Picture book");
		using var conflictingLanguage = await Client.PostAsJsonAsync($"/api/library/books?academicYearId={Factory.SeededAcademicYearId:D}",
			input with { language = (int?)0 }, JsonOptions);
		conflictingLanguage.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await conflictingLanguage.Content.ReadAsStringAsync());
		using var staleConfiguration = await Client.PutAsJsonAsync(configurationRoute, new LibraryCatalogueConfigurationRequest
		{
			ExpectedRevision = current.Revision, Definition = current
		}, JsonOptions);
		staleConfiguration.StatusCode.ShouldBe(HttpStatusCode.Conflict, await staleConfiguration.Content.ReadAsStringAsync());
		var retained = await Client.GetFromJsonAsync<LibraryCatalogueConfigurationDto>(configurationRoute, JsonOptions);
		retained.ShouldNotBeNull(); retained.Revision.ShouldBe(saved.Revision);
	}

	private async Task<Guid> SeedCategoryAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenant = Guid.Parse(TestAuthHandler.TestTenantId);
		using var school = services.GetRequiredService<ICurrentTenant>().Change(tenant);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var category = await services.GetRequiredService<IRepository<BookCategory, Guid>>().InsertAsync(
			new BookCategory("Configured picture books", "", Factory.SeededAcademicYearId, tenant), true);
		await unit.CompleteAsync();
		return category.Id;
	}
}
