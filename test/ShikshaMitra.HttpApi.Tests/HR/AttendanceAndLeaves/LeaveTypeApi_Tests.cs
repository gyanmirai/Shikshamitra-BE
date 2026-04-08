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
public class LeaveTypeApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/leave-types";
	private static readonly Guid TestMediumStageId = Guid.NewGuid();

	public LeaveTypeApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Leave_Type()
	{
		var input = BuildCreateRequest();

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?mediumStageId={TestMediumStageId}", input, JsonOptions);

		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<LeaveTypeResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Leave_Type_By_Id()
	{
		var created = await CreateLeaveTypeAsync();

		var response = await Client.GetAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<LeaveTypeResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldBe(created.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Leave_Type_List()
	{
		await CreateLeaveTypeAsync();

		var response = await Client.GetAsync(
			$"{BaseUrl}?mediumStageId={TestMediumStageId}&page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<LeaveTypeListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Leave_Type()
	{
		var created = await CreateLeaveTypeAsync();

		var updateRequest = new LeaveTypeUpdateRequest
		{
			Name = TestDataHelper.UniqueName("UpdatedLeave"),
			Color = "#DC3545",
			Description = "Updated description",
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", updateRequest, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Delete_Leave_Type()
	{
		var created = await CreateLeaveTypeAsync();

		var response = await Client.DeleteAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	#endregion

	#region Helpers

	private static LeaveTypeCreateRequest BuildCreateRequest()
	{
		return new LeaveTypeCreateRequest
		{
			MediumStageId = TestMediumStageId,
			Name = TestDataHelper.UniqueName("SickLeave"),
			Color = "#28A745",
			Description = "Sick leave type",
		};
	}

	private async Task<LeaveTypeResponse> CreateLeaveTypeAsync()
	{
		var input = BuildCreateRequest();
		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?mediumStageId={TestMediumStageId}", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<LeaveTypeResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create leave type");
	}

	#endregion
}
