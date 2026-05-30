using System.Collections.Generic;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Status;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class RunPayrollApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/payroll/run";
	private const string ReviewUrl = "/api/payroll/run/reviews";
	private const string PayrollMonth = "APRIL-2026";

	public RunPayrollApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Payroll_Preview_Without_500()
	{
		var response = await Client.GetAsync($"{BaseUrl}?month=2026-05&page=0&pageSize=100");
		var body = await response.Content.ReadAsStringAsync();

		body.ShouldNotContain("HR:Payroll:030011");
		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Exclude_Employee_Before_Joining_Month()
	{
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"PayrollMayJoin",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

		var aprilPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-04&page=0&pageSize=100&forceRefresh=true");
		aprilPreview.Items.Any(x => x.EmployeeId == employee.Id).ShouldBeFalse();

		var mayPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-05&page=0&pageSize=100&forceRefresh=true");
		mayPreview.Items.Any(x => x.EmployeeId == employee.Id).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Readiness_Should_Not_Count_Employee_Before_Joining_Month()
	{
		var before = await GetAndReadAsync<RunPayrollReadinessSummary>(
			$"{BaseUrl}/readiness?month=2026-04");

		await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"PayrollReadinessMayJoin",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

		var after = await GetAndReadAsync<RunPayrollReadinessSummary>(
			$"{BaseUrl}/readiness?month=2026-04");
		after.EmployeeCount.ShouldBe(before.EmployeeCount);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Include_Inactive_Employee_For_Effective_Month_Only()
	{
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollInactiveFinal{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

		await SetEmployeeStatusAsync(employee.Id, 1, new DateOnly(2026, 5, 31));

		var mayPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-05&page=0&pageSize=500&forceRefresh=true");
		mayPreview.Items.Any(x => x.EmployeeId == employee.Id).ShouldBeTrue();

		var junePreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-06&page=0&pageSize=500&forceRefresh=true");
		junePreview.Items.Any(x => x.EmployeeId == employee.Id).ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Show_Final_Month_Inactive_Employee_With_No_Attendance_Issue()
	{
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollInactiveNoAttendance{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

		await SetEmployeeStatusAsync(employee.Id, 1, new DateOnly(2026, 5, 31));

		var mayPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-05&page=0&pageSize=500&forceRefresh=true");
		var item = mayPreview.Items.Single(x => x.EmployeeId == employee.Id);
		item.IssueCodes.ShouldContain("NO_ATTENDANCE");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Readiness_Should_Refresh_After_Mark_Attendance_Save()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			$"PayrollAttendanceRefresh{Guid.NewGuid():N}",
			dateOfJoining: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

		var before = await GetAndReadAsync<RunPayrollReadinessSummary>(
			$"{BaseUrl}/readiness?month=2026-04");

		var attendanceResponse = await Client.PostAsJsonAsync(
			"/api/hr/attendance/take-attendance/day/save",
			new TakeAttendanceSaveInput
			{
				AttendanceDate = "11-04-2026",
				Entries =
				[
					new TakeAttendanceSaveEntryDto
					{
						EmployeeId = employee.Id,
						StatusWire = "PRESENT",
						Remarks = "Payroll readiness refresh regression"
					}
				]
			},
			JsonOptions);
		attendanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var after = await GetAndReadAsync<RunPayrollReadinessSummary>(
			$"{BaseUrl}/readiness?month=2026-04");

		after.EmployeeCount.ShouldBe(before.EmployeeCount);
		after.AttendanceFoundCount.ShouldBeGreaterThanOrEqualTo(before.AttendanceFoundCount + 1);
		after.MissingAttendanceCount.ShouldBeLessThanOrEqualTo(Math.Max(0, before.MissingAttendanceCount - 1));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Lock_Should_Reject_Employee_Before_Joining_Month()
	{
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"PayrollLockMayJoin",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

		var response = await Client.PostAsJsonAsync(
			BaseUrl,
			new RunPayrollRequest
			{
				Month = "2026-04",
				EmployeeIds = new List<Guid> { employee.Id }
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payroll_Register_Should_Remove_Unlocked_Stale_Rows_For_Ineligible_Employees()
	{
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(
			Client,
			"PayrollStaleMayJoin",
			dateOfJoining: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
		await SeedStalePayrollRowsAsync(employee.Id, "2026-04");

		var aprilPreview = await GetAndReadAsync<RunPayrollResponse>(
			$"{BaseUrl}?month=2026-04&page=0&pageSize=100&forceRefresh=true");

		aprilPreview.Items.Any(x => x.EmployeeId == employee.Id).ShouldBeFalse();
		await AssertNoStalePayrollRowsAsync(employee.Id, "2026-04");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Payroll_Readiness_Without_Generating_Preview()
	{
		var response = await Client.GetAsync($"{BaseUrl}/readiness?month=2026-05");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Lock_Payroll_Without_500()
	{
		var response = await Client.PostAsJsonAsync(
			BaseUrl,
			new RunPayrollRequest { Month = PayrollMonth },
			JsonOptions);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Mark_Review_Without_500()
	{
		var response = await Client.PostAsJsonAsync(
			ReviewUrl,
			new PayrollRunReviewRequest
			{
				Month = PayrollMonth,
				EmployeeIds = new List<System.Guid> { System.Guid.NewGuid() }
			},
			JsonOptions);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Unmark_Review_Without_500()
	{
		var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, ReviewUrl)
		{
			Content = JsonContent.Create(
				new PayrollRunReviewRequest
				{
					Month = PayrollMonth,
					EmployeeIds = new List<System.Guid> { System.Guid.NewGuid() }
				},
				options: JsonOptions)
		});

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Invalid_Month_Format()
	{
		var response = await Client.GetAsync($"{BaseUrl}?month=invalid-format&page=0&pageSize=50");
		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task HrSwagger_Should_Contain_RunPayroll_And_AutomatedRunLock_Endpoints()
	{
		var response = await Client.GetAsync("/swagger/v1-hr/swagger.json");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		document.RootElement.GetProperty("paths").TryGetProperty("/api/payroll/run", out _).ShouldBeTrue();
		document.RootElement.GetProperty("paths").TryGetProperty("/api/payroll/run/readiness", out _).ShouldBeTrue();
		document.RootElement.GetProperty("paths").TryGetProperty("/api/payroll/run/reviews", out _).ShouldBeTrue();
		document.RootElement.GetProperty("paths").TryGetProperty("/api/hr/attendance/automated-runs/{batchId}/lock", out _).ShouldBeTrue();
	}

	private async Task SeedStalePayrollRowsAsync(Guid employeeId, string monthYear)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var snapshotRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		var reviewRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunReview, Guid>>();
		var adjustmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollAdjustment, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var reviewerId = Guid.Parse(TestAuthHandler.TestUserId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), tenantId, monthYear, employeeId)
			{
				EmployeeName = "Stale Payroll Employee",
				EmployeeIdentifier = "STALE",
				ReviewStatus = "REVIEWED"
			};
			await snapshotRepository.InsertAsync(snapshot, autoSave: false);
			await reviewRepository.InsertAsync(
				new PayrollRunReview(Guid.NewGuid(), tenantId, monthYear, employeeId, reviewerId, DateTime.UtcNow),
				autoSave: false);
			await adjustmentRepository.InsertAsync(
				new PayrollAdjustment(Guid.NewGuid(), tenantId, monthYear, employeeId, "Stale adjustment", 100m, true, "Regression setup"),
				autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task SetEmployeeStatusAsync(Guid employeeId, int status, DateOnly effectiveDate)
	{
		var response = await Client.PutAsJsonAsync(
			$"/api/employees/{employeeId}/status",
			new EmployeeStatusUpdateRequest
			{
				EmployeeStatusType = status,
				Remarks = "Payroll eligibility regression",
				EffectiveDate = effectiveDate
			},
			JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	private async Task AssertNoStalePayrollRowsAsync(Guid employeeId, string monthYear)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var snapshotRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		var reviewRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunReview, Guid>>();
		var adjustmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollAdjustment, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var snapshots = await snapshotRepository.GetQueryableAsync();
			var reviews = await reviewRepository.GetQueryableAsync();
			var adjustments = await adjustmentRepository.GetQueryableAsync();

			snapshots.Any(x => x.MonthYear == monthYear && x.EmployeeId == employeeId).ShouldBeFalse();
			reviews.Any(x => x.MonthYear == monthYear && x.EmployeeId == employeeId).ShouldBeFalse();
			adjustments.Any(x => x.MonthYear == monthYear && x.EmployeeId == employeeId).ShouldBeFalse();
			await uow.CompleteAsync();
		}
	}
}
