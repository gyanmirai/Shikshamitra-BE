using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.WorkExperienceContracts;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class WorkExperienceApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/employees";

	public WorkExperienceApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Work_Experience()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "WExpGet");

		var response = await Client.GetAsync($"{BaseUrl}/{empId}/work-experience");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Work_Experience()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "WExpPut");

		var request = new List<WorkExperienceContract>
		{
			new()
			{
				MediumStageName = "Previous School",
				Designation = "Assistant Teacher",
				Department = "Mathematics",
				StartDate = DateTime.UtcNow.AddYears(-3),
				EndDate = DateTime.UtcNow.AddYears(-1),
				City = "Mumbai",
			}
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/work-experience", request, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	/// <summary>
	/// Mirrors staging client payloads: camelCase, nested geo, DateTime with timezone offset.
	/// Regression: PUT appeared to succeed but GET returned [] when JSON owned collection was not persisted.
	/// </summary>
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Persist_Work_Experience_When_Json_Uses_CamelCase_And_Offset_Dates()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "WExpStgJson");

		const string json =
			"""
			[{"mediumStageName":"La Net Solution","designation":"Software engineer trainee","department":"Frontend","country":{"name":"India","isoCode":"IN"},"state":{"name":"Gujarat","isoCode":"GJ"},"city":"Surat","startDate":"2026-04-01T00:00:00+05:30","endDate":"2026-04-10T00:00:00+05:30"}]
			""";

		var putResponse = await Client.PutAsync(
			$"{BaseUrl}/{empId}/work-experience",
			new StringContent(json.Trim(), Encoding.UTF8, "application/json"));
		putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var body = await GetAndReadAsync<List<WorkExperienceContract>>($"{BaseUrl}/{empId}/work-experience");
		body.Count.ShouldBe(1);
		body[0].MediumStageName.ShouldBe("La Net Solution");
		body[0].Designation.ShouldBe("Software engineer trainee");
		body[0].Department.ShouldBe("Frontend");
		body[0].City.ShouldBe("Surat");
		body[0].Country.ShouldNotBeNull();
		body[0].Country!.IsoCode.ShouldBe("IN");
		body[0].State.ShouldNotBeNull();
		body[0].State!.IsoCode.ShouldBe("GJ");
		body[0].StartDate.ShouldNotBeNull();
		body[0].EndDate.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Return_Data_On_Get_Immediately_After_Put()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "WExpImmediate");

		var request = new List<WorkExperienceContract>
		{
			new()
			{
				MediumStageName = "Immediate Co",
				Designation = "Engineer",
				StartDate = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc),
			}
		};

		var put = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/work-experience", request, JsonOptions);
		put.StatusCode.ShouldBe(HttpStatusCode.OK);

		var get = await Client.GetAsync($"{BaseUrl}/{empId}/work-experience");
		get.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await get.Content.ReadFromJsonAsync<List<WorkExperienceContract>>(JsonOptions);
		body.ShouldNotBeNull();
		body!.Count.ShouldBe(1);
		body[0].MediumStageName.ShouldBe("Immediate Co");
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Nonexistent_Employee()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/work-experience");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.NotFound,
			HttpStatusCode.InternalServerError,
			HttpStatusCode.Forbidden);
	}

	#endregion

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Update_Multiple_Experiences()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "WExpMulti");

		var request = new List<WorkExperienceContract>
		{
			new()
			{
				MediumStageName = "School A",
				Designation = "Teacher",
				Department = "Science",
				StartDate = DateTime.UtcNow.AddYears(-5),
				EndDate = DateTime.UtcNow.AddYears(-3),
			},
			new()
			{
				MediumStageName = "School B",
				Designation = "Senior Teacher",
				Department = "Physics",
				StartDate = DateTime.UtcNow.AddYears(-3),
				EndDate = DateTime.UtcNow.AddMonths(-6),
			}
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/work-experience", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/work-experience");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<List<WorkExperienceContract>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Count.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Clear_Experiences_With_Empty_List()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "WExpClear");

		var populateRequest = new List<WorkExperienceContract>
		{
			new()
			{
				MediumStageName = "School To Clear",
				Designation = "Teacher",
				Department = "English",
				StartDate = DateTime.UtcNow.AddYears(-2),
				EndDate = DateTime.UtcNow.AddMonths(-1),
			}
		};
		await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/work-experience", populateRequest, JsonOptions);

		var clearResponse = await Client.PutAsJsonAsync(
			$"{BaseUrl}/{empId}/work-experience",
			new List<WorkExperienceContract>(),
			JsonOptions);
		clearResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/work-experience");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<List<WorkExperienceContract>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Count.ShouldBe(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Update_With_Country_State_Info()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "WExpGeo");

		var request = new List<WorkExperienceContract>
		{
			new()
			{
				MediumStageName = "International School",
				Designation = "Principal",
				Department = "Administration",
				Country = new WorkExperienceContracts.CountryInfo { Name = "India", IsoCode = "IN" },
				State = new WorkExperienceContracts.StateInfo { Name = "Maharashtra", IsoCode = "MH" },
				City = "Pune",
				StartDate = DateTime.UtcNow.AddYears(-4),
				EndDate = DateTime.UtcNow.AddYears(-1),
			}
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/work-experience", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/work-experience");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<List<WorkExperienceContract>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Count.ShouldBeGreaterThanOrEqualTo(1);
		body[0].City.ShouldBe("Pune");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Update_Then_Verify_Count_And_Fields()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "WExpFields");

		var request = new List<WorkExperienceContract>
		{
			new()
			{
				MediumStageName = "Alpha School",
				Designation = "Vice Principal",
				Department = "Maths",
				StartDate = DateTime.UtcNow.AddYears(-6),
				EndDate = DateTime.UtcNow.AddYears(-4),
			},
			new()
			{
				MediumStageName = "Beta Academy",
				Designation = "HOD",
				Department = "Physics",
				StartDate = DateTime.UtcNow.AddYears(-4),
				EndDate = DateTime.UtcNow.AddYears(-2),
			},
			new()
			{
				MediumStageName = "Gamma Institute",
				Designation = "Teacher",
				Department = "Chemistry",
				StartDate = DateTime.UtcNow.AddYears(-2),
				EndDate = DateTime.UtcNow.AddMonths(-3),
			},
		};

		await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/work-experience", request, JsonOptions);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/work-experience");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<List<WorkExperienceContract>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Count.ShouldBe(3);
		body.ShouldContain(e => e.MediumStageName == "Alpha School");
		body.ShouldContain(e => e.MediumStageName == "Beta Academy");
		body.ShouldContain(e => e.MediumStageName == "Gamma Institute");
	}

	#endregion
}
