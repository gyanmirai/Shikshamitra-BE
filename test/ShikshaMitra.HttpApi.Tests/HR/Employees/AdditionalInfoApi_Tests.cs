using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.AdditionalInfo;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class AdditionalInfoApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/employees";

	public AdditionalInfoApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Additional_Info()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoGet");

		var response = await Client.GetAsync($"{BaseUrl}/{empId}/additional-info");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Additional_Info()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoPut");

		var request = new AdditionalInfoContract
		{
			BplCardNo = "BPL-12345",
			PersonWithDisability = false,
			SecondLanguage = "Hindi",
			IsLibraryMember = true,
			IsTransportMember = false,
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", request, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Nonexistent_Employee()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/additional-info");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.NotFound,
			HttpStatusCode.InternalServerError,
			HttpStatusCode.Forbidden);
	}

	#endregion

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Update_Then_Verify_Additional_Info()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoVerify");

		var request = new AdditionalInfoContract
		{
			BplCardNo = "BPL-VERIFY",
			PersonWithDisability = true,
			SecondLanguage = "Sanskrit",
			IsLibraryMember = false,
			IsTransportMember = true,
		};

		await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", request, JsonOptions);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/additional-info");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<AdditionalInfoContract>(JsonOptions);
		body.ShouldNotBeNull();
		body.BplCardNo.ShouldBe("BPL-VERIFY");
		body.PersonWithDisability.ShouldBeTrue();
	}

	#endregion
}
