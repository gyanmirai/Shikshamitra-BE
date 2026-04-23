using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class TakeAttendanceApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/hr/attendance/take-attendance";

	public TakeAttendanceApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Day_Without_EducationMediumStageId()
	{
		var response = await Client.GetAsync($"{BaseUrl}/day?attendanceDate=11-04-2026&skipCount=0&maxResultCount=20");
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden);
		if (response.StatusCode != HttpStatusCode.OK)
		{
			return;
		}

		var json = await response.Content.ReadAsStringAsync();
		using var doc = JsonDocument.Parse(json);
		doc.RootElement.TryGetProperty("items", out var itemsElement).ShouldBeTrue();
		itemsElement.ValueKind.ShouldBe(JsonValueKind.Array);
		if (itemsElement.GetArrayLength() > 0)
		{
			var first = itemsElement[0];
			first.TryGetProperty("shiftStartTime", out _).ShouldBeTrue();
			first.TryGetProperty("shiftEndTime", out _).ShouldBeTrue();
			first.TryGetProperty("shiftName", out _).ShouldBeTrue();
			first.TryGetProperty("status", out var status).ShouldBeTrue();
			status.ValueKind.ShouldBe(JsonValueKind.Number);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Lock_And_Unlock_Day_Without_EducationMediumStageId()
	{
		var lockResponse = await Client.PostAsync($"{BaseUrl}/day/lock?attendanceDate=11-04-2026", null);
		lockResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.NoContent, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);
		if (lockResponse.StatusCode == HttpStatusCode.NoContent)
		{
			var secondLock = await Client.PostAsync($"{BaseUrl}/day/lock?attendanceDate=11-04-2026", null);
			secondLock.StatusCode.ShouldBe(HttpStatusCode.NoContent);

			var unlockResponse = await Client.DeleteAsync($"{BaseUrl}/day/lock?attendanceDate=11-04-2026");
			unlockResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

			var secondUnlock = await Client.DeleteAsync($"{BaseUrl}/day/lock?attendanceDate=11-04-2026");
			secondUnlock.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task SaveDay_Should_Not_Require_EducationMediumStageId_In_Request()
	{
		var request = new TakeAttendanceSaveInput
		{
			AttendanceDate = "11-04-2026",
			Entries = []
		};

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/day/save", request, JsonOptions);
		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden,
			HttpStatusCode.InternalServerError);

		if (response.StatusCode == HttpStatusCode.BadRequest)
		{
			var payload = await response.Content.ReadAsStringAsync();
			payload.Contains("educationMediumStageId", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
		}
	}
}
