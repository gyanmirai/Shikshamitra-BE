using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
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
		notMarked.CanEdit.ShouldBeFalse();
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

	private async Task SeedAttendanceAsync(Guid employeeId, DateOnly attendanceDate, Guid? educationMediumStageId)
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
				Remarks = "Register row"
			}, autoSave: false);
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
}
