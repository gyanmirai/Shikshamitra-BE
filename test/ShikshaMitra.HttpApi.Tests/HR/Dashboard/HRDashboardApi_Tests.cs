using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.ReportingManagerInfo;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
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
	public async Task Should_Get_HR_Dashboard_Summary_With_Contract_Sections()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.GetAsync($"{BaseUrl}/summary?date=2026-05-23&month=2026-05&range=7");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);

		document.RootElement.TryGetProperty("workforce", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("attendanceToday", out var attendanceToday).ShouldBeTrue();
		attendanceToday.TryGetProperty("missingAttendance", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("attendanceTrend", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("leave", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("payroll", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("celebrations", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("insights", out _).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Show_Inactive_Reporting_Manager_Insight_For_Existing_Data()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var managerId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DashboardInactiveMgr");
		var reportId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DashboardInactiveReport");

		var assignResponse = await Client.PutAsJsonAsync(
			$"/api/employees/{reportId}/reporting-manager",
			new ReportingManagerRequest { ReportingManagerId = managerId.ToString() },
			JsonOptions);
		assignResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			var employeeManager = scope.ServiceProvider.GetRequiredService<EmployeeManager>();
			var employeeRepository = scope.ServiceProvider.GetRequiredService<IEmployeeRepository>();

			using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
			using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
			{
				var manager = await employeeManager.UpdateEmployeeStatusAsync(
					managerId,
					EmployeeStatusType.Inactive,
					"Legacy inconsistent state");
				await employeeRepository.UpdateAsync(manager);
				await uow.CompleteAsync();
			}
		}

		var response = await Client.GetAsync($"{BaseUrl}/summary?date=2026-05-23&month=2026-05&range=7");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		var insights = document.RootElement.GetProperty("insights").EnumerateArray();
		var reportingManagerInsight = insights.FirstOrDefault(insight =>
			insight.GetProperty("source").GetString() == "employee.reporting-manager");

		reportingManagerInsight.ValueKind.ShouldNotBe(JsonValueKind.Undefined);
		reportingManagerInsight.GetProperty("severity").GetString().ShouldBe("critical");
		var summary = reportingManagerInsight.GetProperty("summary").GetString();
		summary.ShouldNotBeNull();
		summary.ShouldContain("1 active employee");
		reportingManagerInsight.GetProperty("actionHref").GetString().ShouldBe("/hr/employee?reportingManagerStatus=nonActive");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Dashboard_Date()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.GetAsync($"{BaseUrl}/summary?date=not-a-date&month=2026-05");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Dashboard_Month()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.GetAsync($"{BaseUrl}/summary?date=2026-05-23&month=05-2026");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Empty_Preview_Without_Live_Calculation()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await ClearDashboardPreviewsAsync();

		var response = await Client.GetAsync($"{BaseUrl}/previews/Main");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		document.RootElement.GetProperty("dashboardKey").GetString().ShouldBe(HrDashboardKeys.Main);
		document.RootElement.GetProperty("scopeKey").GetString().ShouldBe(HrDashboardScopeKeys.HrWide);
		document.RootElement.GetProperty("status").GetString().ShouldBe(HrDashboardPreviewStatus.Empty);
		document.RootElement.GetProperty("payloadJson").GetString().ShouldBe("{}");
		document.RootElement.GetProperty("isStale").GetBoolean().ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Queue_Manual_Preview_Refresh()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await ClearDashboardPreviewsAsync();

		var response = await Client.PostAsync($"{BaseUrl}/previews/Main/refresh", content: null);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		document.RootElement.GetProperty("status").GetString().ShouldBe(HrDashboardPreviewStatus.Refreshing);
		document.RootElement.GetProperty("dashboardKey").GetString().ShouldBe(HrDashboardKeys.Main);
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
					new HrDashboardPreview(previewId, tenantId, HrDashboardKeys.Main, HrDashboardScopeKeys.HrWide),
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
				DashboardKey = HrDashboardKeys.Main,
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
				preview.PayloadJson.ShouldContain("\"workforce\"");
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
					new HrDashboardPreview(scopedPreviewId, tenantId, HrDashboardKeys.Main, scopedKey),
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
				previews.Count(x => x.ScopeKey == HrDashboardScopeKeys.HrWide).ShouldBe(HrDashboardKeys.All.Length);

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
}
