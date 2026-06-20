using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class AttendanceRegisterApi_Tests : ShikshaMitraApiTestBase
{
	private const string RegisterUrl = "/api/hr/attendance/register";
	private const string TakeAttendanceUrl = "/api/hr/attendance/take-attendance";

	public AttendanceRegisterApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Register_Should_Mark_Holiday_Attendance_As_Non_Editable()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"RegHoliday{Guid.NewGuid():N}";
		var holidayDate = new DateOnly(2026, 5, 10);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			marker,
			dateOfJoining: holidayDate.ToDateTime(TimeOnly.MinValue).AddDays(-1));
		await SeedAttendanceAsync(employee.Id, holidayDate, EmployeeTestHelper.DefaultMediumStageId);

		var response = await Client.GetAsync(
			$"{RegisterUrl}?startDate=2026-05-10&endDate=2026-05-10&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<AttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize register response.");
		var row = payload.Items.Single(x => x.EmployeeId == employee.Id);

		row.IsHoliday.ShouldBeTrue();
		row.HolidayName.ShouldNotBeNull();
		row.HolidayName.ShouldContain("Summer Vacation");
		row.HolidayReason.ShouldNotBeNull();
		row.HolidayReason.ShouldContain("Holiday:");
		row.CanEdit.ShouldBeFalse();
		row.LockReason.ShouldNotBeNull();
		row.LockReason.ShouldContain("Holiday:");
		payload.Summary.Holiday.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Bulk_Adjust_Should_Skip_Holiday_Attendance_Rows()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"RegHolidayBulk{Guid.NewGuid():N}";
		var holidayDate = new DateOnly(2026, 5, 10);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			marker,
			dateOfJoining: holidayDate.ToDateTime(TimeOnly.MinValue).AddDays(-1));
		await SeedAttendanceAsync(employee.Id, holidayDate, EmployeeTestHelper.DefaultMediumStageId);

		var registerResponse = await Client.GetAsync(
			$"{RegisterUrl}?startDate=2026-05-10&endDate=2026-05-10&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		registerResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var register = await registerResponse.Content.ReadFromJsonAsync<AttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize register response.");
		var row = register.Items.Single(x => x.EmployeeId == employee.Id);
		row.AttendanceId.ShouldNotBeNull();

		var adjustResponse = await Client.PostAsJsonAsync($"{RegisterUrl}/bulk-adjust", new AttendanceRegisterBulkAdjustRequest
		{
			AttendanceIds = [row.AttendanceId!.Value],
			StatusWire = "ABSENT",
			Reason = "Holiday bulk adjust should skip"
		}, JsonOptions);
		adjustResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var result = await adjustResponse.Content.ReadFromJsonAsync<AttendanceRegisterAdjustmentResult>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize adjustment result.");
		result.UpdatedCount.ShouldBe(0);
		result.SkippedCount.ShouldBe(1);
		var skippedRow = result.Rows.Single();
		skippedRow.Reason.ShouldNotBeNull();
		skippedRow.Reason.ShouldContain("Holiday:");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Register_Should_Not_Count_Manual_Status_Present_Without_Punch_As_Missing_Punch()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"RegManualStatus{Guid.NewGuid():N}";
		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			marker,
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));
		await AssignCaptureModePolicyAsync(employee.Id, attendanceDate, AttendanceCaptureMode.ManualStatus);
		await SeedAttendanceAsync(employee.Id, attendanceDate, EmployeeTestHelper.DefaultMediumStageId);

		var response = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");
		var group = payload.Items.Single(x => x.EmployeeId == employee.Id);

		group.Days.Single().CaptureMode.ShouldBe("manual_status");
		group.MissingPunch.ShouldBe(0);
		group.HasExceptions.ShouldBeFalse();
		payload.Summary.MissingPunch.ShouldBe(0);

		var filterResponse = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}&exceptionFilter=MissingPunch&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		filterResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var filtered = await filterResponse.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");
		filtered.TotalCount.ShouldBe(0);
		filtered.Items.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Register_Should_Not_Count_Manual_Status_Csv_Or_Integration_Raw_Punches_As_Missing_Punch()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"RegManualRaw{Guid.NewGuid():N}";
		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
		var csvEmployee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"{marker}Csv",
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));
		var integrationEmployee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"{marker}Integration",
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));
		await AssignCaptureModePolicyAsync(csvEmployee.Id, attendanceDate, AttendanceCaptureMode.ManualStatus);
		await AssignCaptureModePolicyAsync(integrationEmployee.Id, attendanceDate, AttendanceCaptureMode.ManualStatus);
		await SeedAttendanceAsync(
			csvEmployee.Id,
			attendanceDate,
			EmployeeTestHelper.DefaultMediumStageId,
			AttendanceRecordSource.CsvImport,
			new TimeOnly(9, 0),
			new TimeOnly(17, 30));
		await SeedAttendanceAsync(
			integrationEmployee.Id,
			attendanceDate,
			EmployeeTestHelper.DefaultMediumStageId,
			AttendanceRecordSource.Integration,
			new TimeOnly(9, 5),
			new TimeOnly(17, 35));

		var response = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");

		payload.Summary.MissingPunch.ShouldBe(0);
		payload.Items.Count.ShouldBe(2);
		payload.Items.ShouldAllBe(x => x.MissingPunch == 0 && !x.HasExceptions);
		var csvRow = payload.Items.Single(x => x.EmployeeId == csvEmployee.Id).Days.Single();
		csvRow.Source.ShouldBe("csv_import");
		csvRow.InTime.ShouldNotBeNull();
		csvRow.OutTime.ShouldNotBeNull();
		var integrationRow = payload.Items.Single(x => x.EmployeeId == integrationEmployee.Id).Days.Single();
		integrationRow.Source.ShouldBe("integration");
		integrationRow.InTime.ShouldNotBeNull();
		integrationRow.OutTime.ShouldNotBeNull();

		var filterResponse = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}&exceptionFilter=MissingPunch&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		filterResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var filtered = await filterResponse.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");
		filtered.TotalCount.ShouldBe(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Adjust_Should_Strip_Times_For_Manual_Status_Policy()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"RegManualStatusAdjust{Guid.NewGuid():N}";
		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			marker,
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));
		await AssignCaptureModePolicyAsync(employee.Id, attendanceDate, AttendanceCaptureMode.ManualStatus);

		var adjustResponse = await Client.PostAsJsonAsync($"{RegisterUrl}/adjust", new AttendanceRegisterAdjustRequest
		{
			EmployeeId = employee.Id,
			AttendanceDate = attendanceDate.ToString("yyyy-MM-dd"),
			StatusWire = "PRESENT",
			InTime = "09:00:00",
			OutTime = "17:00:00",
			Reason = "Manual status adjustment should strip time"
		}, JsonOptions);
		adjustResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var attendance = await GetAttendanceAsync(employee.Id, attendanceDate);
		attendance.TimeIn.ShouldBeNull();
		attendance.TimeOut.ShouldBeNull();
		attendance.Source.ShouldBe(AttendanceRecordSource.Correction);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Adjust_Should_Reject_Timestamp_AttendanceDate()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"RegTimestampAdjust{Guid.NewGuid():N}");

		var adjustResponse = await Client.PostAsJsonAsync($"{RegisterUrl}/adjust", new AttendanceRegisterAdjustRequest
		{
			EmployeeId = employee.Id,
			AttendanceDate = "2026-04-17T22:15:34-07:00",
			StatusWire = "PRESENT",
			InTime = "09:00:00",
			OutTime = "17:00:00",
			Reason = "Timestamp date should be rejected"
		}, JsonOptions);

		adjustResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var payload = await adjustResponse.Content.ReadAsStringAsync();
		payload.ShouldContain(HRErrorCodes.AttendanceInvalidDateFormat);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Grouped_Register_Should_Reject_Timestamp_Date_Range()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate=2026-04-17T22:15:34-07:00&endDate=2026-04-17&skipCount=0&maxResultCount=50");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var payload = await response.Content.ReadAsStringAsync();
		payload.ShouldContain(HRErrorCodes.AttendanceInvalidDateFormat);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Grouped_Register_Should_Count_Missing_Punch_For_Time_Based_Policies()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"RegTimePunch{Guid.NewGuid():N}";
		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
		var manualTimeEmployee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"{marker}ManualTime",
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));
		var integratedEmployee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"{marker}Integrated",
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));
		await AssignCaptureModePolicyAsync(manualTimeEmployee.Id, attendanceDate, AttendanceCaptureMode.ManualWithTime);
		await AssignCaptureModePolicyAsync(integratedEmployee.Id, attendanceDate, AttendanceCaptureMode.Integrated);
		await SeedAttendanceAsync(manualTimeEmployee.Id, attendanceDate, EmployeeTestHelper.DefaultMediumStageId);
		await SeedAttendanceAsync(integratedEmployee.Id, attendanceDate, EmployeeTestHelper.DefaultMediumStageId);

		var response = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");

		payload.Summary.MissingPunch.ShouldBe(2);
		payload.Items.Single(x => x.EmployeeId == manualTimeEmployee.Id).MissingPunch.ShouldBe(1);
		payload.Items.Single(x => x.EmployeeId == integratedEmployee.Id).MissingPunch.ShouldBe(1);

		var filterResponse = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}&exceptionFilter=MissingPunch&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		filterResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var filtered = await filterResponse.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");
		filtered.TotalCount.ShouldBe(2);
		filtered.Items.Select(x => x.EmployeeId).ShouldBe([manualTimeEmployee.Id, integratedEmployee.Id], ignoreOrder: true);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Grouped_Register_Should_Count_All_Scoped_Employees_Including_NotMarked()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"RegCount{Guid.NewGuid():N}";
		var attendanceDate = DateTime.UtcNow.AddDays(-1);
		var attendanceDateText = attendanceDate.ToString("dd-MM-yyyy");
		var startEnd = attendanceDate.ToString("yyyy-MM-dd");
		var presentEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"{marker}Present", dateOfJoining: attendanceDate.AddDays(-10));
		var absentEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"{marker}Absent", dateOfJoining: attendanceDate.AddDays(-10));
		var notMarkedEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"{marker}NotMarked", dateOfJoining: attendanceDate.AddDays(-10));

		var saveResponse = await Client.PostAsJsonAsync($"{TakeAttendanceUrl}/day/save", new TakeAttendanceSaveInput
		{
			AttendanceDate = attendanceDateText,
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = presentEmployee.Id,
					StatusWire = "PRESENT",
					InTime = "09:00:00",
					OutTime = "17:00:00"
				},
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = absentEmployee.Id,
					StatusWire = "ABSENT"
				}
			]
		}, JsonOptions);
		saveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var response = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={startEnd}&endDate={startEnd}&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");

		payload.TotalCount.ShouldBe(3);
		payload.Items.Count.ShouldBe(3);
		payload.Summary.StaffCount.ShouldBe(3);
		payload.Summary.Present.ShouldBe(1);
		payload.Summary.Absent.ShouldBe(1);
		payload.Summary.NotMarked.ShouldBe(1);

		var notMarked = payload.Items.Single(x => x.EmployeeId == notMarkedEmployee.Id).Days.Single();
		notMarked.Status.ShouldBe(AttendanceRegisterStatus.NotMarked);
		notMarked.IsMarked.ShouldBeFalse();
		notMarked.AttendanceId.ShouldBeNull();
		notMarked.CanEdit.ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Grouped_Register_Should_Fallback_When_Employee_Policy_Assignment_Is_Broken()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"RegBrokenPolicy{Guid.NewGuid():N}";
		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
		var healthyEmployee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"{marker}Healthy",
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));
		var brokenEmployee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"{marker}Broken",
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));
		await SeedAttendanceAsync(healthyEmployee.Id, attendanceDate, EmployeeTestHelper.DefaultMediumStageId);
		await SeedAttendanceAsync(brokenEmployee.Id, attendanceDate, EmployeeTestHelper.DefaultMediumStageId);
		await AssignBrokenEmployeePolicyAsync(brokenEmployee.Id, attendanceDate);

		var response = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");
		var brokenDay = payload.Items.Single(x => x.EmployeeId == brokenEmployee.Id).Days.Single();

		payload.TotalCount.ShouldBe(2);
		brokenDay.Status.ShouldBe(AttendanceRegisterStatus.Present);
		brokenDay.CaptureMode.ShouldBe("integrated");
		payload.Items.Single(x => x.EmployeeId == healthyEmployee.Id).Days.Single().Status.ShouldBe(AttendanceRegisterStatus.Present);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Adjust_Should_Create_Attendance_For_Unlocked_NotMarked_Row()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"RegCreate{Guid.NewGuid():N}";
		var attendanceDate = DateTime.UtcNow.AddDays(-1);
		var startEnd = attendanceDate.ToString("yyyy-MM-dd");
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, marker, dateOfJoining: attendanceDate.AddDays(-10));

		var registerResponse = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={startEnd}&endDate={startEnd}&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		registerResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var register = await registerResponse.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");

		var notMarked = register.Items.Single(x => x.EmployeeId == employee.Id).Days.Single();
		notMarked.Status.ShouldBe(AttendanceRegisterStatus.NotMarked);
		notMarked.AttendanceId.ShouldBeNull();
		notMarked.CanEdit.ShouldBeTrue();

		var adjustResponse = await Client.PostAsJsonAsync($"{RegisterUrl}/adjust", new AttendanceRegisterAdjustRequest
		{
			EmployeeId = employee.Id,
			AttendanceDate = startEnd,
			StatusWire = "PRESENT",
			InTime = "09:00:00",
			OutTime = "17:00:00",
			Reason = "Mark missing register day"
		}, JsonOptions);
		adjustResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var result = await adjustResponse.Content.ReadFromJsonAsync<AttendanceRegisterAdjustmentResult>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize adjustment result.");
		result.UpdatedCount.ShouldBe(1);
		result.SkippedCount.ShouldBe(0);
		result.Rows.Single().EmployeeId.ShouldBe(employee.Id);

		var afterResponse = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={startEnd}&endDate={startEnd}&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		afterResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var after = await afterResponse.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");
		var marked = after.Items.Single(x => x.EmployeeId == employee.Id).Days.Single();
		marked.Status.ShouldBe(AttendanceRegisterStatus.Present);
		marked.IsMarked.ShouldBeTrue();
		marked.AttendanceId.ShouldNotBeNull();
		marked.CanEdit.ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Grouped_Register_Status_Filter_Should_Exclude_NotMarked_Rows()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"RegStatus{Guid.NewGuid():N}";
		var attendanceDate = DateTime.UtcNow.AddDays(-1);
		var attendanceDateText = attendanceDate.ToString("dd-MM-yyyy");
		var startEnd = attendanceDate.ToString("yyyy-MM-dd");
		var presentEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"{marker}Present", dateOfJoining: attendanceDate.AddDays(-10));
		await EmployeeTestHelper.CreateEmployeeAsync(Client, $"{marker}NotMarked", dateOfJoining: attendanceDate.AddDays(-10));

		var saveResponse = await Client.PostAsJsonAsync($"{TakeAttendanceUrl}/day/save", new TakeAttendanceSaveInput
		{
			AttendanceDate = attendanceDateText,
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = presentEmployee.Id,
					StatusWire = "PRESENT",
					InTime = "09:00:00",
					OutTime = "17:00:00"
				}
			]
		}, JsonOptions);
		saveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var response = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={startEnd}&endDate={startEnd}&status=Present&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");

		payload.TotalCount.ShouldBe(1);
		payload.Items.Single().EmployeeId.ShouldBe(presentEmployee.Id);
		payload.Summary.StaffCount.ShouldBe(1);
		payload.Summary.Present.ShouldBe(1);
		payload.Summary.NotMarked.ShouldBe(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Grouped_Register_Search_Should_Match_Attendance_Remarks()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"RegRemark{Guid.NewGuid():N}";
		var attendanceDate = DateTime.UtcNow.AddDays(-1);
		var attendanceDateText = attendanceDate.ToString("dd-MM-yyyy");
		var startEnd = attendanceDate.ToString("yyyy-MM-dd");
		var matchedEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "Remark Search Matched", dateOfJoining: attendanceDate.AddDays(-10));
		await EmployeeTestHelper.CreateEmployeeAsync(Client, "Remark Search Unmarked", dateOfJoining: attendanceDate.AddDays(-10));

		var saveResponse = await Client.PostAsJsonAsync($"{TakeAttendanceUrl}/day/save", new TakeAttendanceSaveInput
		{
			AttendanceDate = attendanceDateText,
			Entries =
			[
				new TakeAttendanceSaveEntryDto
				{
					EmployeeId = matchedEmployee.Id,
					StatusWire = "PRESENT",
					InTime = "09:00:00",
					OutTime = "17:00:00",
					Remarks = marker
				}
			]
		}, JsonOptions);
		saveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var response = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={startEnd}&endDate={startEnd}&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");

		payload.TotalCount.ShouldBe(1);
		payload.Items.Single().EmployeeId.ShouldBe(matchedEmployee.Id);
		payload.Items.Single().Days.Single().Remarks.ShouldBe(marker);
		payload.Summary.StaffCount.ShouldBe(1);
		payload.Summary.NotMarked.ShouldBe(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Grouped_Register_Should_Include_New_Employee_After_Academic_Permission_Is_Saved()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"RegAcademicPermission{Guid.NewGuid():N}";
		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			marker,
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));

		var response = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}&academicYearId={Factory.SeededAcademicYearId:D}&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");
		var day = payload.Items.Single(x => x.EmployeeId == employee.Id).Days.Single();

		payload.TotalCount.ShouldBe(1);
		payload.Summary.StaffCount.ShouldBe(1);
		payload.Summary.NotMarked.ShouldBe(1);
		day.Status.ShouldBe(AttendanceRegisterStatus.NotMarked);
		day.EducationMediumStageId.ShouldBe(EmployeeTestHelper.DefaultMediumStageId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Grouped_Register_Should_Show_Take_Attendance_Data_When_AcademicYear_Filter_Is_Selected()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var marker = $"RegTakenYear{Guid.NewGuid():N}";
		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			marker,
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));

		await SeedAttendanceAsync(employee.Id, attendanceDate, EmployeeTestHelper.DefaultMediumStageId);

		var response = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}&academicYearId={Factory.SeededAcademicYearId:D}&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");
		var day = payload.Items.Single(x => x.EmployeeId == employee.Id).Days.Single();

		day.Status.ShouldBe(AttendanceRegisterStatus.Present);
		day.IsMarked.ShouldBeTrue();
		day.AttendanceId.ShouldNotBeNull();
		payload.Summary.Present.ShouldBe(1);
		payload.Summary.NotMarked.ShouldBe(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Grouped_Register_Should_Show_Attendance_With_Stale_Stage_When_AcademicYear_Filter_Is_Selected()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var seeded = await SeedRegisterAcademicOrganizationsAsync();
		var marker = $"RegYearScope{Guid.NewGuid():N}";
		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			marker,
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));

		await AssignEmployeeToStageAsync(employee.Id, seeded.CurrentYearStageId);
		await AssignEmployeeToStageAsync(employee.Id, seeded.OtherYearStageId);
		await SeedAttendanceAsync(employee.Id, attendanceDate, seeded.OtherYearStageId);

		var response = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}&academicYearId={seeded.AcademicYearId:D}&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");
		var day = payload.Items.Single(x => x.EmployeeId == employee.Id).Days.Single();

		day.Status.ShouldBe(AttendanceRegisterStatus.Present);
		day.IsMarked.ShouldBeTrue();
		day.AttendanceId.ShouldNotBeNull();
		day.EducationMediumStageId.ShouldBe(seeded.CurrentYearStageId);
		payload.Summary.Present.ShouldBe(1);
		payload.Summary.NotMarked.ShouldBe(0);

		var stageResponse = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}&educationMediumStageId={seeded.CurrentYearStageId:D}&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		stageResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var stagePayload = await stageResponse.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");
		var stageDay = stagePayload.Items.Single(x => x.EmployeeId == employee.Id).Days.Single();

		stageDay.Status.ShouldBe(AttendanceRegisterStatus.Present);
		stageDay.IsMarked.ShouldBeTrue();
		stageDay.AttendanceId.ShouldNotBeNull();
		stageDay.EducationMediumStageId.ShouldBe(seeded.CurrentYearStageId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Grouped_Register_Should_Show_Selected_Employee_When_AcademicYear_Assignment_Is_Missing()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var seeded = await SeedRegisterAcademicOrganizationsAsync();
		var marker = $"RegYearMissing{Guid.NewGuid():N}";
		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			marker,
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));

		await AssignEmployeeToStageAsync(employee.Id, seeded.OtherYearStageId);
		await SeedAttendanceAsync(employee.Id, attendanceDate, seeded.OtherYearStageId);

		var response = await Client.GetAsync(
			$"{RegisterUrl}/grouped?startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}&academicYearId={seeded.AcademicYearId:D}&employeeId={employee.Id:D}&skipCount=0&maxResultCount=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");
		var day = payload.Items.Single(x => x.EmployeeId == employee.Id).Days.Single();

		payload.TotalCount.ShouldBe(1);
		day.Status.ShouldBe(AttendanceRegisterStatus.Present);
		day.IsMarked.ShouldBeTrue();
		day.AttendanceId.ShouldNotBeNull();
		payload.Summary.Present.ShouldBe(1);
		payload.Summary.NotMarked.ShouldBe(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Grouped_Register_Should_Only_Return_Employees_In_UserScope_Stage()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var seeded = await SeedRegisterAcademicOrganizationsAsync();
		var marker = $"RegScope{Guid.NewGuid():N}";
		var attendanceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
		var inScopeEmployee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"{marker}In",
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));
		var outOfScopeEmployee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"{marker}Out",
			dateOfJoining: attendanceDate.ToDateTime(TimeOnly.MinValue).AddDays(-10));

		await AssignEmployeeToStageAsync(inScopeEmployee.Id, seeded.CurrentYearStageId);
		await AssignEmployeeToStageAsync(outOfScopeEmployee.Id, seeded.OtherYearStageId);
		await SeedAttendanceAsync(inScopeEmployee.Id, attendanceDate, seeded.CurrentYearStageId);
		await SeedAttendanceAsync(outOfScopeEmployee.Id, attendanceDate, seeded.OtherYearStageId);

		var scopedUserId = Guid.NewGuid();
		await EnsureUserHasStageScopeAsync(scopedUserId, seeded.CurrentYearStageId);
		var scopedClient = CreateClientAs(scopedUserId);

		var response = await scopedClient.GetAsync(
			$"{RegisterUrl}/grouped?startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<GroupedAttendanceRegisterResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize grouped register response.");

		payload.TotalCount.ShouldBe(1);
		payload.Items.Single().EmployeeId.ShouldBe(inScopeEmployee.Id);
		payload.Items.Single().Days.Single().EducationMediumStageId.ShouldBe(seeded.CurrentYearStageId);
		payload.Items.ShouldNotContain(x => x.EmployeeId == outOfScopeEmployee.Id);
		payload.Summary.StaffCount.ShouldBe(1);
		payload.Summary.Present.ShouldBe(1);

		var deniedResponse = await scopedClient.GetAsync(
			$"{RegisterUrl}/grouped?startDate={attendanceDate:yyyy-MM-dd}&endDate={attendanceDate:yyyy-MM-dd}&educationMediumStageId={seeded.OtherYearStageId:D}&search={Uri.EscapeDataString(marker)}&skipCount=0&maxResultCount=50");
		deniedResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
		if (deniedResponse.StatusCode == HttpStatusCode.BadRequest)
		{
			var deniedPayload = await deniedResponse.Content.ReadAsStringAsync();
			deniedPayload.ShouldContain(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}
	}

	private async Task SeedAttendanceAsync(
		Guid employeeId,
		DateOnly attendanceDate,
		Guid? educationMediumStageId,
		AttendanceRecordSource source = AttendanceRecordSource.Manual,
		TimeOnly? timeIn = null,
		TimeOnly? timeOut = null)
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
				EducationMediumStageId = educationMediumStageId,
				StatusType = EmployeeAttendanceStatusType.Present,
				Source = source,
				TimeIn = timeIn.HasValue ? new DateTimeOffset(attendanceDate.ToDateTime(timeIn.Value), TimeSpan.Zero) : null,
				TimeOut = timeOut.HasValue ? new DateTimeOffset(attendanceDate.ToDateTime(timeOut.Value), TimeSpan.Zero) : null,
				Remarks = "Register row"
			}, autoSave: false);
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
		var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
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
				null,
				targetDate.Date,
				null,
				true,
				priority: 100,
				assignmentReason: "Capture mode regression test"), autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task AssignBrokenEmployeePolicyAsync(Guid employeeId, DateOnly effectiveDate)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var targetDate = effectiveDate.ToDateTime(TimeOnly.MinValue);
			var academicYearQuery = await academicYearRepository.GetQueryableAsync();
			var academicYear = academicYearQuery.Single(x => x.StartDate <= targetDate && x.EndDate >= targetDate);

			await assignmentRepository.InsertAsync(new HrPolicyAssignment(
				Guid.NewGuid(),
				tenantId,
				academicYear.Id,
				HrPolicyAssignmentTargetType.Employee,
				employeeId.ToString("D"),
				null,
				Guid.NewGuid(),
				Guid.NewGuid(),
				null,
				targetDate.Date,
				null,
				true,
				priority: 500,
				assignmentReason: "Broken assignment regression test"), autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task AssignEmployeeToStageAsync(Guid employeeId, Guid stageId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var assignmentRepository =
			scope.ServiceProvider.GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var exists = await assignmentRepository.AnyAsync(x =>
				x.EmployeeId == employeeId &&
				x.EducationMediumStageId == stageId);
			if (!exists)
			{
				await assignmentRepository.InsertAsync(
					new EmployeeOrganizationAssignment(Guid.NewGuid(), tenantId, employeeId, stageId),
					autoSave: false);
			}
			await uow.CompleteAsync();
		}
	}

	private async Task<RegisterAcademicOrganizations> SeedRegisterAcademicOrganizationsAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
			var boardRepository = scope.ServiceProvider.GetRequiredService<IEducationBoardRepository>();
			var mediumRepository = scope.ServiceProvider.GetRequiredService<IEducationMediumRepository>();
			var stageRepository = scope.ServiceProvider.GetRequiredService<IEducationMediumStageRepository>();
			var academicYearManager = scope.ServiceProvider.GetRequiredService<AcademicYearManager>();
			var boardManager = scope.ServiceProvider.GetRequiredService<EducationBoardManager>();
			var mediumManager = scope.ServiceProvider.GetRequiredService<EducationMediumManager>();
			var stageManager = scope.ServiceProvider.GetRequiredService<EducationMediumStageManager>();

			var suffix = Guid.NewGuid().ToString("N")[..8];
			var academicYear = await academicYearManager.CreateAsync(
				$"RegYear-{suffix}",
				new DateTime(2035, 4, 1),
				new DateTime(2036, 3, 31));
			await academicYearRepository.InsertAsync(academicYear, autoSave: false);

			var otherYear = await academicYearManager.CreateAsync(
				$"RegOther-{suffix}",
				new DateTime(2036, 4, 1),
				new DateTime(2037, 3, 31));
			await academicYearRepository.InsertAsync(otherYear, autoSave: false);

			var board = await boardManager.CreateAsync(academicYear.Id, EducationBoardType.GSEB);
			await boardRepository.InsertAsync(board, autoSave: false);
			var otherBoard = await boardManager.CreateAsync(otherYear.Id, EducationBoardType.CBSE);
			await boardRepository.InsertAsync(otherBoard, autoSave: false);

			var medium = await mediumManager.CreateAsync(board.Id, $"Gujarati-{suffix}");
			await mediumRepository.InsertAsync(medium, autoSave: false);
			var otherMedium = await mediumManager.CreateAsync(otherBoard.Id, $"English-{suffix}");
			await mediumRepository.InsertAsync(otherMedium, autoSave: false);

			var stage = await stageManager.CreateAsync(medium.Id, $"Primary-{suffix}");
			await stageRepository.InsertAsync(stage, autoSave: false);
			var otherStage = await stageManager.CreateAsync(otherMedium.Id, $"Secondary-{suffix}");
			await stageRepository.InsertAsync(otherStage, autoSave: false);

			await uow.CompleteAsync();
			return new RegisterAcademicOrganizations(academicYear.Id, stage.Id, otherStage.Id);
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

	private sealed record RegisterAcademicOrganizations(
		Guid AcademicYearId,
		Guid CurrentYearStageId,
		Guid OtherYearStageId);
}
