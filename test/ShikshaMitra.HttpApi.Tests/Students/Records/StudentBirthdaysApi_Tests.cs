using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Records;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class StudentBirthdaysApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string BaseUrl = "/api/students/records/birthdays";
	private const string View = "Students.Records.Birthdays.View";
	private const string OfficeView = "Students.Records.Birthdays.OfficeScope.View";

	[Theory]
	[InlineData("")]
	[InlineData("/options")]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Office_scope_does_not_grant_access_without_birthday_view(string action)
	{
		using var client = CreatePermissionClient(OfficeView);
		var response = await client.GetAsync($"{BaseUrl}{action}?academicYearId={Factory.SeededAcademicYearId}");
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task View_without_assignment_returns_no_student_rows_or_options()
	{
		using var client = CreatePermissionClient(View);
		using var rosterResponse = await client.GetAsync($"{BaseUrl}?academicYearId={Factory.SeededAcademicYearId}");
		rosterResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await rosterResponse.Content.ReadAsStringAsync());
		var roster = await rosterResponse.Content.ReadFromJsonAsync<JsonElement>();
		roster.GetProperty("data").GetProperty("totalCount").GetInt64().ShouldBe(0);
		using var optionsResponse = await client.GetAsync($"{BaseUrl}/options?academicYearId={Factory.SeededAcademicYearId}");
		optionsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await optionsResponse.Content.ReadAsStringAsync());
		var options = await optionsResponse.Content.ReadFromJsonAsync<JsonElement>();
		options.GetProperty("data").GetProperty("divisions").GetArrayLength().ShouldBe(0);
	}

	[Theory]
	[InlineData("period=custom&startDate=2026-02-01&endDate=2026-01-01")]
	[InlineData("period=custom&startDate=2026-01-01&endDate=2027-01-02")]
	[InlineData("period=custom&startDate=2026-1-01&endDate=2026-01-05")]
	[InlineData("period=invalid")]
	[InlineData("maxResultCount=101")]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Invalid_date_or_page_parameters_are_rejected(string filter)
	{
		using var client = CreatePermissionClient(View, OfficeView);
		var response = await client.GetAsync($"{BaseUrl}?academicYearId={Factory.SeededAcademicYearId}&{filter}");
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Unknown_year_returns_an_empty_roster_and_empty_options()
	{
		using var client = CreatePermissionClient(View, OfficeView);
		var yearId = Guid.NewGuid();
		using var rosterResponse = await client.GetAsync($"{BaseUrl}?academicYearId={yearId}&divisionId={Guid.NewGuid()}");
		rosterResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await rosterResponse.Content.ReadAsStringAsync());
		var roster = await rosterResponse.Content.ReadFromJsonAsync<JsonElement>();
		roster.GetProperty("data").GetProperty("totalCount").GetInt64().ShouldBe(0);
		using var optionsResponse = await client.GetAsync($"{BaseUrl}/options?academicYearId={yearId}");
		optionsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await optionsResponse.Content.ReadAsStringAsync());
		var options = await optionsResponse.Content.ReadFromJsonAsync<JsonElement>();
		options.GetProperty("data").GetProperty("academicYears").GetArrayLength().ShouldBe(0);
		options.GetProperty("data").GetProperty("divisions").GetArrayLength().ShouldBe(0);
	}

	private HttpClient CreatePermissionClient(params string[] permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(",", permissions));
		return client;
	}
}
