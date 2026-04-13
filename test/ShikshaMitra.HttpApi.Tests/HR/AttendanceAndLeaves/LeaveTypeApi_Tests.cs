using System;
using System.Net;
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
	private const string BaseUrl = "/api/hr-leave-types";

	public LeaveTypeApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_And_Get_Leave_Type()
	{
		var created = await CreateLeaveTypeAsync();

		var getResponse = await Client.GetAsync($"{BaseUrl}/{created.Id}");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var body = await getResponse.Content.ReadFromJsonAsync<LeaveTypeResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldBe(created.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Filter_Leave_Type_List_By_Search()
	{
		var marker = $"Leave-{Guid.NewGuid():N}";
		await CreateLeaveTypeAsync(marker);

		var response = await Client.GetAsync($"{BaseUrl}?academicYearId={AcademicYearId}&page=1&pageSize=10&search={marker}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var list = await response.Content.ReadFromJsonAsync<LeaveTypeListResponse>(JsonOptions);
		list.ShouldNotBeNull();
		list.Items.ShouldContain(x => x.Name == marker);
	}

	private async Task<LeaveTypeResponse> CreateLeaveTypeAsync(string? name = null)
	{
		var request = new LeaveTypeCreateRequest
		{
			Name = name ?? $"Sick-{Guid.NewGuid():N}",
			Color = "#28A745",
			Description = "Sick leave"
		};

		var response = await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={AcademicYearId}", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.Created);

		return await response.Content.ReadFromJsonAsync<LeaveTypeResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create leave type.");
	}

	private Guid AcademicYearId => Factory.SeededAcademicYearId;
}
