using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
	public async Task Should_Create_Employee_With_Staff_Add_And_No_Policy_Permissions()
	{
		using var client = CreatePermissionClient(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add);
		var input = EmployeeTestHelper.BuildPersonalInfoRequest("DelegatedCreator");
		input.PolicyOverride.ShouldBeNull();
		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(JsonSerializer.Serialize(input, JsonOptions)), "data");

		var response = await client.PostAsync($"{BaseUrl}/personal-info", content);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var created = await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions);
		created.ShouldNotBeNull();
		created.Id.ShouldNotBe(Guid.Empty);
		created.UserId.ShouldNotBeNull();
		created.FirstName.ShouldBe(input.FirstName);
		created.PolicyOverride.ShouldBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Staff_Creation_Without_Staff_Add_Even_With_Policy_Access()
	{
		using var client = CreatePermissionClient(
			ShikshaMitraSubmodulePermissions.HR.Policies.View,
			ShikshaMitraSubmodulePermissions.HR.Policies.Assign,
			ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact);
		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(JsonSerializer.Serialize(
			EmployeeTestHelper.BuildPersonalInfoRequest("NoStaffAdd"), JsonOptions)), "data");

		var response = await client.PostAsync($"{BaseUrl}/personal-info", content);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Explicit_Policy_Override_With_Staff_Add_Only()
	{
		using var client = CreatePermissionClient(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add);
		var input = EmployeeTestHelper.BuildPersonalInfoRequest("DeniedOverride");
		input.PolicyOverride = new PersonalInfoPolicyOverrideRequest
		{
			AcademicYearId = Factory.SeededAcademicYearId,
			PayrollPolicyId = Guid.NewGuid(),
			AttendancePolicyId = Guid.NewGuid(),
			LeavePolicyId = Guid.NewGuid(),
			EffectiveFrom = DateTime.UtcNow.Date,
			ConfirmationToken = "__employee_onboarding_default_policy_assignment__"
		};
		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(JsonSerializer.Serialize(input, JsonOptions)), "data");

		var response = await client.PostAsync($"{BaseUrl}/personal-info", content);

		response.IsSuccessStatusCode.ShouldBeFalse();
		(await response.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.EmployeePolicyAccessRequired);
	}

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
	public async Task Should_Reject_EmployeeGroup_Null_Payload_For_A_Restricted_Role()
	{
		var personalInfo = EmployeeTestHelper.BuildPersonalInfoRequest("NullEmployeeGroup");
		var payload = JsonNode.Parse(JsonSerializer.Serialize(personalInfo, JsonOptions))!.AsObject();
		payload.Remove("isTeaching");
		payload["employeeGroup"] = null;

		using var content = new MultipartFormDataContent();
		content.Add(
			new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
			"data");

		var response = await Client.PostAsync($"{BaseUrl}/personal-info", content);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		(await response.Content.ReadAsStringAsync())
			.ShouldContain(HRErrorCodes.EmployeeRoleGroupNotAllowed);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Create_When_Pan_And_Adhar_Are_Blank()
	{
		var personalInfo = EmployeeTestHelper.BuildPersonalInfoRequest("CreateBlankIdentity");
		personalInfo.PanNo = string.Empty;
		personalInfo.AdharNo = string.Empty;
		var jsonData = JsonSerializer.Serialize(personalInfo, JsonOptions);

		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(jsonData), "data");

		var response = await Client.PostAsync($"{BaseUrl}/personal-info", content);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		var payload = await response.Content.ReadAsStringAsync();
		payload.ShouldContain("PAN number is required");
		payload.ShouldContain("Aadhaar number is required");
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
		body.ShiftId.ShouldBe(created.ShiftId);
		body.Shift.ShouldNotBeNull();
		body.Shift!.Id.ShouldBe(created.ShiftId);
		body.Shift.Name.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Personal_Info()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"UpdatePI",
			isTeaching: false);

		var updateRequest = new PersonalInfoRequest
		{
			FirstName = TestDataHelper.UniqueName("Updated"),
			LastName = "UpdatedDoe",
			MobileNo = TestDataHelper.UniquePhone(),
			Doj = DateTime.UtcNow.AddMonths(-3),
			IsTeaching = false,
			RoleId = created.RoleId,
			ShiftId = EmployeeTestHelper.DefaultShiftId,
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
	public async Task Should_Update_Personal_Info_When_Pan_And_Adhar_Are_Omitted()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "UpdateNoIdentity");
		var updateRequest = new
		{
			FirstName = created.FirstName,
			LastName = "NoIdentityDoe",
			MobileNo = TestDataHelper.UniquePhone(),
			Doj = created.Doj,
			IsTeaching = created.IsTeaching,
			RoleId = created.RoleId,
			ShiftId = created.ShiftId,
			Email = created.Email,
			Gender = created.Gender,
			Department = "Identity Preserve"
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}/personal-info", updateRequest, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.LastName.ShouldBe("NoIdentityDoe");
		body.Department.ShouldBe("Identity Preserve");
		body.PanNo.ShouldBe(created.PanNo);
		body.AdharNo.ShouldBe(created.AdharNo);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Personal_Info_When_Pan_And_Adhar_Are_Blank()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "UpdateBlankIdentity");
		var updateRequest = EmployeeTestHelper.BuildPersonalInfoRequest("BlankIdentityUpdate");
		updateRequest.Email = created.Email;
		updateRequest.PanNo = string.Empty;
		updateRequest.AdharNo = string.Empty;
		updateRequest.Department = "Blank Identity Preserve";

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}/personal-info", updateRequest, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Department.ShouldBe("Blank Identity Preserve");
		body.PanNo.ShouldBe(created.PanNo);
		body.AdharNo.ShouldBe(created.AdharNo);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Personal_Info_Update_When_Pan_And_Adhar_Are_Invalid_Nonblank()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "UpdateInvalidIdentity");
		var updateRequest = EmployeeTestHelper.BuildPersonalInfoRequest("InvalidIdentityUpdate");
		updateRequest.Email = created.Email;
		updateRequest.PanNo = "BADPAN";
		updateRequest.AdharNo = "123";

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}/personal-info", updateRequest, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		var payload = await response.Content.ReadAsStringAsync();
		payload.ShouldContain("PAN must be in format AAAAA9999A.");
		payload.ShouldContain("Aadhaar number must be 12 digits.");
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
	public async Task Should_Create_Employee_Without_Organization_Assignment()
	{
		var request = EmployeeTestHelper.BuildPersonalInfoRequest("NoOrgCreate");
		var jsonData = JsonSerializer.Serialize(request, JsonOptions);
		jsonData.ShouldNotContain("OrganizationIds");
		jsonData.ShouldNotContain("organizationIds");

		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(jsonData), "data");

		var response = await Client.PostAsync($"{BaseUrl}/personal-info", content);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldNotBe(Guid.Empty);
		body.OrganizationIds.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Update_Personal_Info_Without_Organization_Assignment()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, "NoOrgUpdate");
		var updateRequest = EmployeeTestHelper.BuildPersonalInfoRequest("NoOrgUpdateRequest");
		updateRequest.Email = created.Email;
		var updatePayload = JsonSerializer.Serialize(updateRequest, JsonOptions);
		updatePayload.ShouldNotContain("OrganizationIds");
		updatePayload.ShouldNotContain("organizationIds");

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}/personal-info", updateRequest, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldBe(created.Id);
		body.OrganizationIds.ShouldContain(EmployeeTestHelper.DefaultMediumStageId);
	}

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
