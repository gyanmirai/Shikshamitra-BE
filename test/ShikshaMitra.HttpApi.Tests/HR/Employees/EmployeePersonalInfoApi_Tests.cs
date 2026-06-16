using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.Infrastructure.Auth;
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
		body.PanNo.ShouldBe(personalInfo.PanNo);
		body.AdharNo.ShouldBe(personalInfo.AdharNo);
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
			OrganizationIds = [EmployeeTestHelper.DefaultMediumStageId],
			Email = created.Email,
			PanNo = TestDataHelper.UniquePan(),
			AdharNo = TestDataHelper.UniqueAdhar(),
			Gender = 2,
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}/personal-info", updateRequest, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.LastName.ShouldBe("UpdatedDoe");
		body.Email.ShouldBe(created.Email);
		body.PanNo.ShouldBe(updateRequest.PanNo);
		body.AdharNo.ShouldBe(updateRequest.AdharNo);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Personal_Info_Email_Change()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "RejectEmailChange");
		var originalEmail = created.Email;
		originalEmail.ShouldNotBeNull();
		var updateRequest = EmployeeTestHelper.BuildPersonalInfoRequest("RejectEmailChangeUpdate");
		var requestedEmail = TestDataHelper.UniqueEmail("upd-email-change");
		updateRequest.Email = requestedEmail;

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}/personal-info", updateRequest, JsonOptions);

		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		var responseBody = await response.Content.ReadAsStringAsync();
		responseBody.ShouldContain(HRErrorCodes.EmployeeEmailCannotBeChanged);
		responseBody.ShouldContain("Employee email cannot be changed after employee creation.");
		responseBody.ShouldNotContain(originalEmail);
		responseBody.ShouldNotContain(requestedEmail);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Personal_Info_With_Employee_Edit_Only()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "UpdateEditOnly");
		using var client = CreatePermissionClient(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit);
		var updateRequest = EmployeeTestHelper.BuildPersonalInfoRequest("EditOnly");
		updateRequest.Email = created.Email;

		var response = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}/personal-info", updateRequest, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Personal_Info_Update_Without_Employee_Edit()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "UpdateNoEdit");
		using var client = CreatePermissionClient(
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_PayrollStructure.Add);
		var updateRequest = EmployeeTestHelper.BuildPersonalInfoRequest("NoEdit");

		var response = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}/personal-info", updateRequest, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
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
		body.PanNo.ShouldBe(personalInfo.PanNo);
		body.AdharNo.ShouldBe(personalInfo.AdharNo);
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

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Fail_With_Invalid_Pan_And_Adhar()
	{
		var request = EmployeeTestHelper.BuildPersonalInfoRequest("BadIdentity");
		request.PanNo = "BADPAN";
		request.AdharNo = "123";

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
	public async Task Should_Fail_With_Duplicate_Pan()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "DupPan");
		var request = EmployeeTestHelper.BuildPersonalInfoRequest("DupPanTwo");
		request.PanNo = created.PanNo;

		var jsonData = JsonSerializer.Serialize(request, JsonOptions);

		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(jsonData), "data");

		var response = await Client.PostAsync($"{BaseUrl}/personal-info", content);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
		payload.GetProperty("errorCode").GetString().ShouldBe(HRErrorCodes.EmployeePanAlreadyExists);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Fail_With_Duplicate_Adhar()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "DupAdhar");
		var request = EmployeeTestHelper.BuildPersonalInfoRequest("DupAdharTwo");
		request.AdharNo = created.AdharNo;

		var jsonData = JsonSerializer.Serialize(request, JsonOptions);

		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(jsonData), "data");

		var response = await Client.PostAsync($"{BaseUrl}/personal-info", content);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
		payload.GetProperty("errorCode").GetString().ShouldBe(HRErrorCodes.EmployeeAdharAlreadyExists);
	}

	#endregion

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Update_Then_Verify_All_Fields_RoundTrip()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "RoundTrip");

		var updateRequest = EmployeeTestHelper.BuildFullPersonalInfoRequest("RTUpdate");
		updateRequest.Email = created.Email;
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
		body.PanNo.ShouldBe(updateRequest.PanNo);
		body.AdharNo.ShouldBe(updateRequest.AdharNo);
	}

	#endregion

	private HttpClient CreatePermissionClient(params string[] permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			permissions.Length == 0 ? "__none__" : string.Join(",", permissions));
		return client;
	}
}
