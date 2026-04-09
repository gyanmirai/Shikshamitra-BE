using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
	public async Task Should_Create_Employee_With_Profile_Picture()
	{
		var result = await EmployeeTestHelper.CreateEmployeeWithPictureAsync(Client, "WithPic");

		result.ShouldNotBeNull();
		result.Id.ShouldNotBe(Guid.Empty);
		result.FirstName.ShouldContain("WithPic");
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
			RoleId = EmployeeTestHelper.DefaultRoleId,
			ShiftId = EmployeeTestHelper.DefaultShiftId,
			Email = TestDataHelper.UniqueEmail("upd"),
			Gender = 2,
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}/personal-info", updateRequest, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.LastName.ShouldBe("UpdatedDoe");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Employee_With_All_Optional_Fields()
	{
		var personalInfo = EmployeeTestHelper.BuildFullPersonalInfoRequest("AllOpts");
		var jsonData = JsonSerializer.Serialize(personalInfo, JsonOptions);

		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(jsonData), "data");

		var response = await Client.PostAsync($"{BaseUrl}/personal-info", content);

		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldNotBe(Guid.Empty);
		body.MiddleName.ShouldBe("MiddleTest");
		body.Address1.ShouldBe("123 Test Street");
		body.City.ShouldBe("Mumbai");
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_404_For_Nonexistent_Employee()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/personal-info");

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Authenticated_Request_For_Nonexistent_Employee_Should_Not_Return_401()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/personal-info");

		response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Fail_With_Invalid_Json_In_Data_Field()
	{
		using var content = new MultipartFormDataContent();
		content.Add(new StringContent("{ not valid json !!!"), "data");

		var response = await Client.PostAsync($"{BaseUrl}/personal-info", content);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Fail_With_Missing_Required_Fields()
	{
		var incompleteRequest = new PersonalInfoRequest
		{
			FirstName = "",
			LastName = "",
			MobileNo = "",
			Doj = default,
		};
		var jsonData = JsonSerializer.Serialize(incompleteRequest, JsonOptions);

		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(jsonData), "data");

		var response = await Client.PostAsync($"{BaseUrl}/personal-info", content);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Fail_With_Invalid_Phone_Format()
	{
		var request = EmployeeTestHelper.BuildPersonalInfoRequest("BadPhone");
		request.MobileNo = "12345";

		var jsonData = JsonSerializer.Serialize(request, JsonOptions);

		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(jsonData), "data");

		var response = await Client.PostAsync($"{BaseUrl}/personal-info", content);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Fail_With_Duplicate_Secondary_Mobile()
	{
		var request = EmployeeTestHelper.BuildPersonalInfoRequest("DupMobile");
		request.SecondaryMobileNo = request.MobileNo;

		var jsonData = JsonSerializer.Serialize(request, JsonOptions);

		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(jsonData), "data");

		var response = await Client.PostAsync($"{BaseUrl}/personal-info", content);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.InternalServerError);
	}

	#endregion

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Update_Then_Verify_All_Fields_RoundTrip()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "RoundTrip");

		var updateRequest = EmployeeTestHelper.BuildFullPersonalInfoRequest("RTUpdate");
		var response = await Client.PutAsJsonAsync(
			$"{BaseUrl}/{created.Id}/personal-info", updateRequest, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{created.Id}/personal-info");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions);

		body.ShouldNotBeNull();
		body.LastName.ShouldBe("FullDoe");
		body.MiddleName.ShouldBe("MiddleTest");
		body.Gender.ShouldBe(1);
		body.Religion.ShouldBe("Hindu");
		body.MaritalStatus.ShouldBe(1);
		body.BloodGroup.ShouldBe(3);
		body.Address1.ShouldBe("123 Test Street");
		body.City.ShouldBe("Mumbai");
		body.PinCode.ShouldBe("400001");
	}

	#endregion
}
