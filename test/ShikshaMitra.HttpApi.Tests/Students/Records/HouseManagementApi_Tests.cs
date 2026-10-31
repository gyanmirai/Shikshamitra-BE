using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Records.HouseManagement;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Records;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class HouseManagementApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Root = "/api/students/records/house-management";
	[Theory]
	[InlineData("GET", "houses", "Students.Records.HouseManagement.Add")]
	[InlineData("GET", "students", "Students.Records.HouseManagement.Add")]
	[InlineData("GET", "divisions", "Students.Records.HouseManagement.Delete")]
	[InlineData("POST", "houses", "Students.Records.HouseManagement.View")]
	[InlineData("PUT", "houses/11111111-1111-1111-1111-111111111111", "Students.Records.HouseManagement.View")]
	[InlineData("DELETE", "houses/11111111-1111-1111-1111-111111111111", "Students.Records.HouseManagement.Add")]
	[InlineData("POST", "assignments/preview", "Students.Records.HouseManagement.View")]
	[InlineData("POST", "assignments/commit", "Students.Records.HouseManagement.View")]
	[InlineData("PUT", "houses/11111111-1111-1111-1111-111111111111/leader", "Students.Records.HouseManagement.View")]
	[InlineData("PUT", "divisions/11111111-1111-1111-1111-111111111111/leader", "Students.Records.HouseManagement.View")]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Every_endpoint_requires_its_exact_action(string method, string path, string unrelatedAction)
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, unrelatedAction);
		using var request = new HttpRequestMessage(new HttpMethod(method), $"{Root}/{path}?academicMediumId={Factory.SeededMediumId}");
		if (method is "POST" or "PUT")
			request.Content = JsonContent.Create(new { });
		using var response = await client.SendAsync(request);
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Anonymous_house_reads_are_rejected()
	{
		using var client = CreateAnonymousClient();
		using var response = await client.GetAsync($"{Root}/houses?academicMediumId={Factory.SeededMediumId}");
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task House_contract_persists_details_and_returns_safe_stale_preview_message()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var input = new SaveHouseRequest
		{
			AcademicMediumId = Factory.SeededMediumId,
			Name = $"API Falcons {Guid.NewGuid():N}",
			Color = "#2468ab"
		};
		using var created = await Client.PostAsJsonAsync($"{Root}/houses", input, JsonOptions);
		created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
		var house = (await created.Content.ReadFromJsonAsync<Result<HouseManagementHouseDto>>(JsonOptions))!.Data!;
		house.Name.ShouldBe(input.Name);
		house.ConcurrencyStamp.ShouldNotBeNullOrWhiteSpace();
		input.Name += " revised";
		input.ConcurrencyStamp = house.ConcurrencyStamp;
		using var updated = await Client.PutAsJsonAsync($"{Root}/houses/{house.Id}", input, JsonOptions);
		updated.StatusCode.ShouldBe(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
		var revision = (await updated.Content.ReadFromJsonAsync<Result<HouseManagementHouseDto>>(JsonOptions))!.Data!;
		revision.Name.ShouldBe(input.Name);
		using var stale = await Client.PutAsJsonAsync($"{Root}/houses/{house.Id}", input, JsonOptions);
		stale.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		using var problem = JsonDocument.Parse(await stale.Content.ReadAsStringAsync());
		problem.RootElement.GetProperty("errorCode").GetString().ShouldBe(HouseManagementErrorCodes.StalePreview);
		problem.RootElement.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain("Refresh the list");
		using var deleted = await Client.DeleteAsync($"{Root}/houses/{house.Id}?academicMediumId={input.AcademicMediumId}&concurrencyStamp={Uri.EscapeDataString(revision.ConcurrencyStamp)}");
		deleted.StatusCode.ShouldBe(HttpStatusCode.OK, await deleted.Content.ReadAsStringAsync());
	}
}
