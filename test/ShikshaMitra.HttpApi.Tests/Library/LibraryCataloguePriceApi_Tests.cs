using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCataloguePriceApi_Tests(LibraryApiTestFactory factory)
	: ShikshaMitraApiTestBase(factory), IClassFixture<LibraryApiTestFactory>
{
	[Theory]
	[InlineData("0.001", null)]
	[InlineData("10000000000000000.00", null)]
	[InlineData("-0.01", null)]
	[InlineData("12.51", "12.50")]
	public async Task Catalogue_Price_Http_Should_Reject_Invalid_Or_Contradictory_Prices(string exact, string? numeric)
	{
		var (client, input) = await InputAsync();
		using (client)
		{
			input["priceText"] = exact;
			input["price"] = numeric == null ? null : decimal.Parse(numeric, System.Globalization.CultureInfo.InvariantCulture);
			using var response = await client.PostAsJsonAsync($"/api/library/books?academicYearId={Factory.SeededAcademicYearId:D}", input, JsonOptions);
			response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		}
	}

	[Fact]
	public async Task Catalogue_Price_Http_Should_Clear_A_Known_Price_Only_Through_An_Explicit_Authorized_Edit()
	{
		var (client, input) = await InputAsync();
		using (client)
		{
			input["priceText"] = "12.50";
			var known = await CreateAsync(client, input);
			known.GetProperty("priceText").GetString().ShouldBe("12.50");
			var id = known.GetProperty("id").GetGuid();
			client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
			client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(',',
				ShikshaMitraSubmodulePermissions.Library.AllBooks.View, ShikshaMitraSubmodulePermissions.Library.AllBooks.Update));
			input["price"] = null; input["priceText"] = null;
			input["expectedMetadataRevision"] = known.GetProperty("metadataRevision").GetString();
			using var update = await client.PutAsJsonAsync($"/api/library/books/{id:D}", input, JsonOptions);
			update.StatusCode.ShouldBe(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
			var cleared = await client.GetFromJsonAsync<JsonElement>($"/api/library/books/{id:D}", JsonOptions);
			cleared.GetProperty("price").ValueKind.ShouldBe(JsonValueKind.Null);
			cleared.GetProperty("priceText").ValueKind.ShouldBe(JsonValueKind.Null);
		}
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Catalogue_Price_Http_Should_Save_An_Unknown_Price_Without_Inventing_Zero(bool omitted)
	{
		var (client, input) = await InputAsync();
		using (client)
		{
			if (omitted) input.Remove("price");
			else input["price"] = null;
			input["priceText"] = null;
			var book = await CreateAsync(client, input);
			book.GetProperty("price").ValueKind.ShouldBe(JsonValueKind.Null);
			book.GetProperty("priceText").ValueKind.ShouldBe(JsonValueKind.Null);
		}
	}

	[Fact]
	public async Task Catalogue_Price_Http_Should_Save_The_Full_Exact_Decimal_String_Without_A_Number_Conversion()
	{
		var (client, input) = await InputAsync();
		using (client)
		{
			input.Remove("price"); input["priceText"] = "9999999999999999.99";
			var book = await CreateAsync(client, input);
			book.GetProperty("price").GetDecimal().ShouldBe(9999999999999999.99m);
			book.GetProperty("priceText").GetString().ShouldBe("9999999999999999.99");
		}
	}

	[Fact]
	public async Task Catalogue_Price_Http_Should_Keep_A_Legacy_Known_Zero_As_A_Known_Zero()
	{
		var (client, input) = await InputAsync();
		using (client)
		{
			input["price"] = 0m;
			var book = await CreateAsync(client, input);
			book.GetProperty("price").GetDecimal().ShouldBe(0m);
		}
	}

	private async Task<(System.Net.Http.HttpClient Client, Dictionary<string, object?> Input)> InputAsync()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		Guid categoryId;
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var category = await scope.ServiceProvider.GetRequiredService<IRepository<BookCategory, Guid>>().InsertAsync(
				new BookCategory($"Price {Guid.NewGuid():N}", "", Factory.SeededAcademicYearId, Guid.Parse(TestAuthHandler.TestTenantId)), true);
			categoryId = category.Id; await unit.CompleteAsync();
		}
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.AllBooks.View, ShikshaMitraSubmodulePermissions.Library.AllBooks.Add));
		var options = await client.GetFromJsonAsync<LibraryBookEditorOptionsDto>(
			$"/api/library/books/editor-options?academicYearId={Factory.SeededAcademicYearId:D}", JsonOptions);
		options.ShouldNotBeNull(); options.CatalogueConfiguration.ShouldNotBeNull();
		return (client, new()
		{
			["title"] = $"Catalogue price {Guid.NewGuid():N}", ["author"] = "Book author", ["language"] = 2,
			["languageCode"] = "en", ["categoryId"] = categoryId, ["publisher"] = "", ["edition"] = "",
			["subject"] = "", ["applicableAge"] = "", ["pages"] = 20, ["numberOfCopies"] = 0,
			["copies"] = Array.Empty<object>(), ["expectedCatalogueConfigurationRevision"] = options.CatalogueConfiguration.Revision
		});
	}

	private async Task<JsonElement> CreateAsync(System.Net.Http.HttpClient client, Dictionary<string, object?> input)
	{
		using var response = await client.PostAsJsonAsync($"/api/library/books?academicYearId={Factory.SeededAcademicYearId:D}", input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		var created = await response.Content.ReadFromJsonAsync<GenericCreatedResponse>(JsonOptions);
		created.ShouldNotBeNull();
		return await client.GetFromJsonAsync<JsonElement>($"/api/library/books/{created.Id}", JsonOptions);
	}
}
