using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class LeavePolicyApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/leave-policies";
	private Guid TestMediumStageId => Factory.SeededMediumStageId;

	public LeavePolicyApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Leave_Policy()
	{
		var leaveType = await CreateLeaveTypeAsync();

		var input = new LeavePolicyCreateRequest
		{
			MediumStageId = TestMediumStageId,
			PolicyTitle = TestDataHelper.UniqueName("AnnualPolicy"),
			LeaveType = leaveType.Id,
			MaxLeavePerYear = 12,
			PaidType = "Paid",
			CarryForward = true,
			EncashmentAllowed = false,
			RequiresApproval = true,
			Description = "Annual leave policy",
		};

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?mediumStageId={TestMediumStageId}", input, JsonOptions);

		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<LeavePolicyResponse>(JsonOptions);
		body.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Leave_Policy_List()
	{
		var leaveType = await CreateLeaveTypeAsync();
		await CreateLeavePolicyAsync(leaveType.Id);

		var response = await Client.GetAsync(
			$"{BaseUrl}?mediumStageId={TestMediumStageId}&page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<LeavePolicyListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Leave_Policy()
	{
		var leaveType = await CreateLeaveTypeAsync();
		var created = await CreateLeavePolicyAsync(leaveType.Id);

		var updateRequest = new LeavePolicyCreateRequest
		{
			MediumStageId = TestMediumStageId,
			PolicyTitle = TestDataHelper.UniqueName("UpdatedPolicy"),
			LeaveType = leaveType.Id,
			MaxLeavePerYear = 15,
			PaidType = "Paid",
			CarryForward = false,
			EncashmentAllowed = true,
			RequiresApproval = true,
		};

		var response = await Client.PutAsJsonAsync(
			$"{BaseUrl}/{created.Id}", updateRequest, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Delete_Leave_Policy()
	{
		var leaveType = await CreateLeaveTypeAsync();
		var created = await CreateLeavePolicyAsync(leaveType.Id);

		var response = await Client.DeleteAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	#endregion

	#region Helpers

	private async Task<LeaveTypeResponse> CreateLeaveTypeAsync()
	{
		var input = new LeaveTypeCreateRequest
		{
			MediumStageId = TestMediumStageId,
			Name = TestDataHelper.UniqueName("PolicyLeave"),
			Color = "#28A745",
			Description = "Leave type for policy test",
		};
		var response = await Client.PostAsJsonAsync(
			$"/api/leave-types?mediumStageId={TestMediumStageId}", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<LeaveTypeResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create leave type");
	}

	private async Task<LeavePolicyResponse> CreateLeavePolicyAsync(Guid leaveTypeId)
	{
		var input = new LeavePolicyCreateRequest
		{
			MediumStageId = TestMediumStageId,
			PolicyTitle = TestDataHelper.UniqueName("TestPolicy"),
			LeaveType = leaveTypeId,
			MaxLeavePerYear = 12,
			PaidType = "Paid",
			CarryForward = true,
			EncashmentAllowed = false,
			RequiresApproval = true,
		};
		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?mediumStageId={TestMediumStageId}", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<LeavePolicyResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create leave policy");
	}

	#endregion
}
