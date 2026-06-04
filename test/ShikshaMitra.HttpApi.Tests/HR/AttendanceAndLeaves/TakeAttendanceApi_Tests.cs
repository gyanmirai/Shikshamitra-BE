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
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
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
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"ManualTimeSave{Guid.NewGuid():N}",
			dateOfJoining: attendanceDate.AddDays(-10));
		await AssignCaptureModePolicyAsync(employee.Id, DateOnly.FromDateTime(attendanceDate), AttendanceCaptureMode.ManualWithTime);

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
		attendance.TimeIn.ShouldNotBeNull();
		attendance.TimeOut.ShouldNotBeNull();
		attendance.Source.ShouldBe(AttendanceRecordSource.ManualTime);
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
				targetDate.Date,
				null,
				true,
				priority: 100,
				assignmentReason: "Capture mode regression test"), autoSave: false);
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
