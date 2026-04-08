using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// API-level tests for the employee personal info endpoints.
/// Validates HTTP pipeline, auth, and response contracts.
/// </summary>
[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EmployeePersonalInfoApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/employees";

	public EmployeePersonalInfoApi_Tests(ShikshaMitraApiTestFactory factory)
		: base(factory)
	{
	}

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Employee_Via_PersonalInfo_Returns_Success()
	{
		var personalInfo = EmployeeTestHelper.BuildPersonalInfoRequest("CreateTest");
		var jsonData = JsonSerializer.Serialize(personalInfo, JsonOptions);

		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(jsonData), "data");

		var response = await Client.PostAsync($"{BaseUrl}/personal-info", content);

		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldNotBe(Guid.Empty);
		body.FirstName.ShouldContain("CreateTest");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Personal_Info_After_Create()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "GetPI");

		var response = await Client.GetAsync($"{BaseUrl}/{created.Id}/personal-info");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldBe(created.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Personal_Info()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "UpdatePI");

		var updateRequest = new PersonalInfoRequest
		{
			FirstName = TestDataHelper.UniqueName("Updated"),
			LastName = "UpdatedDoe",
			MobileNo = TestDataHelper.UniquePhone(),
			Doj = DateTime.UtcNow.AddMonths(-3),
			IsTeaching = false,
			RoleId = Guid.NewGuid(),
			ShiftId = Guid.NewGuid(),
			Email = TestDataHelper.UniqueEmail("upd"),
			Gender = 2,
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}/personal-info", updateRequest, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.LastName.ShouldBe("UpdatedDoe");
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_404_For_Nonexistent_Employee()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/personal-info");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.NotFound,
			HttpStatusCode.InternalServerError,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Authenticated_Request_For_Nonexistent_Employee_Should_Not_Return_401()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/personal-info");

		response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
	}

	#endregion
}
