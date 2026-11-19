using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using Shouldly;
using Xunit;

namespace ShikshaMitra.TenantSettings;

[Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public class AcademicNamePreviewApi_Tests(AcademicFieldFormattingApiFactory factory) : IClassFixture<AcademicFieldFormattingApiFactory>
{
	private const string Route = "/api/app/academic-field-formatting/preview";

	[Theory]
	[InlineData("İSTANBUL", AcademicTextCase.CamelCase, "İstanbul")]
	[InlineData("ΟΣ", AcademicTextCase.CamelCase, "οσ")]
	[InlineData("ßeta", AcademicTextCase.PascalCase, "ßeta")]
	[InlineData(" mIxEd\tStage ", AcademicTextCase.Default, " mIxEd\tStage ")]
	[InlineData("", AcademicTextCase.Capitalize, "")]
	[InlineData("  école\tκόσμος ßeta  ", AcademicTextCase.Uppercase, "  ÉCOLE\tΚΌΣΜΟΣ ßETA  ")]
	[InlineData("", AcademicTextCase.Uppercase, "")]
	public async Task Preview_uses_authoritative_casing_without_saving_settings(string value, AcademicTextCase textCase, string expected)
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View);
		var before = await client.GetStringAsync("/api/app/academic-field-formatting");
		var response = await client.PostAsJsonAsync(Route, new { value, textCase });
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		(await response.Content.ReadFromJsonAsync<AcademicNamePreviewDto>())!.Value.ShouldBe(expected);
		(await client.GetStringAsync("/api/app/academic-field-formatting")).ShouldBe(before);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(5)]
	public async Task Invalid_modes_are_rejected(int textCase)
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		(await client.PostAsJsonAsync(Route, new { value = "İSTANBUL", textCase })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Null_and_oversized_values_are_rejected()
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		(await client.PostAsJsonAsync(Route, new { value = (string?)null, textCase = 0 })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await client.PostAsJsonAsync(Route, new { value = new string('é', 513), textCase = 1 })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Preview_rejects_host_and_missing_view_permission()
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "none");
		(await client.PostAsJsonAsync(Route, new { value = "ΟΣ", textCase = 2 })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host");
		(await client.PostAsJsonAsync(Route, new { value = "ΟΣ", textCase = 2 })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
}
