using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Employees.ReportingManagerInfo;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Identity;
using Volo.Abp.Uow;
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
	public async Task Retired_Main_Reads_Should_Not_Expose_Combined_Data()
	{
		(await Client.GetAsync($"{BaseUrl}/summary")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await Client.GetAsync($"{BaseUrl}/previews/Main")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await Client.PostAsync($"{BaseUrl}/previews/Main/refresh", null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}
	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Automatically_Queue_Missing_Preview_Refresh()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await ClearDashboardPreviewsAsync();

		var response = await Client.GetAsync($"{BaseUrl}/previews/Attendance");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		document.RootElement.GetProperty("dashboardKey").GetString().ShouldBe(HrDashboardKeys.Attendance);
		document.RootElement.GetProperty("scopeKey").GetString().ShouldBe(HrDashboardScopeKeys.HrWide);
		document.RootElement.GetProperty("status").GetString().ShouldBe(HrDashboardPreviewStatus.Refreshing);
		document.RootElement.GetProperty("payloadJson").GetString().ShouldBe("{}");
		document.RootElement.GetProperty("isStale").GetBoolean().ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Queue_Manual_Preview_Refresh()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await ClearDashboardPreviewsAsync();

		var response = await Client.PostAsync($"{BaseUrl}/previews/Attendance/refresh", content: null);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		document.RootElement.GetProperty("status").GetString().ShouldBe(HrDashboardPreviewStatus.Refreshing);
		document.RootElement.GetProperty("dashboardKey").GetString().ShouldBe(HrDashboardKeys.Attendance);
		document.RootElement.GetProperty("scopeKey").GetString().ShouldBe(HrDashboardScopeKeys.HrWide);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Background_Refresh_Should_Build_Ready_Snapshot()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await ClearDashboardPreviewsAsync();

		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var previewId = Guid.NewGuid();

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<HrDashboardPreview, Guid>>();

			using (currentTenant.Change(tenantId))
			using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
			{
				await repository.InsertAsync(
					new HrDashboardPreview(previewId, tenantId, HrDashboardKeys.Attendance, HrDashboardScopeKeys.HrWide),
					autoSave: true);
				await uow.CompleteAsync();
			}
		}

		using (var scope = Factory.Services.CreateScope())
		{
			var job = scope.ServiceProvider.GetRequiredService<HrDashboardPreviewRefreshBackgroundJob>();
			await job.ExecuteAsync(new HrDashboardPreviewRefreshJobArgs
			{
				TenantId = tenantId,
				PreviewId = previewId,
				DashboardKey = HrDashboardKeys.Attendance,
				ScopeKey = HrDashboardScopeKeys.HrWide
			});
		}

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<HrDashboardPreview, Guid>>();

			using (currentTenant.Change(tenantId))
			using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
			{
				var preview = await repository.GetAsync(previewId);
				preview.Status.ShouldBe(HrDashboardPreviewStatus.Ready);
				preview.LastError.ShouldBeNull();
				preview.PayloadJson.ShouldContain("\"totalStaff\"");
				preview.LastRefreshedAt.ShouldNotBeNull();

				await uow.CompleteAsync();
			}
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Scheduled_Refresh_Should_Ignore_Scoped_Previews()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await ClearDashboardPreviewsAsync();

		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var scopedPreviewId = Guid.NewGuid();
		const string scopedKey = "scoped-test";

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<HrDashboardPreview, Guid>>();
			var dashboardAppService = scope.ServiceProvider.GetRequiredService<HRDashboardAppService>();

			using (currentTenant.Change(tenantId))
			using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
			{
				await repository.InsertAsync(
					new HrDashboardPreview(scopedPreviewId, tenantId, HrDashboardKeys.Attendance, scopedKey),
					autoSave: true);

				await dashboardAppService.RefreshExistingPreviewsAsync();
				await uow.CompleteAsync();
			}
		}

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<HrDashboardPreview, Guid>>();

			using (currentTenant.Change(tenantId))
			using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
			{
				var scopedPreview = await repository.GetAsync(scopedPreviewId);
				scopedPreview.Status.ShouldBe(HrDashboardPreviewStatus.Empty);
				scopedPreview.RefreshStartedAt.ShouldBeNull();
				scopedPreview.LastError.ShouldBeNull();

				var previews = await repository.GetListAsync();
				previews.Count(x => x.ScopeKey == HrDashboardScopeKeys.HrWide).ShouldBe(HrDashboardKeys.All.Length - 1);

				await uow.CompleteAsync();
			}
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Preview_Key()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.GetAsync($"{BaseUrl}/previews/Unknown");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Forbidden_When_Dashboard_Permission_Is_Missing()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			"__none__");

		var response = await client.GetAsync($"{BaseUrl}/previews/Main");

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Operational_Dashboard_Should_Not_Serialize_Unauthorized_Sections()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "HR.Dashboard.View");
		var response = await client.GetAsync($"{BaseUrl}/overview?date=2026-05-23");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await response.Content.ReadFromJsonAsync<HRDashboardOverviewDto>(JsonOptions);
		result!.AttendanceState.ShouldBe(HRDashboardSectionState.Forbidden);
		result.WorkforceState.ShouldBe(HRDashboardSectionState.Forbidden);
		result.Summary.ShouldBeNull();
		result.Payroll.ShouldBeNull();
		result.Trend.ShouldBeNull();
		result.PendingLeaveRequests.ShouldBeNull();
		result.Filters.Departments.ShouldBeEmpty();
		result.Attention.ShouldBeEmpty();
		(await client.GetAsync($"{BaseUrl}/staff?date=2026-05-23")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Operational_Attendance_Grant_Should_Not_Imply_Payroll_Or_Staff_Grant()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			"HR.Dashboard.View,HR.AttendanceAndLeave.Dashboard.View");
		var response = await client.GetAsync($"{BaseUrl}/overview?date=2026-05-23");
		var raw = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, raw);
		var result = await response.Content.ReadFromJsonAsync<HRDashboardOverviewDto>(JsonOptions);
		result!.AttendanceState.ShouldBe(HRDashboardSectionState.Available);
		result.Summary.ShouldNotBeNull();
		result.Capabilities.CanViewStaff.ShouldBeFalse();
		result.Payroll.ShouldBeNull();
		result.PendingLeaveRequests.ShouldBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Operational_Staff_Should_Respect_Stage_Scope_And_Include_Unmarked_Staff()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"OpDash{Guid.NewGuid():N}", dateOfJoining: new DateTime(2026, 1, 1));
		await AssignStaffOrganizationAsync(employee.Id, EmployeeTestHelper.DefaultMediumStageId);
		var userId = Guid.NewGuid();
		await EnsureUserHasStageScopeAsync(userId, EmployeeTestHelper.DefaultMediumStageId);
		var client = CreateClientAs(userId);
		var response = await client.GetAsync($"{BaseUrl}/staff?date=2026-05-23&search={employee.EmployeeCode}&maxResultCount=100");
		var raw = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, raw);
		var result = await response.Content.ReadFromJsonAsync<HRDashboardStaffPageDto>(JsonOptions);
		result!.Items.ShouldContain(x => x.EmployeeId == employee.Id && x.Status != HRDashboardDailyStatus.Absent, raw);
		result.Items.ShouldAllBe(x => x.Stages.All(stage => stage.Id == EmployeeTestHelper.DefaultMediumStageId));
		raw.ShouldNotContain("bank");
		raw.ShouldNotContain("salary");
		client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "HR.Dashboard.View");
		(await client.GetAsync($"{BaseUrl}/staff")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var restricted = await client.GetFromJsonAsync<HRDashboardOverviewDto>($"{BaseUrl}/overview", JsonOptions);
		restricted!.Summary.ShouldBeNull();
		restricted.Capabilities.CanViewStaff.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Operational_Reads_Should_Reject_Foreign_Tenant_Identity()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		(await client.GetAsync($"{BaseUrl}/overview")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await client.GetAsync($"{BaseUrl}/staff")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	private async Task AssignStaffOrganizationAsync(Guid employeeId, Guid stageId)
	{
		using var serviceScope = Factory.Services.CreateScope();
		var tenant = serviceScope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var units = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var organizations = serviceScope.ServiceProvider.GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = units.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var employees = serviceScope.ServiceProvider.GetRequiredService<IRepository<Employee, Guid>>();
			var manager = serviceScope.ServiceProvider.GetRequiredService<EmployeeManager>();
			var staff = await manager.UpdateEmployeeStatusAsync(employeeId, EmployeeStatusType.Active, "Active dashboard test fixture", new DateOnly(2026, 1, 1));
			await employees.UpdateAsync(staff, autoSave: true);
			if (!await organizations.AnyAsync(x => x.EmployeeId == employeeId && x.EducationMediumStageId == stageId))
				await organizations.InsertAsync(new EmployeeOrganizationAssignment(Guid.NewGuid(), tenant.Id, employeeId, stageId), autoSave: true);
			await unit.CompleteAsync();
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Operational_Counts_Should_Reconcile_And_Reads_Should_Not_Write_Preview_Data()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		async Task<(long Payroll, long Previews)> StoredCountsAsync()
		{
			using var serviceScope = Factory.Services.CreateScope();
			using (serviceScope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
			using (var unit = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
			{
				var payroll = await serviceScope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().GetCountAsync();
				var previews = await serviceScope.ServiceProvider.GetRequiredService<IRepository<HrDashboardPreview, Guid>>().GetCountAsync();
				await unit.CompleteAsync();
				return (payroll, previews);
			}
		}
		var before = await StoredCountsAsync();
		var overview = await Client.GetFromJsonAsync<HRDashboardOverviewDto>($"{BaseUrl}/overview", JsonOptions);
		var staff = await Client.GetFromJsonAsync<HRDashboardStaffPageDto>($"{BaseUrl}/staff?maxResultCount=100", JsonOptions);
		overview!.AttendanceState.ShouldBe(HRDashboardSectionState.Available);
		overview.Summary!.StaffCount.ShouldBe(staff!.TotalCount);
		var allStaff = staff.Items.ToList();
		for (var skip = 100; skip < staff.TotalCount; skip += 100)
		{
			var nextPage = await Client.GetFromJsonAsync<HRDashboardStaffPageDto>($"{BaseUrl}/staff?maxResultCount=100&skipCount={skip}", JsonOptions);
			allStaff.AddRange(nextPage!.Items);
		}
		allStaff.Count.ShouldBe(staff.TotalCount);
		var summary = overview.Summary;
		(summary.Present + summary.Absent + summary.OnLeave + summary.HalfDay + summary.NotMarked + summary.NotScheduled + summary.NeedsSetup).ShouldBe(summary.StaffCount);
		foreach (var status in new[] { "Present", "Absent", "OnLeave", "HalfDay", "NotMarked", "NotScheduled", "NeedsSetup" })
		{
			var matching = await Client.GetFromJsonAsync<HRDashboardStaffPageDto>($"{BaseUrl}/staff?status={status}&maxResultCount=100", JsonOptions);
			matching!.TotalCount.ShouldBe(allStaff.Count(x => x.Status.ToString() == status));
		}
		(await StoredCountsAsync()).ShouldBe(before);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Operational_Reads_Should_Respect_Joining_And_Employment_End_Dates()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"DatedDash{Guid.NewGuid():N}", dateOfJoining: new DateTime(2026, 5, 23));
		await AssignStaffOrganizationAsync(employee.Id, EmployeeTestHelper.DefaultMediumStageId);
		var beforeJoining = await Client.GetFromJsonAsync<HRDashboardStaffPageDto>($"{BaseUrl}/staff?date=2026-05-22&search={employee.EmployeeCode}", JsonOptions);
		beforeJoining!.Items.ShouldBeEmpty();
		var joined = await Client.GetFromJsonAsync<HRDashboardStaffPageDto>($"{BaseUrl}/staff?date=2026-05-23&search={employee.EmployeeCode}", JsonOptions);
		joined!.Items.ShouldContain(x => x.EmployeeId == employee.Id);
		using (var serviceScope = Factory.Services.CreateScope())
		using (serviceScope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var changed = await serviceScope.ServiceProvider.GetRequiredService<EmployeeManager>().UpdateEmployeeStatusAsync(
				employee.Id, EmployeeStatusType.Inactive, "Ended dashboard test employment", new DateOnly(2026, 5, 23));
			await serviceScope.ServiceProvider.GetRequiredService<IRepository<Employee, Guid>>().UpdateAsync(changed, autoSave: true);
			await unit.CompleteAsync();
		}
		var afterExit = await Client.GetFromJsonAsync<HRDashboardStaffPageDto>($"{BaseUrl}/staff?date=2026-05-24&search={employee.EmployeeCode}", JsonOptions);
		afterExit!.Items.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Operational_Reads_Should_Reflect_New_Attendance_In_Rows_And_Counts()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"FreshDash{Guid.NewGuid():N}", dateOfJoining: new DateTime(2026, 1, 1));
		await AssignStaffOrganizationAsync(employee.Id, EmployeeTestHelper.DefaultMediumStageId);
		var before = await Client.GetFromJsonAsync<HRDashboardOverviewDto>($"{BaseUrl}/overview?date=2026-05-23", JsonOptions);
		using (var serviceScope = Factory.Services.CreateScope())
		using (serviceScope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await serviceScope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>().InsertAsync(new EmployeeAttendance
			{
				TenantId = Guid.Parse(TestAuthHandler.TestTenantId), EmployeeId = employee.Id,
				EducationMediumStageId = EmployeeTestHelper.DefaultMediumStageId,
				AttendanceDate = new DateOnly(2026, 5, 23), StatusType = EmployeeAttendanceStatusType.Present,
				Source = AttendanceRecordSource.Manual
			}, autoSave: true);
			await unit.CompleteAsync();
		}
		var staff = await Client.GetFromJsonAsync<HRDashboardStaffPageDto>($"{BaseUrl}/staff?date=2026-05-23&search={employee.EmployeeCode}", JsonOptions);
		staff!.Items.ShouldHaveSingleItem().Status.ShouldBe(HRDashboardDailyStatus.Present);
		var after = await Client.GetFromJsonAsync<HRDashboardOverviewDto>($"{BaseUrl}/overview?date=2026-05-23", JsonOptions);
		after!.Summary!.Present.ShouldBe(before!.Summary!.Present + 1);
		after.Summary.StaffCount.ShouldBe(before.Summary.StaffCount);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Operational_Default_Date_And_Timestamps_Should_Use_Actual_UTC_And_IST()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var before = DateTimeOffset.UtcNow;
		var overview = await Client.GetFromJsonAsync<HRDashboardOverviewDto>($"{BaseUrl}/overview", JsonOptions);
		var staff = await Client.GetFromJsonAsync<HRDashboardStaffPageDto>($"{BaseUrl}/staff", JsonOptions);
		var after = DateTimeOffset.UtcNow;
		overview!.GeneratedAt.ShouldBeGreaterThanOrEqualTo(before);
		overview.GeneratedAt.ShouldBeLessThanOrEqualTo(after);
		staff!.GeneratedAt.ShouldBeGreaterThanOrEqualTo(before);
		staff.GeneratedAt.ShouldBeLessThanOrEqualTo(after);
		if (overview.Payroll?.LastUpdatedAt is { } savedPayrollTime)
			savedPayrollTime.ShouldBeLessThanOrEqualTo(after);
		overview.Date.ShouldBe(overview.GeneratedAt.ToOffset(TimeSpan.FromHours(5.5)).ToString("yyyy-MM-dd"));
		staff.Date.ShouldBe(staff.GeneratedAt.ToOffset(TimeSpan.FromHours(5.5)).ToString("yyyy-MM-dd"));
	}

	[Theory]
	[InlineData("maxResultCount=101")]
	[InlineData("skipCount=-1")]
	[InlineData("status=Late")]
	[InlineData("sorting=salary")]
	[InlineData("date=2099-01-01")]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Operational_Staff_Should_Reject_Invalid_Requests(string query)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		(await Client.GetAsync($"{BaseUrl}/staff?{query}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private async Task ClearDashboardPreviewsAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<HrDashboardPreview, Guid>>();
		var dataFilter = scope.ServiceProvider.GetRequiredService<IDataFilter>();

		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		using (dataFilter.Disable<ISoftDelete>())
		{
			var previews = await repository.GetListAsync();
			foreach (var preview in previews)
			{
				await repository.HardDeleteAsync(preview, autoSave: false);
			}

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

		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var users = scope.ServiceProvider.GetRequiredService<IIdentityUserRepository>();
			if (await users.FindAsync(userId) == null)
			{
				await users.InsertAsync(new IdentityUser(userId, $"dashboard-{userId:N}", $"dashboard-{userId:N}@test.local", currentTenant.Id), autoSave: true);
			}
			await scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					Guid.Parse(TestAuthHandler.TestTenantId),
					userId: userId,
					educationMediumStageId: stageId,
					description: "API regression test HR dashboard stage scope"),
				autoSave: false);
			await userScopeService.InvalidateUserScopeCacheAsync(userId);
			await uow.CompleteAsync();
		}
	}
}
