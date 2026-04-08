using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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

	#endregion
}
