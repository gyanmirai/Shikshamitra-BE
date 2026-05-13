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

/// <summary>
/// API-level tests for the employee additional details endpoints.
/// Validates HTTP pipeline, auth, and response contracts for GET/PUT /api/employees/{id}/additional-info.
/// </summary>
[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class AdditionalInfoApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/employees";

	public AdditionalInfoApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Additional_Info_After_Upsert()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoGet");

		var seed = new AdditionalInfoContract { SecondLanguage = "Hindi" };
		var putResponse = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", seed, JsonOptions);
		putResponse.EnsureSuccessStatusCode();

		var response = await Client.GetAsync($"{BaseUrl}/{empId}/additional-info");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<AdditionalInfoContract>(JsonOptions);
		body.ShouldNotBeNull();
		body.SecondLanguage.ShouldBe("Hindi");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Empty_Additional_Info_For_Employee_Without_Record()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoEmpty");

		var response = await Client.GetAsync($"{BaseUrl}/{empId}/additional-info");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<AdditionalInfoContract>(JsonOptions);
		body.ShouldNotBeNull();
		body.BplCardNo.ShouldBeNull();
		body.SecondLanguage.ShouldBeNull();
		body.PersonWithDisability.ShouldBeFalse();
		body.IsLibraryMember.ShouldBeFalse();
		body.IsTransportMember.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Additional_Info_Via_Put()
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

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Existing_Additional_Info()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoUpd");

		var initial = new AdditionalInfoContract
		{
			BplCardNo = "BPL-INIT",
			SecondLanguage = "Hindi",
			IsLibraryMember = false,
		};
		var createResponse = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", initial, JsonOptions);
		createResponse.EnsureSuccessStatusCode();

		var updated = new AdditionalInfoContract
		{
			BplCardNo = "BPL-UPDATED",
			SecondLanguage = "Tamil",
			IsLibraryMember = true,
			IsTransportMember = true,
			PersonWithDisability = true,
		};
		var updateResponse = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", updated, JsonOptions);
		updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/additional-info");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<AdditionalInfoContract>(JsonOptions);
		body.ShouldNotBeNull();
		body.BplCardNo.ShouldBe("BPL-UPDATED");
		body.SecondLanguage.ShouldBe("Tamil");
		body.IsLibraryMember.ShouldBeTrue();
		body.IsTransportMember.ShouldBeTrue();
		body.PersonWithDisability.ShouldBeTrue();
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Get_Nonexistent_Employee()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/additional-info");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.NotFound,
			HttpStatusCode.InternalServerError,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Update_Nonexistent_Employee()
	{
		var request = new AdditionalInfoContract
		{
			SecondLanguage = "Hindi",
		};

		var response = await Client.PutAsJsonAsync(
			$"{BaseUrl}/{Guid.NewGuid()}/additional-info", request, JsonOptions);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.NotFound,
			HttpStatusCode.InternalServerError,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Handle_Put_With_All_Default_Values()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoDef");

		var request = new AdditionalInfoContract();

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/additional-info");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<AdditionalInfoContract>(JsonOptions);
		body.ShouldNotBeNull();
		body.BplCardNo.ShouldBeNull();
		body.SecondLanguage.ShouldBeNull();
		body.PersonWithDisability.ShouldBeFalse();
		body.IsLibraryMember.ShouldBeFalse();
		body.IsTransportMember.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Be_Idempotent_On_Repeated_Put()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoIdem");

		var request = new AdditionalInfoContract
		{
			BplCardNo = "BPL-IDEM",
			PersonWithDisability = true,
			SecondLanguage = "Kannada",
			IsLibraryMember = true,
			IsTransportMember = false,
		};

		var response1 = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", request, JsonOptions);
		response1.StatusCode.ShouldBe(HttpStatusCode.OK);

		var response2 = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", request, JsonOptions);
		response2.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/additional-info");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<AdditionalInfoContract>(JsonOptions);
		body.ShouldNotBeNull();
		body.BplCardNo.ShouldBe("BPL-IDEM");
		body.PersonWithDisability.ShouldBeTrue();
		body.SecondLanguage.ShouldBe("Kannada");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Authenticated_Request_Should_Not_Return_401()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoAuth");

		var seed = new AdditionalInfoContract { SecondLanguage = "Hindi" };
		await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", seed, JsonOptions);

		var response = await Client.GetAsync($"{BaseUrl}/{empId}/additional-info");

		response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
	}

	#endregion

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_RoundTrip_All_Fields()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoRT");

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
		body.SecondLanguage.ShouldBe("Sanskrit");
		body.IsLibraryMember.ShouldBeFalse();
		body.IsTransportMember.ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Clear_All_Fields_With_Null_Values()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoClear");

		var populateRequest = new AdditionalInfoContract
		{
			BplCardNo = "BPL-CLEAR",
			PersonWithDisability = true,
			SecondLanguage = "Marathi",
			IsLibraryMember = true,
			IsTransportMember = true,
		};
		await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", populateRequest, JsonOptions);

		var clearRequest = new AdditionalInfoContract
		{
			BplCardNo = null,
			PersonWithDisability = false,
			SecondLanguage = null,
			IsLibraryMember = false,
			IsTransportMember = false,
		};
		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", clearRequest, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/additional-info");
		var body = await getResponse.Content.ReadFromJsonAsync<AdditionalInfoContract>(JsonOptions);
		body.ShouldNotBeNull();
		body.BplCardNo.ShouldBeNull();
		body.SecondLanguage.ShouldBeNull();
		body.PersonWithDisability.ShouldBeFalse();
		body.IsLibraryMember.ShouldBeFalse();
		body.IsTransportMember.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_RoundTrip_All_Boolean_Fields_As_True()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoBools");

		var request = new AdditionalInfoContract
		{
			BplCardNo = "BPL-BOOLS",
			PersonWithDisability = true,
			SecondLanguage = "French",
			IsLibraryMember = true,
			IsTransportMember = true,
		};
		await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", request, JsonOptions);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/additional-info");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<AdditionalInfoContract>(JsonOptions);
		body.ShouldNotBeNull();
		body.PersonWithDisability.ShouldBeTrue();
		body.IsLibraryMember.ShouldBeTrue();
		body.IsTransportMember.ShouldBeTrue();
		body.SecondLanguage.ShouldBe("French");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_RoundTrip_All_Boolean_Fields_As_False()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoFalse");

		var setTrue = new AdditionalInfoContract
		{
			PersonWithDisability = true,
			IsLibraryMember = true,
			IsTransportMember = true,
		};
		await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", setTrue, JsonOptions);

		var setFalse = new AdditionalInfoContract
		{
			PersonWithDisability = false,
			IsLibraryMember = false,
			IsTransportMember = false,
		};
		await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", setFalse, JsonOptions);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/additional-info");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<AdditionalInfoContract>(JsonOptions);
		body.ShouldNotBeNull();
		body.PersonWithDisability.ShouldBeFalse();
		body.IsLibraryMember.ShouldBeFalse();
		body.IsTransportMember.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Handle_Only_BplCardNo_Field()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoBpl");

		var request = new AdditionalInfoContract
		{
			BplCardNo = "BPL-ONLY-FIELD",
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/additional-info");
		var body = await getResponse.Content.ReadFromJsonAsync<AdditionalInfoContract>(JsonOptions);
		body.ShouldNotBeNull();
		body.BplCardNo.ShouldBe("BPL-ONLY-FIELD");
		body.PersonWithDisability.ShouldBeFalse();
		body.SecondLanguage.ShouldBeNull();
		body.IsLibraryMember.ShouldBeFalse();
		body.IsTransportMember.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Handle_Only_SecondLanguage_Field()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoLang");

		var request = new AdditionalInfoContract
		{
			SecondLanguage = "Telugu",
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/additional-info");
		var body = await getResponse.Content.ReadFromJsonAsync<AdditionalInfoContract>(JsonOptions);
		body.ShouldNotBeNull();
		body.SecondLanguage.ShouldBe("Telugu");
		body.BplCardNo.ShouldBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Overwrite_Previous_Values_Completely()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoOver");

		var first = new AdditionalInfoContract
		{
			BplCardNo = "BPL-FIRST",
			PersonWithDisability = true,
			SecondLanguage = "Hindi",
			IsLibraryMember = true,
			IsTransportMember = true,
		};
		await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", first, JsonOptions);

		var second = new AdditionalInfoContract
		{
			BplCardNo = "BPL-SECOND",
			PersonWithDisability = false,
			SecondLanguage = "Bengali",
			IsLibraryMember = false,
			IsTransportMember = false,
		};
		await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", second, JsonOptions);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/additional-info");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<AdditionalInfoContract>(JsonOptions);
		body.ShouldNotBeNull();
		body.BplCardNo.ShouldBe("BPL-SECOND");
		body.PersonWithDisability.ShouldBeFalse();
		body.SecondLanguage.ShouldBe("Bengali");
		body.IsLibraryMember.ShouldBeFalse();
		body.IsTransportMember.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Trim_Whitespace_In_String_Fields()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoTrim");

		var request = new AdditionalInfoContract
		{
			BplCardNo = "  BPL-TRIM  ",
			SecondLanguage = "  Gujarati  ",
		};

		await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/additional-info", request, JsonOptions);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/additional-info");
		var body = await getResponse.Content.ReadFromJsonAsync<AdditionalInfoContract>(JsonOptions);
		body.ShouldNotBeNull();
		body.BplCardNo.ShouldBe("BPL-TRIM");
		body.SecondLanguage.ShouldBe("Gujarati");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Isolate_Additional_Info_Between_Employees()
	{
		var empId1 = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoIso1");
		var empId2 = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AddInfoIso2");

		var request1 = new AdditionalInfoContract
		{
			BplCardNo = "BPL-EMP1",
			SecondLanguage = "Hindi",
			IsLibraryMember = true,
		};
		var request2 = new AdditionalInfoContract
		{
			BplCardNo = "BPL-EMP2",
			SecondLanguage = "Urdu",
			IsTransportMember = true,
		};

		await Client.PutAsJsonAsync($"{BaseUrl}/{empId1}/additional-info", request1, JsonOptions);
		await Client.PutAsJsonAsync($"{BaseUrl}/{empId2}/additional-info", request2, JsonOptions);

		var body1 = await (await Client.GetAsync($"{BaseUrl}/{empId1}/additional-info"))
			.Content.ReadFromJsonAsync<AdditionalInfoContract>(JsonOptions);
		var body2 = await (await Client.GetAsync($"{BaseUrl}/{empId2}/additional-info"))
			.Content.ReadFromJsonAsync<AdditionalInfoContract>(JsonOptions);

		body1.ShouldNotBeNull();
		body1.BplCardNo.ShouldBe("BPL-EMP1");
		body1.SecondLanguage.ShouldBe("Hindi");
		body1.IsLibraryMember.ShouldBeTrue();
		body1.IsTransportMember.ShouldBeFalse();

		body2.ShouldNotBeNull();
		body2.BplCardNo.ShouldBe("BPL-EMP2");
		body2.SecondLanguage.ShouldBe("Urdu");
		body2.IsLibraryMember.ShouldBeFalse();
		body2.IsTransportMember.ShouldBeTrue();
	}

	#endregion
}
