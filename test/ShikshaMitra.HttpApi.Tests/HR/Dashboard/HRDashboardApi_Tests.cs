using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Dashboard;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class HRDashboardApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/hr/dashboard";

	public HRDashboardApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_HR_Dashboard_Summary_With_Contract_Sections()
	{
		var response = await Client.GetAsync($"{BaseUrl}/summary?date=2026-05-23&month=2026-05&range=7");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);

		document.RootElement.TryGetProperty("workforce", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("attendanceToday", out var attendanceToday).ShouldBeTrue();
		attendanceToday.TryGetProperty("missingAttendance", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("attendanceTrend", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("leave", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("payroll", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("celebrations", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("insights", out _).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Dashboard_Date()
	{
		var response = await Client.GetAsync($"{BaseUrl}/summary?date=not-a-date&month=2026-05");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Dashboard_Month()
	{
		var response = await Client.GetAsync($"{BaseUrl}/summary?date=2026-05-23&month=05-2026");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}
}
