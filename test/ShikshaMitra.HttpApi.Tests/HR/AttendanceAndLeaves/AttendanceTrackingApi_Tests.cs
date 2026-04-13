using System;
using System.Net;
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
	public async Task Should_Get_Date_Wise_Attendance()
	{
		var today = DateTime.UtcNow.ToString("dd-MM-yyyy");
		var response = await Client.GetAsync(
			$"{BaseUrl}/date-wise?attendanceDate={today}&page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Employee_Wise_Attendance()
	{
		var employeeId = Guid.NewGuid();
		var start = DateTime.UtcNow.AddDays(-30).ToString("dd-MM-yyyy");
		var end = DateTime.UtcNow.ToString("dd-MM-yyyy");

		var response = await Client.GetAsync(
			$"{BaseUrl}/employee-wise?employeeId={employeeId}&startDate={start}&endDate={end}");

		// Unknown employee returns 200 with empty items
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Date_Format()
	{
		var response = await Client.GetAsync(
			$"{BaseUrl}/date-wise?attendanceDate=not-a-date&page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_EndDate_Before_StartDate()
	{
		var employeeId = Guid.NewGuid();
		var start = DateTime.UtcNow.ToString("dd-MM-yyyy");
		var end = DateTime.UtcNow.AddDays(-10).ToString("dd-MM-yyyy");

		var response = await Client.GetAsync(
			$"{BaseUrl}/employee-wise?employeeId={employeeId}&startDate={start}&endDate={end}");

		response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Date_Range_Exceeding_90_Days()
	{
		var employeeId = Guid.NewGuid();
		var start = DateTime.UtcNow.AddDays(-100).ToString("dd-MM-yyyy");
		var end = DateTime.UtcNow.ToString("dd-MM-yyyy");

		var response = await Client.GetAsync(
			$"{BaseUrl}/employee-wise?employeeId={employeeId}&startDate={start}&endDate={end}");

		response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
	}

	#endregion
}
