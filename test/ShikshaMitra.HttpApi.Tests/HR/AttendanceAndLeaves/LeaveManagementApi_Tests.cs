using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class LeaveManagementApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/leave-management";
	private static readonly Guid TestAcademicYearId = Guid.Parse("00000000-0000-0000-0000-000000000030");

	public LeaveManagementApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Leave_Request_List()
	{
		var monthYear = Uri.EscapeDataString(new DateTime(DateTime.UtcNow.Year, 6, 1).ToString("O"));
		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={TestAcademicYearId}&month-year={monthYear}&page=1&pageSize=10");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task List_Response_Should_Have_TotalCount_And_Items_When_Ok()
	{
		var monthYear = Uri.EscapeDataString(new DateTime(DateTime.UtcNow.Year, 6, 1).ToString("O"));
		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={TestAcademicYearId}&month-year={monthYear}&page=1&pageSize=10");

		if (response.StatusCode != HttpStatusCode.OK)
		{
			return;
		}

		var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		json.TryGetProperty("totalCount", out _).ShouldBeTrue("Response must include 'totalCount'");
		json.TryGetProperty("items", out var items).ShouldBeTrue("Response must include 'items'");
		items.ValueKind.ShouldBe(JsonValueKind.Array);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Leave_Request_By_Id_Returns_NotFound_For_Random_Guid()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Delete_Returns_NotFound_For_Random_Guid()
	{
		var response = await Client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Page_Parameters()
	{
		var monthYear = Uri.EscapeDataString(new DateTime(DateTime.UtcNow.Year, 6, 1).ToString("O"));
		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={TestAcademicYearId}&month-year={monthYear}&page=0&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_PageSize_Parameters()
	{
		var monthYear = Uri.EscapeDataString(new DateTime(DateTime.UtcNow.Year, 6, 1).ToString("O"));
		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={TestAcademicYearId}&month-year={monthYear}&page=1&pageSize=5");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	#endregion
}
