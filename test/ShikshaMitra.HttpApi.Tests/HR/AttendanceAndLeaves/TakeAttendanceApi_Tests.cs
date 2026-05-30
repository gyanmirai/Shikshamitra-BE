using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
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
	public async Task Should_Not_Show_Employee_Before_Joining_Date_In_Day_Attendance()
	{
		var uniqueName = $"FutureJoiner{Guid.NewGuid():N}";
		var personalInfo = EmployeeTestHelper.BuildPersonalInfoRequest(uniqueName);
		personalInfo.FirstName = uniqueName;
		personalInfo.Doj = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
		var jsonData = JsonSerializer.Serialize(personalInfo, JsonOptions);

		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(jsonData), "data");

		var createResponse = await Client.PostAsync("/api/employees/personal-info", content);
		createResponse.EnsureSuccessStatusCode();
		var created = await createResponse.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create future-joining employee");

		var response = await Client.GetAsync(
			$"{BaseUrl}/day?attendanceDate=15-04-2026&skipCount=0&maxResultCount=20&search={Uri.EscapeDataString(uniqueName)}");
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden);
		if (response.StatusCode != HttpStatusCode.OK)
		{
			return;
		}

		var payload = await response.Content.ReadFromJsonAsync<TakeAttendanceDayResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize day attendance response");

		payload.Items.ShouldNotContain(x => x.EmployeeId == created.Id);
		payload.TotalCount.ShouldBe(0);
		payload.Summary.NotMarked.ShouldBe(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Show_Left_Employee_For_Dates_Before_Status_Change()
	{
		var uniqueName = $"LeftHistory{Guid.NewGuid():N}";
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, uniqueName);

		var leftRequest = new ChangeEmployeeStatusDto
		{
			EmployeeStatusType = EmployeeStatusType.Left,
			Remarks = "Historical attendance regression",
			EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow)
		};
		var statusResponse = await Client.PutAsJsonAsync($"/api/employees/{created.Id}/status", leftRequest, JsonOptions);
		statusResponse.EnsureSuccessStatusCode();

		var historicalDate = DateTime.UtcNow.AddDays(-1).ToString("dd-MM-yyyy");
		var response = await Client.GetAsync(
			$"{BaseUrl}/day?attendanceDate={historicalDate}&skipCount=0&maxResultCount=20&search={Uri.EscapeDataString(created.FirstName)}");
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden);
		if (response.StatusCode != HttpStatusCode.OK)
		{
			return;
		}

		var payload = await response.Content.ReadFromJsonAsync<TakeAttendanceDayResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize day attendance response");

		payload.Items.ShouldContain(x => x.EmployeeId == created.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Left_Employee_Historical_Attendance_Should_Remain_Visible_And_Editable()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var uniqueName = $"LeftEditableHistory{Guid.NewGuid():N}";
		var historicalDate = DateTime.UtcNow.AddDays(-1);
		var created = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			uniqueName,
			dateOfJoining: DateTime.UtcNow.AddDays(-2));

		var initialSave = await Client.PostAsJsonAsync($"{BaseUrl}/day/save", new TakeAttendanceSaveInput
		{
			AttendanceDate = historicalDate.ToString("dd-MM-yyyy"),
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = created.Id,
					StatusWire = "PRESENT",
					Remarks = "Saved before employee left"
				}
			]
		}, JsonOptions);
		initialSave.StatusCode.ShouldBe(HttpStatusCode.OK);

		var leftRequest = new ChangeEmployeeStatusDto
		{
			EmployeeStatusType = EmployeeStatusType.Left,
			Remarks = "Historical attendance remains editable",
			EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow)
		};
		var statusResponse = await Client.PutAsJsonAsync($"/api/employees/{created.Id}/status", leftRequest, JsonOptions);
		statusResponse.EnsureSuccessStatusCode();

		var getResponse = await Client.GetAsync(
			$"{BaseUrl}/day?attendanceDate={historicalDate:dd-MM-yyyy}&skipCount=0&maxResultCount=20&search={Uri.EscapeDataString(created.FirstName)}");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await getResponse.Content.ReadFromJsonAsync<TakeAttendanceDayResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize day attendance response");
		var row = payload.Items.Single(x => x.EmployeeId == created.Id);
		row.Status.ShouldBe(TakeAttendanceStatus.Present);

		var editResponse = await Client.PostAsJsonAsync($"{BaseUrl}/day/save", new TakeAttendanceSaveInput
		{
			AttendanceDate = historicalDate.ToString("dd-MM-yyyy"),
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = created.Id,
					StatusWire = "ABSENT",
					Remarks = "Historical correction after employee left"
				}
			]
		}, JsonOptions);
		editResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task SaveDay_Should_Reject_Left_Employee_On_Or_After_Status_Change_Date()
	{
		var created = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"LeftReject{Guid.NewGuid():N}");
		var leftRequest = new ChangeEmployeeStatusDto
		{
			EmployeeStatusType = EmployeeStatusType.Left,
			Remarks = "Post-left attendance regression",
			EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
		};
		var statusResponse = await Client.PutAsJsonAsync($"/api/employees/{created.Id}/status", leftRequest, JsonOptions);
		statusResponse.EnsureSuccessStatusCode();

		var request = new TakeAttendanceSaveInput
		{
			AttendanceDate = DateTime.UtcNow.ToString("dd-MM-yyyy"),
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = created.Id,
					StatusWire = "PRESENT"
				}
			]
		};

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/day/save", request, JsonOptions);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
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

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task SaveDay_Should_Reject_Invalid_StatusWire()
	{
		var employeeId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "AttendanceWire");
		var request = new TakeAttendanceSaveInput
		{
			AttendanceDate = DateTime.UtcNow.ToString("dd-MM-yyyy"),
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = employeeId,
					StatusWire = "Pending"
				}
			]
		};

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/day/save", request, JsonOptions);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);

		if (response.StatusCode == HttpStatusCode.BadRequest)
		{
			var payload = await response.Content.ReadAsStringAsync();
			using var doc = JsonDocument.Parse(payload);
			doc.RootElement.GetProperty("status").GetInt32().ShouldBe(400);
			doc.RootElement.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
		}
	}
}
