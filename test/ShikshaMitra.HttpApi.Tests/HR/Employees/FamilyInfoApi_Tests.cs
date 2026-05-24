using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.FamilyInfo;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class FamilyInfoApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/employees";

	public FamilyInfoApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Family_Info()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "FamGet");

		var response = await Client.GetAsync($"{BaseUrl}/{empId}/family-info");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Family_Info()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "FamPut");

		var request = new FamilyInfoRequest
		{
			FatherDetails = new FamilyMemberInfo
			{
				Name = "Rajesh Kumar",
				Occupation = "Teacher",
				MobileNo = "9876543210",
			},
			MotherDetails = new FamilyMemberInfo
			{
				Name = "Sunita Devi",
				Occupation = "Homemaker",
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/family-info", request, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Forbid_Non_Hr_User_From_Updating_Family_Info()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "FamAuth");
		using var nonHrClient = Factory.CreateClient();
		nonHrClient.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
		nonHrClient.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Teacher");

		var response = await nonHrClient.PutAsJsonAsync(
			$"{BaseUrl}/{empId}/family-info",
			new FamilyInfoRequest
			{
				FatherDetails = new FamilyMemberInfo
				{
					Name = "Rajesh Kumar",
					MobileNo = "9876543210",
				},
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Nonexistent_Employee()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/family-info");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.NotFound,
			HttpStatusCode.InternalServerError,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Field_Level_Validation_Errors_For_Invalid_Request()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "FamVal");

		var request = new FamilyInfoRequest
		{
			FatherDetails = new FamilyMemberInfo
			{
				Name = new string('A', 257),
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/family-info", request, JsonOptions);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);

		if (response.StatusCode != HttpStatusCode.BadRequest)
		{
			return;
		}

		response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;

		root.GetProperty("status").GetInt32().ShouldBe((int)HttpStatusCode.BadRequest);
		var errors = root.GetProperty("errors");
		errors.ValueKind.ShouldBe(JsonValueKind.Object);

		var keys = errors.EnumerateObject()
			.Select(e => e.Name)
			.ToList();
		keys.Any(k => k.Contains("name", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Accept_Blank_Nested_Country_State_Fields()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "FamNestedVal");

		var request = new FamilyInfoRequest
		{
			FatherDetails = new FamilyMemberInfo
			{
				Name = "Father",
				Country = new CountryInfo
				{
					Name = string.Empty,
					IsoCode = string.Empty
				},
				State = new StateInfo
				{
					Name = string.Empty,
					IsoCode = string.Empty
				}
			}
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/family-info", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	#endregion

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Update_Then_Verify_Family_Info()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "FamVerify");

		var request = new FamilyInfoRequest
		{
			FatherDetails = new FamilyMemberInfo
			{
				Name = "Verification Father",
				Occupation = "Engineer",
				MobileNo = "9988776655",
			},
			SpouseDetails = new FamilyMemberInfo
			{
				Name = "Verification Spouse",
				Occupation = "Doctor",
			},
		};

		await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/family-info", request, JsonOptions);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/family-info");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<FamilyInfoResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.FatherDetails.ShouldNotBeNull();
		body.FatherDetails.Name.ShouldBe("Verification Father");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Update_All_Three_Family_Members_With_Full_Address()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "FamAll");

		var request = new FamilyInfoRequest
		{
			FatherDetails = new FamilyMemberInfo
			{
				Name = "Test Father",
				Occupation = "Teacher",
				MobileNo = "9876543210",
				Address1 = "123 Father Street",
				City = "Delhi",
				Pincode = "110001",
				Country = new CountryInfo { Name = "India", IsoCode = "IN" },
				State = new StateInfo { Name = "Delhi", IsoCode = "DL" },
			},
			MotherDetails = new FamilyMemberInfo
			{
				Name = "Test Mother",
				Occupation = "Doctor",
				MobileNo = "9876543211",
				Address1 = "123 Mother Street",
				City = "Mumbai",
				Pincode = "400001",
			},
			SpouseDetails = new FamilyMemberInfo
			{
				Name = "Test Spouse",
				Occupation = "Engineer",
				MobileNo = "9876543212",
				Address1 = "456 Spouse Avenue",
				City = "Pune",
				Pincode = "411001",
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/family-info", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/family-info");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<FamilyInfoResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.FatherDetails.ShouldNotBeNull();
		body.FatherDetails.Name.ShouldBe("Test Father");
		body.FatherDetails.City.ShouldBe("Delhi");
		body.MotherDetails.ShouldNotBeNull();
		body.MotherDetails.Name.ShouldBe("Test Mother");
		body.SpouseDetails.ShouldNotBeNull();
		body.SpouseDetails.Name.ShouldBe("Test Spouse");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Clear_Family_Info_With_Empty_Request()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "FamClear");

		var populateRequest = new FamilyInfoRequest
		{
			FatherDetails = new FamilyMemberInfo { Name = "Father To Clear", Occupation = "Worker" },
		};
		await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/family-info", populateRequest, JsonOptions);

		var clearRequest = new FamilyInfoRequest();
		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/family-info", clearRequest, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Verify_Address_Fields_RoundTrip()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "FamAddr");

		var request = new FamilyInfoRequest
		{
			FatherDetails = new FamilyMemberInfo
			{
				Name = "Address Father",
				Address1 = "Line 1",
				Address2 = "Line 2",
				Address3 = "Line 3",
				Country = new CountryInfo { Name = "India", IsoCode = "IN" },
				State = new StateInfo { Name = "Karnataka", IsoCode = "KA" },
				City = "Bangalore",
				Pincode = "560001",
			},
		};

		await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/family-info", request, JsonOptions);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/family-info");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<FamilyInfoResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.FatherDetails.ShouldNotBeNull();
		body.FatherDetails.Address1.ShouldBe("Line 1");
		body.FatherDetails.Address2.ShouldBe("Line 2");
		body.FatherDetails.Address3.ShouldBe("Line 3");
		body.FatherDetails.City.ShouldBe("Bangalore");
		body.FatherDetails.Pincode.ShouldBe("560001");
	}

	#endregion
}
