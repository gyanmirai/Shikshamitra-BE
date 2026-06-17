using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
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
	public async Task Day_Should_Return_Holiday_Metadata_For_Summer_Vacation()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"HolidayDay{Guid.NewGuid():N}";
		await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			marker,
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

		var response = await Client.GetAsync(
			$"{BaseUrl}/day?attendanceDate=10-05-2026&skipCount=0&maxResultCount=20&search={Uri.EscapeDataString(marker)}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<TakeAttendanceDayResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize day attendance response");

		payload.IsHoliday.ShouldBeTrue();
		payload.HolidayName.ShouldNotBeNull();
		payload.HolidayName.ShouldContain("Summer Vacation");
		var row = payload.Items.Single();
		row.IsHoliday.ShouldBeTrue();
		row.HolidayName.ShouldNotBeNull();
		row.HolidayName.ShouldContain("Summer Vacation");
		row.HolidayReason.ShouldNotBeNull();
		row.HolidayReason.ShouldContain("Holiday:");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Day_Should_Auto_Fill_Approved_Full_Day_Leave_As_OnLeave()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = new DateOnly(2026, 4, 11);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"OnLeaveFull{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
		var leaveId = await InsertLeaveForAttendanceAsync(
			employee.Id,
			attendanceDate,
			attendanceDate,
			EmployeeLeaveStatusType.Approved);

		var response = await Client.GetAsync(
			$"{BaseUrl}/day?attendanceDate={attendanceDate:dd-MM-yyyy}&skipCount=0&maxResultCount=20&search={Uri.EscapeDataString(employee.FirstName)}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<TakeAttendanceDayResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize day attendance response");
		var row = payload.Items.Single(x => x.EmployeeId == employee.Id);

		row.Status.ShouldBe(TakeAttendanceStatus.OnLeave);
		row.IsApprovedLeave.ShouldBeTrue();
		row.ApprovedLeaveId.ShouldBe(leaveId);
		row.ApprovedLeaveKind.ShouldBe("FullDay");
		row.ApprovedLeaveLabel.ShouldBe("On Leave");
		row.Source.ShouldBe("approved_leave");
		payload.Summary.OnLeave.ShouldBe(1);
		payload.Summary.NotMarked.ShouldBe(0);
		payload.Summary.Absent.ShouldBe(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Day_Should_Auto_Fill_Approved_Half_Day_Leave_Metadata()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = new DateOnly(2026, 4, 11);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"OnLeaveHalf{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
		await InsertLeaveForAttendanceAsync(
			employee.Id,
			attendanceDate,
			attendanceDate,
			EmployeeLeaveStatusType.Approved,
			halfDay: true);

		var response = await Client.GetAsync(
			$"{BaseUrl}/day?attendanceDate={attendanceDate:dd-MM-yyyy}&skipCount=0&maxResultCount=20&search={Uri.EscapeDataString(employee.FirstName)}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<TakeAttendanceDayResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize day attendance response");
		var row = payload.Items.Single(x => x.EmployeeId == employee.Id);

		row.Status.ShouldBe(TakeAttendanceStatus.OnLeave);
		row.IsApprovedLeave.ShouldBeTrue();
		row.ApprovedLeaveKind.ShouldBe("HalfDay");
		row.ApprovedLeaveLabel.ShouldBe("On Leave (Half Day)");
		payload.Summary.OnLeave.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Day_Should_Keep_Manual_Attendance_When_Approved_Leave_Exists()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = new DateOnly(2026, 4, 11);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"OnLeaveOverride{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
		await InsertLeaveForAttendanceAsync(
			employee.Id,
			attendanceDate,
			attendanceDate,
			EmployeeLeaveStatusType.Approved);

		await InsertAttendanceForTestAsync(
			employee.Id,
			attendanceDate,
			EmployeeAttendanceStatusType.Present,
			"Manual override");

		var response = await Client.GetAsync(
			$"{BaseUrl}/day?attendanceDate={attendanceDate:dd-MM-yyyy}&skipCount=0&maxResultCount=20&search={Uri.EscapeDataString(employee.FirstName)}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<TakeAttendanceDayResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize day attendance response");
		var row = payload.Items.Single(x => x.EmployeeId == employee.Id);

		row.Status.ShouldBe(TakeAttendanceStatus.Present);
		row.IsApprovedLeave.ShouldBeTrue();
		row.Source.ShouldBe("manual");
		row.Warnings.ShouldContain(x => x.Contains("Approved leave exists", StringComparison.OrdinalIgnoreCase));
		payload.Summary.OnLeave.ShouldBe(0);
		payload.Summary.Present.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Day_Should_Not_Auto_Fill_Approved_Leave_Outside_Selected_Date()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = new DateOnly(2026, 4, 11);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"OnLeaveOutside{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
		await InsertLeaveForAttendanceAsync(
			employee.Id,
			attendanceDate.AddDays(1),
			attendanceDate.AddDays(1),
			EmployeeLeaveStatusType.Approved);

		var response = await Client.GetAsync(
			$"{BaseUrl}/day?attendanceDate={attendanceDate:dd-MM-yyyy}&skipCount=0&maxResultCount=20&search={Uri.EscapeDataString(employee.FirstName)}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<TakeAttendanceDayResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize day attendance response");
		var row = payload.Items.Single(x => x.EmployeeId == employee.Id);

		row.Status.ShouldBe(TakeAttendanceStatus.NotMarked);
		row.IsApprovedLeave.ShouldBeFalse();
		payload.Summary.OnLeave.ShouldBe(0);
	}

	[Theory]
	[InlineData(EmployeeLeaveStatusType.Pending)]
	[InlineData(EmployeeLeaveStatusType.Reviewed)]
	[InlineData(EmployeeLeaveStatusType.Rejected)]
	[InlineData(EmployeeLeaveStatusType.Cancelled)]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Day_Should_Not_Auto_Fill_Non_Approved_Leave(EmployeeLeaveStatusType status)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = new DateOnly(2026, 4, 11);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"OnLeaveNonApproved{status}{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
		await InsertLeaveForAttendanceAsync(employee.Id, attendanceDate, attendanceDate, status);

		var response = await Client.GetAsync(
			$"{BaseUrl}/day?attendanceDate={attendanceDate:dd-MM-yyyy}&skipCount=0&maxResultCount=20&search={Uri.EscapeDataString(employee.FirstName)}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<TakeAttendanceDayResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize day attendance response");
		var row = payload.Items.Single(x => x.EmployeeId == employee.Id);

		row.Status.ShouldBe(TakeAttendanceStatus.NotMarked);
		row.IsApprovedLeave.ShouldBeFalse();
		payload.Summary.OnLeave.ShouldBe(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task SaveDay_Should_Reject_Holiday_Attendance()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"HolidaySave{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/day/save", new TakeAttendanceSaveInput
		{
			AttendanceDate = "10-05-2026",
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = employee.Id,
					StatusWire = "PRESENT",
					Remarks = "Holiday save should be rejected"
				}
			]
		}, JsonOptions);

		var body = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
		body.ShouldContain("HR:Attendance:020314");
		body.ShouldContain("Summer Vacation");
	}

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
					InTime = "09:00:00",
					OutTime = "17:00:00",
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
	public async Task Day_Should_Handle_Mixed_Capture_Modes_Without_Throwing()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"MixedCapture{Guid.NewGuid():N}";
		var attendanceDate = DateTime.UtcNow.AddDays(-1);
		var manualStatusEmployee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"{marker}Status",
			dateOfJoining: attendanceDate.AddDays(-10));
		var manualTimeEmployee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"{marker}Time",
			dateOfJoining: attendanceDate.AddDays(-10));
		await AssignCaptureModePolicyAsync(manualStatusEmployee.Id, DateOnly.FromDateTime(attendanceDate), AttendanceCaptureMode.ManualStatus);
		await AssignCaptureModePolicyAsync(manualTimeEmployee.Id, DateOnly.FromDateTime(attendanceDate), AttendanceCaptureMode.ManualWithTime);

		var response = await Client.GetAsync(
			$"{BaseUrl}/day?attendanceDate={attendanceDate:dd-MM-yyyy}&skipCount=0&maxResultCount=20&search={Uri.EscapeDataString(marker)}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<TakeAttendanceDayResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize day attendance response");

		payload.CaptureMode.ShouldBe("integrated");
		payload.Items.Single(x => x.EmployeeId == manualStatusEmployee.Id).CaptureMode.ShouldBe("manual_status");
		payload.Items.Single(x => x.EmployeeId == manualTimeEmployee.Id).CaptureMode.ShouldBe("manual_with_time");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task SaveDay_Should_Strip_Times_For_Manual_Status_Policy()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = DateTime.UtcNow.AddDays(-1);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"ManualStatusSave{Guid.NewGuid():N}",
			dateOfJoining: attendanceDate.AddDays(-10));
		await AssignCaptureModePolicyAsync(employee.Id, DateOnly.FromDateTime(attendanceDate), AttendanceCaptureMode.ManualStatus);

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/day/save", new TakeAttendanceSaveInput
		{
			AttendanceDate = attendanceDate.ToString("dd-MM-yyyy"),
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = employee.Id,
					StatusWire = "PRESENT",
					InTime = "09:00:00",
					OutTime = "17:00:00"
				}
			]
		}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var attendance = await GetAttendanceAsync(employee.Id, DateOnly.FromDateTime(attendanceDate));
		attendance.TimeIn.ShouldBeNull();
		attendance.TimeOut.ShouldBeNull();
		attendance.Source.ShouldBe(AttendanceRecordSource.Manual);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task SaveDay_Should_Stamp_Manual_Time_Source_For_Manual_With_Time_Policy()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = DateTime.UtcNow.AddDays(-1);
		var attendanceDateOnly = DateOnly.FromDateTime(attendanceDate);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"ManualTimeSave{Guid.NewGuid():N}",
			dateOfJoining: attendanceDate.AddDays(-10));
		await AssignCaptureModePolicyAsync(employee.Id, attendanceDateOnly, AttendanceCaptureMode.ManualWithTime);

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/day/save", new TakeAttendanceSaveInput
		{
			AttendanceDate = attendanceDateOnly.ToString("yyyy-MM-dd"),
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = employee.Id,
					StatusWire = "PRESENT",
					InTime = "09:00:00",
					OutTime = "17:00:00"
				}
			]
		}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var attendance = await GetAttendanceAsync(employee.Id, attendanceDateOnly);
		attendance.TimeIn.ShouldNotBeNull();
		attendance.TimeOut.ShouldNotBeNull();
		DateOnly.FromDateTime(attendance.TimeIn.Value.Date).ShouldBe(attendanceDateOnly);
		DateOnly.FromDateTime(attendance.TimeOut.Value.Date).ShouldBe(attendanceDateOnly);
		attendance.Source.ShouldBe(AttendanceRecordSource.ManualTime);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task SaveDay_Should_Accept_Absent_With_Zero_Times_For_Manual_With_Time_Policy()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = new DateTime(2026, 4, 9);
		var attendanceDateOnly = DateOnly.FromDateTime(attendanceDate);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"ManualAbsentZeroTime{Guid.NewGuid():N}",
			dateOfJoining: attendanceDate.AddDays(-10));
		await AssignCaptureModePolicyAsync(employee.Id, attendanceDateOnly, AttendanceCaptureMode.ManualWithTime);

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/day/save", new TakeAttendanceSaveInput
		{
			AttendanceDate = attendanceDateOnly.ToString("yyyy-MM-dd"),
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = employee.Id,
					StatusWire = "ABSENT",
					InTime = "00:00:00",
					OutTime = "00:00:00"
				}
			]
		}, JsonOptions);
		var responseBody = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, responseBody);

		var attendance = await GetAttendanceAsync(employee.Id, attendanceDateOnly);
		attendance.StatusType.ShouldBe(EmployeeAttendanceStatusType.FullDayLeave);
		attendance.TimeIn.ShouldNotBeNull();
		attendance.TimeOut.ShouldNotBeNull();
		attendance.TimeIn.Value.TimeOfDay.ShouldBe(TimeSpan.Zero);
		attendance.TimeOut.Value.TimeOfDay.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task SaveDay_Should_Normalize_DateTime_Punches_To_Selected_Attendance_Date()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
		var mismatchedPunchDate = attendanceDate.AddMonths(-1);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"ManualDateTimePunch{Guid.NewGuid():N}",
			dateOfJoining: attendanceDate.AddDays(-10).ToDateTime(TimeOnly.MinValue));
		await AssignCaptureModePolicyAsync(employee.Id, attendanceDate, AttendanceCaptureMode.ManualWithTime);

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/day/save", new TakeAttendanceSaveInput
		{
			AttendanceDate = attendanceDate.ToString("yyyy-MM-dd"),
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = employee.Id,
					StatusWire = "PRESENT",
					InTime = $"{mismatchedPunchDate:yyyy-MM-dd}T09:15:00+05:30",
					OutTime = $"{mismatchedPunchDate:yyyy-MM-dd}T17:45:00+05:30"
				}
			]
		}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var attendance = await GetAttendanceAsync(employee.Id, attendanceDate);
		attendance.TimeIn.ShouldNotBeNull();
		attendance.TimeOut.ShouldNotBeNull();
		DateOnly.FromDateTime(attendance.TimeIn.Value.Date).ShouldBe(attendanceDate);
		DateOnly.FromDateTime(attendance.TimeOut.Value.Date).ShouldBe(attendanceDate);
		attendance.TimeIn.Value.TimeOfDay.ShouldBe(new TimeSpan(9, 15, 0));
		attendance.TimeOut.Value.TimeOfDay.ShouldBe(new TimeSpan(17, 45, 0));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Day_Should_Load_After_Archived_Assigned_Attendance_Policy_Falls_Back_To_Default()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = new DateOnly(2026, 4, 11);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"ArchivedAttendancePolicy{Guid.NewGuid():N}",
			dateOfJoining: attendanceDate.AddDays(-10).ToDateTime(TimeOnly.MinValue));
		var policyContext = await AssignArchivableAttendancePolicyAsync(employee.Id, attendanceDate);

		var previewResponse = await Client.PostAsJsonAsync("/api/hr/policies/impact-preview", new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "attendance",
			PolicyId = policyContext.PolicyId,
			AcademicYearId = policyContext.AcademicYearId,
			Action = "archive"
		}, JsonOptions);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await previewResponse.Content.ReadFromJsonAsync<HrPolicyImpactPreviewDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize policy impact preview response");
		preview.AffectedEmployeeCount.ShouldBeGreaterThan(0);
		preview.Warnings.ShouldContain(x => x.Code == HRErrorCodes.AffectedEmployees);
		preview.RequiresConfirmation.ShouldBeTrue();
		preview.ConfirmationToken.ShouldNotBeNullOrWhiteSpace();

		Client.DefaultRequestHeaders.Remove(HrPolicyImpactConfirmationService.HeaderName);
		Client.DefaultRequestHeaders.Add(HrPolicyImpactConfirmationService.HeaderName, preview.ConfirmationToken);
		try
		{
			var archiveResponse = await Client.DeleteAsync($"/api/hr/attendance/policies/{policyContext.PolicyId:D}");
			archiveResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		}
		finally
		{
			Client.DefaultRequestHeaders.Remove(HrPolicyImpactConfirmationService.HeaderName);
		}

		var response = await Client.GetAsync(
			$"{BaseUrl}/day?attendanceDate={attendanceDate:dd-MM-yyyy}&skipCount=0&maxResultCount=20&search={Uri.EscapeDataString(employee.FirstName)}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<TakeAttendanceDayResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize day attendance response");
		var row = payload.Items.Single(x => x.EmployeeId == employee.Id);
		row.Status.ShouldBe(TakeAttendanceStatus.NotMarked);
		row.Errors.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task SaveDay_Should_Reject_Timestamp_AttendanceDate()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/day/save", new TakeAttendanceSaveInput
		{
			AttendanceDate = "2026-06-01T22:15:34-07:00",
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = Guid.NewGuid(),
					StatusWire = "PRESENT",
					InTime = "09:00:00",
					OutTime = "17:00:00"
				}
			]
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var payload = await response.Content.ReadAsStringAsync();
		payload.ShouldContain(HRErrorCodes.AttendanceInvalidDateFormat);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task SaveDay_Should_Reject_Overnight_Shift_For_Worked_Time_Evaluation()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"ManualOvernightShift{Guid.NewGuid():N}",
			dateOfJoining: attendanceDate.AddDays(-10).ToDateTime(TimeOnly.MinValue));
		var overnightShiftId = await InsertShiftAsync(
			$"Overnight Manual Test {Guid.NewGuid():N}",
			new TimeOnly(22, 0),
			new TimeOnly(6, 0));
		await AssignEmployeeShiftAsync(employee.Id, overnightShiftId);
		await AssignCaptureModePolicyAsync(employee.Id, attendanceDate, AttendanceCaptureMode.ManualWithTime);

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/day/save", new TakeAttendanceSaveInput
		{
			AttendanceDate = attendanceDate.ToString("yyyy-MM-dd"),
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = employee.Id,
					StatusWire = "PRESENT",
					InTime = "22:00:00",
					OutTime = "23:00:00"
				}
			]
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var payload = await response.Content.ReadAsStringAsync();
		payload.ShouldContain(HRErrorCodes.AttendancePolicyEvaluationInvalidShift);
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

	private async Task AssertAttendanceStageAsync(Guid employeeId, DateOnly attendanceDate, Guid expectedStageId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var attendanceRepository = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var query = await attendanceRepository.GetQueryableAsync();
			var attendance = query.Single(x =>
				x.EmployeeId == employeeId &&
				x.AttendanceDate == attendanceDate);
			attendance.EducationMediumStageId.ShouldBe(expectedStageId);
			await uow.CompleteAsync();
		}
	}

	private async Task EnsureUserHasStageScopeAsync(Guid userId, Guid stageId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var scopeRepository = scope.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		var userScopeService = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					tenantId,
					userId: userId,
					educationMediumStageId: stageId,
					description: "API regression test stage HR scope"),
				autoSave: false);
			await userScopeService.InvalidateUserScopeCacheAsync(userId);
			await uow.CompleteAsync();
		}
	}

	private async Task AssignCaptureModePolicyAsync(
		Guid employeeId,
		DateOnly effectiveDate,
		AttendanceCaptureMode captureMode)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var attendancePolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		var payrollPolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		var leavePolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<LeavePolicy, Guid>>();
		var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var guidGenerator = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var targetDate = effectiveDate.ToDateTime(TimeOnly.MinValue);
			var academicYearQuery = await academicYearRepository.GetQueryableAsync();
			var academicYear = academicYearQuery.Single(x => x.StartDate <= targetDate && x.EndDate >= targetDate);
			var attendanceQuery = await attendancePolicyRepository.GetQueryableAsync();
			var defaultAttendance = attendanceQuery.Single(x =>
				x.AcademicYearId == academicYear.Id &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active);
			var payrollQuery = await payrollPolicyRepository.GetQueryableAsync();
			var defaultPayroll = payrollQuery.Single(x =>
				x.AcademicYearId == academicYear.Id &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active);
			var leavePolicyId = await EnsureActiveDefaultLeavePolicyAsync(
				tenantId,
				academicYear.Id,
				academicYear.StartDate,
				academicYear.EndDate,
				leavePolicyRepository,
				guidGenerator);
			var policy = new AttendancePolicy(
				Guid.NewGuid(),
				tenantId,
				academicYear.Id,
				defaultAttendance.WorkCalendarPolicyId,
				$"Test {captureMode} {Guid.NewGuid():N}",
				HrPolicyStatus.Active,
				false,
				captureMode,
				75,
				50,
				0,
				AttendanceMissingPunchBehavior.RequiresReview,
				true,
				0,
				0,
				AttendancePenaltyType.None,
				0,
				false,
				0,
				false,
				null,
				null,
				AttendanceHolidayWorkBehavior.CompOff,
				false,
				true);
			await attendancePolicyRepository.InsertAsync(policy, autoSave: false);
			await assignmentRepository.InsertAsync(new HrPolicyAssignment(
				Guid.NewGuid(),
				tenantId,
				academicYear.Id,
				HrPolicyAssignmentTargetType.Employee,
				employeeId.ToString("D"),
				null,
				policy.Id,
				defaultPayroll.Id,
				leavePolicyId,
				targetDate.Date,
				null,
				true,
				priority: 100,
				assignmentReason: "Capture mode regression test"), autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task<(Guid PolicyId, Guid AcademicYearId)> AssignArchivableAttendancePolicyAsync(
		Guid employeeId,
		DateOnly effectiveDate)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var attendancePolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		var payrollPolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		var leavePolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<LeavePolicy, Guid>>();
		var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var guidGenerator = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var targetDate = effectiveDate.ToDateTime(TimeOnly.MinValue);
			var academicYearQuery = await academicYearRepository.GetQueryableAsync();
			var academicYear = academicYearQuery.Single(x => x.StartDate <= targetDate && x.EndDate >= targetDate);
			var attendanceQuery = await attendancePolicyRepository.GetQueryableAsync();
			var defaultAttendance = attendanceQuery.Single(x =>
				x.AcademicYearId == academicYear.Id &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active);
			var payrollQuery = await payrollPolicyRepository.GetQueryableAsync();
			var defaultPayroll = payrollQuery.Single(x =>
				x.AcademicYearId == academicYear.Id &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active);
			var leavePolicyId = await EnsureActiveDefaultLeavePolicyAsync(
				tenantId,
				academicYear.Id,
				academicYear.StartDate,
				academicYear.EndDate,
				leavePolicyRepository,
				guidGenerator);
			var policy = new AttendancePolicy(
				Guid.NewGuid(),
				tenantId,
				academicYear.Id,
				defaultAttendance.WorkCalendarPolicyId,
				$"Archive fallback {Guid.NewGuid():N}",
				HrPolicyStatus.Active,
				false,
				AttendanceCaptureMode.ManualStatus,
				75,
				50,
				0,
				AttendanceMissingPunchBehavior.RequiresReview,
				true,
				0,
				0,
				AttendancePenaltyType.None,
				0,
				false,
				0,
				false,
				null,
				null,
				AttendanceHolidayWorkBehavior.CompOff,
				false,
				true);
			await attendancePolicyRepository.InsertAsync(policy, autoSave: false);
			await assignmentRepository.InsertAsync(new HrPolicyAssignment(
				Guid.NewGuid(),
				tenantId,
				academicYear.Id,
				HrPolicyAssignmentTargetType.Employee,
				employeeId.ToString("D"),
				null,
				policy.Id,
				defaultPayroll.Id,
				leavePolicyId,
				targetDate.Date,
				null,
				true,
				priority: 100,
				assignmentReason: "Archived attendance policy API regression test"), autoSave: false);
			await uow.CompleteAsync();
			return (policy.Id, academicYear.Id);
		}
	}

	private static async Task<Guid> EnsureActiveDefaultLeavePolicyAsync(
		Guid tenantId,
		Guid academicYearId,
		DateTime effectiveFrom,
		DateTime effectiveTo,
		IRepository<LeavePolicy, Guid> leavePolicyRepository,
		IGuidGenerator guidGenerator)
	{
		var leavePolicyQuery = await leavePolicyRepository.GetQueryableAsync();
		var existing = leavePolicyQuery.FirstOrDefault(x =>
			x.AcademicYearId == academicYearId &&
			x.IsActive &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active);
		if (existing != null)
		{
			return existing.Id;
		}

		var suffix = Guid.NewGuid().ToString("N")[..12];
		var leavePolicy = new LeavePolicy(
			guidGenerator.Create(),
			tenantId,
			academicYearId,
			$"AP Policy {suffix}",
			effectiveFrom,
			effectiveTo,
			HrPolicyStatus.Active,
			isDefault: true);
		var leavePolicyRule = new LeavePolicyRule(
			guidGenerator.Create(),
			tenantId,
			leavePolicy.Id,
			LeavePaidType.Paid,
			LeaveEntitlementMode.Limited,
			LeaveAccrualPolicyType.Yearly,
			12,
			carryForward: false,
			carryForwardLimit: null,
			encashable: false,
			encashmentTiming: null,
			requiresApproval: true,
			minNoticeDays: 0,
			maxDaysPerRequest: 30,
			allowHalfDay: true,
			attachmentRequiredAfterDays: null,
			allowNegativeBalance: false);
		var unpaidRule = new LeavePolicyRule(
			guidGenerator.Create(),
			tenantId,
			leavePolicy.Id,
			LeavePaidType.Unpaid,
			LeaveEntitlementMode.Unlimited,
			LeaveAccrualPolicyType.Yearly,
			null,
			carryForward: false,
			carryForwardLimit: null,
			encashable: false,
			encashmentTiming: null,
			requiresApproval: true,
			minNoticeDays: 0,
			maxDaysPerRequest: 30,
			allowHalfDay: true,
			attachmentRequiredAfterDays: null,
			allowNegativeBalance: true);
		leavePolicy.ReplaceRules([leavePolicyRule, unpaidRule]);
		await leavePolicyRepository.InsertAsync(leavePolicy, autoSave: false);
		return leavePolicy.Id;
	}

	private async Task<Guid> InsertShiftAsync(
		string shiftName,
		TimeOnly startTime,
		TimeOnly endTime)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var shiftRepository = scope.ServiceProvider.GetRequiredService<IRepository<ShiftManagement, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var shift = await shiftRepository.InsertAsync(new ShiftManagement
			{
				ShiftName = shiftName,
				StartTime = startTime,
				EndTime = endTime
			}, autoSave: false);
			await uow.CompleteAsync();
			return shift.Id;
		}
	}

	private async Task AssignEmployeeShiftAsync(Guid employeeId, Guid shiftId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var employeeRepository = scope.ServiceProvider.GetRequiredService<IRepository<Employee, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var employee = await employeeRepository.GetAsync(employeeId);
			employee.ShiftId = shiftId;
			await employeeRepository.UpdateAsync(employee, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task<Guid> InsertLeaveForAttendanceAsync(
		Guid employeeId,
		DateOnly startDate,
		DateOnly endDate,
		EmployeeLeaveStatusType status,
		bool halfDay = false)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var guidGenerator = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var leavePolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<LeavePolicy, Guid>>();
		var leaveRepository = scope.ServiceProvider.GetRequiredService<IEmployeeLeaveRepository>();
		var leaveManager = scope.ServiceProvider.GetRequiredService<EmployeeLeaveManager>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
			var academicYearQuery = await academicYearRepository.GetQueryableAsync();
			var academicYear = academicYearQuery.Single(x => x.StartDate <= startDateTime && x.EndDate >= startDateTime);
			var suffix = Guid.NewGuid().ToString("N")[..12];
			var leavePolicy = new LeavePolicy(
				guidGenerator.Create(),
				tenantId,
				academicYear.Id,
				$"AL Policy {suffix}",
				academicYear.StartDate,
				academicYear.EndDate,
				HrPolicyStatus.Active);
			var leavePolicyRule = new LeavePolicyRule(
				guidGenerator.Create(),
				tenantId,
				leavePolicy.Id,
				LeavePaidType.Paid,
				LeaveEntitlementMode.Limited,
				LeaveAccrualPolicyType.Yearly,
				12,
				carryForward: false,
				carryForwardLimit: null,
				encashable: false,
				encashmentTiming: null,
				requiresApproval: true,
				minNoticeDays: 0,
				maxDaysPerRequest: 30,
				allowHalfDay: true,
				attachmentRequiredAfterDays: null,
				allowNegativeBalance: false);
			var unpaidRule = new LeavePolicyRule(
				guidGenerator.Create(),
				tenantId,
				leavePolicy.Id,
				LeavePaidType.Unpaid,
				LeaveEntitlementMode.Unlimited,
				LeaveAccrualPolicyType.Yearly,
				null,
				carryForward: false,
				carryForwardLimit: null,
				encashable: false,
				encashmentTiming: null,
				requiresApproval: true,
				minNoticeDays: 0,
				maxDaysPerRequest: 30,
				allowHalfDay: true,
				attachmentRequiredAfterDays: null,
				allowNegativeBalance: true);
			leavePolicy.ReplaceRules([leavePolicyRule, unpaidRule]);
			await leavePolicyRepository.InsertAsync(leavePolicy, autoSave: false);

			var start = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
			var end = new DateTimeOffset(endDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
			var leave = EmployeeLeave.Create(
				guidGenerator.Create(),
				tenantId,
				employeeId,
				leavePolicy.Id,
				leavePolicyRule.Id,
				LeavePaidType.Paid,
				start,
				end,
				"Attendance approved leave overlay test",
				halfDay);
			var leaveQuery = await leaveRepository.GetQueryableAsync();
			var nextPublicId = (leaveQuery.Select(x => (int?)x.PublicId).Max() ?? 0) + 1;
			typeof(EmployeeLeave)
				.GetProperty(nameof(EmployeeLeave.PublicId))!
				.SetValue(leave, nextPublicId);

			await leaveRepository.InsertAsync(leave, autoSave: false);
			if (status == EmployeeLeaveStatusType.Approved)
			{
				await leaveManager.ApproveAsync(leave, LeavePaidType.Paid, halfDay ? 0.5m : 1m);
			}
			else if (status == EmployeeLeaveStatusType.Cancelled)
			{
				await leaveManager.CancelAsync(leave);
			}
			else if (status != EmployeeLeaveStatusType.Pending)
			{
				await leaveManager.UpdateLeaveRequestStatusAsync(
					leave,
					status,
					status == EmployeeLeaveStatusType.Rejected ? "Rejected" : null);
			}

			await uow.CompleteAsync();
			return leave.Id;
		}
	}

	private async Task InsertAttendanceForTestAsync(
		Guid employeeId,
		DateOnly attendanceDate,
		EmployeeAttendanceStatusType status,
		string? remarks = null)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var attendanceRepository = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await attendanceRepository.InsertAsync(new EmployeeAttendance
			{
				TenantId = tenantId,
				EmployeeId = employeeId,
				AttendanceDate = attendanceDate,
				StatusType = status,
				Source = AttendanceRecordSource.Manual,
				Remarks = remarks
			}, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task<EmployeeAttendance> GetAttendanceAsync(Guid employeeId, DateOnly attendanceDate)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var attendanceRepository = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var query = await attendanceRepository.GetQueryableAsync();
			var attendance = query.Single(x => x.EmployeeId == employeeId && x.AttendanceDate == attendanceDate);
			await uow.CompleteAsync();
			return attendance;
		}
	}
}
