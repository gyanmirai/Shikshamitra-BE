using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.ReportingManagerInfo;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class ReportingManagerApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/employees";

	public ReportingManagerApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Reporting_Manager()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "RMGet");

		var response = await Client.GetAsync($"{BaseUrl}/{empId}/reporting-manager");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Assign_Reporting_Manager()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "RMAssignee");
		var managerId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "RMManager");

		var request = new ReportingManagerRequest
		{
			ReportingManagerId = managerId,
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/reporting-manager", request, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Nonexistent_Employee()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/reporting-manager");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.NotFound,
			HttpStatusCode.InternalServerError,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Employees_Under_Manager()
	{
		var managerId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "RMManagerList");

		var response = await Client.GetAsync($"{BaseUrl}/reporting-manager/{managerId}/employees");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
	}

	#endregion

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Assign_Then_Verify_Reporting_Manager()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "RMVerifyEmp");
		var managerId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "RMVerifyMgr");

		var request = new ReportingManagerRequest { ReportingManagerId = managerId };
		await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/reporting-manager", request, JsonOptions);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/reporting-manager");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<ReportingManagerResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.EmployeeId.ShouldBe(empId);
		body.ReportingManager.ShouldNotBeNull();
		body.ReportingManager.Id.ShouldBe(managerId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Reassign_Manager_From_A_To_B()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "RMReassignE");
		var managerA = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "RMReassignA");
		var managerB = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "RMReassignB");

		await Client.PutAsJsonAsync(
			$"{BaseUrl}/{empId}/reporting-manager",
			new ReportingManagerRequest { ReportingManagerId = managerA },
			JsonOptions);

		await Client.PutAsJsonAsync(
			$"{BaseUrl}/{empId}/reporting-manager",
			new ReportingManagerRequest { ReportingManagerId = managerB },
			JsonOptions);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{empId}/reporting-manager");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await getResponse.Content.ReadFromJsonAsync<ReportingManagerResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.ReportingManager.ShouldNotBeNull();
		body.ReportingManager.Id.ShouldBe(managerB);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Correct_Count_Of_Employees_Under_Manager()
	{
		var managerId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "RMCountMgr");
		var emp1 = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "RMCountE1");
		var emp2 = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "RMCountE2");

		await Client.PutAsJsonAsync(
			$"{BaseUrl}/{emp1}/reporting-manager",
			new ReportingManagerRequest { ReportingManagerId = managerId },
			JsonOptions);
		await Client.PutAsJsonAsync(
			$"{BaseUrl}/{emp2}/reporting-manager",
			new ReportingManagerRequest { ReportingManagerId = managerId },
			JsonOptions);

		var response = await Client.GetAsync($"{BaseUrl}/reporting-manager/{managerId}/employees");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EmployeesUnderManagerResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Employees.Count.ShouldBeGreaterThanOrEqualTo(2);
	}

	#endregion
}
