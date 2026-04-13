using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class AttendanceTrackingApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/attendance-tracking";

	public AttendanceTrackingApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Date_Wise_Attendance_With_Contract_Params()
	{
		var today = DateTime.UtcNow.ToString("dd-MM-yyyy");
		var mediumStageId = Guid.NewGuid();
		var response = await Client.GetAsync(
			$"{BaseUrl}/date-wise?attendanceDate={today}&status=PRESENT&page=1&pageSize=10&educationMediumStageId={mediumStageId}&search=abc");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		document.RootElement.TryGetProperty("totalCount", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("currentPage", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("pageSize", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("totalPages", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("summary", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("items", out _).ShouldBeTrue();
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Employee_Wise_Attendance_With_Pagination_Fields()
	{
		var employeeId = Guid.NewGuid();
		var mediumStageId = Guid.NewGuid();
		var start = DateTime.UtcNow.AddDays(-30).ToString("dd-MM-yyyy");
		var end = DateTime.UtcNow.ToString("dd-MM-yyyy");

		var response = await Client.GetAsync(
			$"{BaseUrl}/employee-wise?employeeId={employeeId}&startDate={start}&endDate={end}&page=1&pageSize=10&educationMediumStageId={mediumStageId}&search=mon");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		document.RootElement.TryGetProperty("totalCount", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("currentPage", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("pageSize", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("totalPages", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("attendanceSummary", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("items", out _).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Status_With_400_And_Standard_Error_Envelope()
	{
		var today = DateTime.UtcNow.ToString("dd-MM-yyyy");
		var mediumStageId = Guid.NewGuid();
		var response = await Client.GetAsync(
			$"{BaseUrl}/date-wise?attendanceDate={today}&status=INVALID&page=1&pageSize=10&educationMediumStageId={mediumStageId}");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		document.RootElement.TryGetProperty("success", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("error", out _).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Date_Format_With_400()
	{
		var mediumStageId = Guid.NewGuid();
		var response = await Client.GetAsync(
			$"{BaseUrl}/date-wise?attendanceDate=not-a-date&status=PRESENT&page=1&pageSize=10&educationMediumStageId={mediumStageId}");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Missing_EducationMediumStageId_With_400()
	{
		var today = DateTime.UtcNow.ToString("dd-MM-yyyy");
		var response = await Client.GetAsync(
			$"{BaseUrl}/date-wise?attendanceDate={today}&status=PRESENT&page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	#endregion
}
