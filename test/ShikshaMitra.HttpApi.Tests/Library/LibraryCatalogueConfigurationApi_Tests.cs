using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Catalogue;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCanonicalLoanApi_Tests
{
	private const string CatalogueConfigurationRoute = "/api/library/catalogue/configuration";

	[Fact]
	public async Task Catalogue_Configuration_Http_Should_Require_Its_Exact_View()
	{
		using var reader = LimitedClient(ShikshaMitraSubmodulePermissions.Library.AllBooks.View);
		using var response = await reader.GetAsync(CatalogueConfigurationRoute);
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.Update)]
	public async Task Catalogue_Configuration_Http_Should_Require_Both_Exact_Actions_For_Save(string only)
	{
		using var writer = LimitedClient(only);
		using var response = await writer.PutAsJsonAsync(CatalogueConfigurationRoute, new LibraryCatalogueConfigurationRequest
		{
			ExpectedRevision = "defaults-v1", Definition = new()
		});
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Catalogue_Configuration_Http_Should_Preserve_Defaults_Without_A_Settings_Write_Grant()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var reader = LimitedClient(ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.View);
		using var response = await reader.GetAsync(CatalogueConfigurationRoute);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var configuration = await response.Content.ReadFromJsonAsync<LibraryCatalogueConfigurationDto>();
		configuration.ShouldNotBeNull();
		configuration.Source.ShouldBe("Defaults"); configuration.Revision.ShouldBe("defaults-v1");
		configuration.RequiredFields.ShouldBe(["author", "language"]);
		configuration.Languages.Count.ShouldBe(5); configuration.Materials.ShouldBeEmpty();
		configuration.CanManageConfiguration.ShouldBeFalse();
	}
}
