using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.ReportingManagerInfo;
using ShikshaMitra.HR.Employees.Status;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.SalaryStructure;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EmployeeStatusApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/employees";
	private static readonly DateOnly TestEffectiveDate = new(2026, 5, 31);

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
			EffectiveDate = TestEffectiveDate
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/status", request, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EmployeeStatusUpdateResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.CurrentStatus.ShouldBe(1);
		body.EffectiveDate.ShouldBe(TestEffectiveDate);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Non_Active_Status_Without_Effective_Date()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusNoEffectiveDate");

		var response = await Client.PutAsJsonAsync(
			$"{BaseUrl}/{empId}/status",
			new EmployeeStatusUpdateRequest
			{
				EmployeeStatusType = 1,
				Remarks = "Missing effective date"
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Block_Activating_Employee_Without_Salary_Structure()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusActiveNoPayroll");
		var toInactive = new EmployeeStatusUpdateRequest
		{
			EmployeeStatusType = 1,
			Remarks = "Seed inactive first",
			EffectiveDate = TestEffectiveDate
		};
		(await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/status", toInactive, JsonOptions))
			.StatusCode.ShouldBe(HttpStatusCode.OK);

		var toActive = new EmployeeStatusUpdateRequest
		{
			EmployeeStatusType = 0,
			Remarks = "Try activate",
		};
		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/status", toActive, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var body = await response.Content.ReadAsStringAsync();
		body.ShouldContain("HR:Employee:010201");
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
			EffectiveDate = TestEffectiveDate
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

		var inactiveReq = new EmployeeStatusUpdateRequest { EmployeeStatusType = 1, Remarks = "Inactive", EffectiveDate = TestEffectiveDate };
		var r1 = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/status", inactiveReq, JsonOptions);
		r1.StatusCode.ShouldBe(HttpStatusCode.OK);

		var earningComponent = await PayrollTestHelper.CreateEarningComponentAsync(Client, "CycleEarn");
		var deductionComponent = await PayrollTestHelper.CreateDeductionComponentAsync(Client, "CycleDed");
		var salaryStructure = new UpdateSalaryStructureDto
		{
			EarningComponents = new List<SalaryComponentDto>
			{
				new()
				{
					Id = Guid.NewGuid(),
					ComponentId = earningComponent.Id,
					Name = "Basic",
					Amount = 20000m,
					IsFixed = true,
					IsTemp = false,
					ApplicableMonth = null,
				}
			},
			DeductionComponents = new List<SalaryComponentDto>
			{
				new()
				{
					Id = Guid.NewGuid(),
					ComponentId = deductionComponent.Id,
					Name = "PF",
					Amount = 1200m,
					IsFixed = true,
					IsTemp = false,
					ApplicableMonth = null,
				}
			},
		};
		var salaryResp = await Client.PutAsJsonAsync(
			$"/api/payroll/employees/{empId}/salary-structure",
			salaryStructure,
			JsonOptions);
		salaryResp.StatusCode.ShouldBe(HttpStatusCode.OK);

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
	public async Task Should_Allow_Activating_Employee_After_Salary_Structure_Is_Configured()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusActiveWithPayroll");

		var earningComponent = await PayrollTestHelper.CreateEarningComponentAsync(Client, "StatusEarn");
		var deductionComponent = await PayrollTestHelper.CreateDeductionComponentAsync(Client, "StatusDed");

		var salaryStructure = new UpdateSalaryStructureDto
		{
			EarningComponents = new List<SalaryComponentDto>
			{
				new()
				{
					Id = Guid.NewGuid(),
					ComponentId = earningComponent.Id,
					Name = "Basic",
					Amount = 20000m,
					IsFixed = true,
					IsTemp = false,
					ApplicableMonth = null,
				}
			},
			DeductionComponents = new List<SalaryComponentDto>
			{
				new()
				{
					Id = Guid.NewGuid(),
					ComponentId = deductionComponent.Id,
					Name = "PF",
					Amount = 1200m,
					IsFixed = true,
					IsTemp = false,
					ApplicableMonth = null,
				}
			},
		};

		var salaryResp = await Client.PutAsJsonAsync(
			$"/api/payroll/employees/{empId}/salary-structure",
			salaryStructure,
			JsonOptions);
		salaryResp.StatusCode.ShouldBe(HttpStatusCode.OK);

		var toInactive = new EmployeeStatusUpdateRequest { EmployeeStatusType = 1, Remarks = "Inactive", EffectiveDate = TestEffectiveDate };
		(await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/status", toInactive, JsonOptions))
			.StatusCode.ShouldBe(HttpStatusCode.OK);

		var toActive = new EmployeeStatusUpdateRequest { EmployeeStatusType = 0, Remarks = "Active now" };
		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{empId}/status", toActive, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Set_Left_And_Retired_Statuses()
	{
		var empLeft = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusLeft");
		var leftReq = new EmployeeStatusUpdateRequest { EmployeeStatusType = 2, Remarks = "Employee left", EffectiveDate = TestEffectiveDate };
		var leftResp = await Client.PutAsJsonAsync($"{BaseUrl}/{empLeft}/status", leftReq, JsonOptions);
		leftResp.StatusCode.ShouldBe(HttpStatusCode.OK);
		var leftBody = await leftResp.Content.ReadFromJsonAsync<EmployeeStatusUpdateResponse>(JsonOptions);
		leftBody.ShouldNotBeNull();
		leftBody.CurrentStatus.ShouldBe(2);

		var empRetired = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusRetired");
		var retiredReq = new EmployeeStatusUpdateRequest { EmployeeStatusType = 3, Remarks = "Employee retired", EffectiveDate = TestEffectiveDate };
		var retiredResp = await Client.PutAsJsonAsync($"{BaseUrl}/{empRetired}/status", retiredReq, JsonOptions);
		retiredResp.StatusCode.ShouldBe(HttpStatusCode.OK);
		var retiredBody = await retiredResp.Content.ReadFromJsonAsync<EmployeeStatusUpdateResponse>(JsonOptions);
		retiredBody.ShouldNotBeNull();
		retiredBody.CurrentStatus.ShouldBe(3);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Block_Non_Active_Status_For_Manager_With_Active_Direct_Reports()
	{
		var managerId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusMgrBlock");
		var reportId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusMgrReport");

		var assignResponse = await Client.PutAsJsonAsync(
			$"{BaseUrl}/{reportId}/reporting-manager",
			new ReportingManagerRequest { ReportingManagerId = managerId.ToString() },
			JsonOptions);
		assignResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var response = await Client.PutAsJsonAsync(
			$"{BaseUrl}/{managerId}/status",
			new EmployeeStatusUpdateRequest { EmployeeStatusType = 1, Remarks = "Going inactive", EffectiveDate = TestEffectiveDate },
			JsonOptions);
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, rawPayload);
		rawPayload.ShouldContain("HR:Employee:010032");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Allow_Non_Active_Status_After_Direct_Reports_Are_Reassigned()
	{
		var managerId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusMgrSwapOld");
		var newManagerId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusMgrSwapNew");
		var reportId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "StatusMgrSwapReport");

		(await Client.PutAsJsonAsync(
			$"{BaseUrl}/{reportId}/reporting-manager",
			new ReportingManagerRequest { ReportingManagerId = managerId.ToString() },
			JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await Client.PutAsJsonAsync(
			$"{BaseUrl}/{reportId}/reporting-manager",
			new ReportingManagerRequest { ReportingManagerId = newManagerId.ToString() },
			JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.OK);

		var response = await Client.PutAsJsonAsync(
			$"{BaseUrl}/{managerId}/status",
			new EmployeeStatusUpdateRequest { EmployeeStatusType = 1, Remarks = "No active reports now", EffectiveDate = TestEffectiveDate },
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Theory]
	[InlineData(2)]
	[InlineData(3)]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Block_Left_And_Retired_Status_For_Manager_With_Active_Direct_Reports(int status)
	{
		var managerId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, $"StatusMgrBlock{status}");
		var reportId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, $"StatusMgrReport{status}");

		(await Client.PutAsJsonAsync(
			$"{BaseUrl}/{reportId}/reporting-manager",
			new ReportingManagerRequest { ReportingManagerId = managerId.ToString() },
			JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.OK);

		var response = await Client.PutAsJsonAsync(
			$"{BaseUrl}/{managerId}/status",
			new EmployeeStatusUpdateRequest { EmployeeStatusType = status, Remarks = "Manager unavailable", EffectiveDate = TestEffectiveDate },
			JsonOptions);
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, rawPayload);
		rawPayload.ShouldContain("HR:Employee:010032");
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
