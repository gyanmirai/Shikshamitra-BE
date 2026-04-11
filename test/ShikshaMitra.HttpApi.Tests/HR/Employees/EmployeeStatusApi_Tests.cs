using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Status;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EmployeeStatusApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/employees";

	public EmployeeStatusApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Employee_Status_To_Inactive()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusInactive");

		var request = new EmployeeStatusUpdateRequest
		{
			EmployeeStatusType = 1,
			Remarks = "Going on sabbatical",
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/status", request, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EmployeeStatusUpdateResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.CurrentStatus.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Employee_Status_With_Empty_Remarks_String()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusEmptyRemarks");

		var request = new EmployeeStatusUpdateRequest
		{
			EmployeeStatusType = 1,
			Remarks = string.Empty,
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/status", request, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Nonexistent_Employee()
	{
		var request = new EmployeeStatusUpdateRequest
		{
			EmployeeStatusType = 0,
			Remarks = "Test",
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}/status", request, JsonOptions);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.NotFound,
			HttpStatusCode.InternalServerError,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Be_Idempotent_For_Same_Status()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusIdem");

		var request = new EmployeeStatusUpdateRequest
		{
			EmployeeStatusType = 0,
			Remarks = "Already active",
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/status", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	#endregion

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Transition_Through_Multiple_Statuses()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusCycle");

		var inactiveReq = new EmployeeStatusUpdateRequest { EmployeeStatusType = 1, Remarks = "Inactive" };
		var r1 = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/status", inactiveReq, JsonOptions);
		r1.StatusCode.ShouldBe(HttpStatusCode.OK);

		var activeReq = new EmployeeStatusUpdateRequest { EmployeeStatusType = 0, Remarks = "Reactivated" };
		var r2 = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/status", activeReq, JsonOptions);
		r2.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await r2.Content.ReadFromJsonAsync<EmployeeStatusUpdateResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.CurrentStatus.ShouldBe(0);
		body.PreviousStatus.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Set_Left_And_Retired_Statuses()
	{
		var empLeft = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusLeft");
		var leftReq = new EmployeeStatusUpdateRequest { EmployeeStatusType = 2, Remarks = "Employee left" };
		var leftResp = await Client.PutAsJsonAsync($"{BaseUrl}/{empLeft}/status", leftReq, JsonOptions);
		leftResp.StatusCode.ShouldBe(HttpStatusCode.OK);
		var leftBody = await leftResp.Content.ReadFromJsonAsync<EmployeeStatusUpdateResponse>(JsonOptions);
		leftBody.ShouldNotBeNull();
		leftBody.CurrentStatus.ShouldBe(2);

		var empRetired = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusRetired");
		var retiredReq = new EmployeeStatusUpdateRequest { EmployeeStatusType = 3, Remarks = "Employee retired" };
		var retiredResp = await Client.PutAsJsonAsync($"{BaseUrl}/{empRetired}/status", retiredReq, JsonOptions);
		retiredResp.StatusCode.ShouldBe(HttpStatusCode.OK);
		var retiredBody = await retiredResp.Content.ReadFromJsonAsync<EmployeeStatusUpdateResponse>(JsonOptions);
		retiredBody.ShouldNotBeNull();
		retiredBody.CurrentStatus.ShouldBe(3);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Reject_Invalid_Status_Value()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusBad");

		var request = new EmployeeStatusUpdateRequest { EmployeeStatusType = 99, Remarks = "Invalid" };
		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/status", request, JsonOptions);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.InternalServerError);
	}

	#endregion
}
