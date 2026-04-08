using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.FamilyInfo;
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

	#endregion
}
