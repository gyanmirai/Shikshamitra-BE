using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Enrollment;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Records;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class StudentDashboardApi_Tests : ShikshaMitraApiTestBase
{
	private const string ReportingPreviewUrl = "/api/students/dashboard/previews/Reporting";

	public StudentDashboardApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Require_Student_Reports_View_Permission()
	{
		using var noPermissionClient = CreatePermissionClient();

		var response = await noPermissionClient.GetAsync(ReportingPreviewUrl);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Create_And_Queue_Reporting_Dashboard_Preview()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var dashboardClient = CreatePermissionClient(ShikshaMitraSubModulePermissions.StudentDashboard);

		var response = await dashboardClient.GetAsync(ReportingPreviewUrl);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var preview = await response.Content.ReadFromJsonAsync<StudentDashboardPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.DashboardKey.ShouldBe(StudentDashboardKeys.Reporting);
		preview.ScopeKey.ShouldBe(StudentDashboardScopeKeys.StudentWide);
		preview.Status.ShouldBe(StudentDashboardPreviewStatus.Refreshing);
		preview.PayloadJson.ShouldBe("{}");
		preview.IsStale.ShouldBeTrue();
		preview.RefreshStartedAt.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Build_Ready_Reporting_Dashboard_Payload()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var registration = await StudentEnrollmentTestHelper.CreateDirectRegistrationAsync(Client, "ReportingDash");

		var response = await Client.PostAsync($"{ReportingPreviewUrl}/refresh", content: null);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var queuedPreview = await response.Content.ReadFromJsonAsync<StudentDashboardPreviewDto>(JsonOptions);
		queuedPreview.ShouldNotBeNull();

		await ExecuteRefreshAsync(queuedPreview);
		var preview = await GetPreviewAsync(queuedPreview.ScopeKey);

		preview.Status.ShouldBe(StudentDashboardPreviewStatus.Ready);
		preview.LastError.ShouldBeNull();
		preview.PayloadJson.ShouldNotBe("{}");

		var payload = JsonSerializer.Deserialize<StudentReportingDashboardDto>(preview.PayloadJson, JsonOptions);
		payload.ShouldNotBeNull();
		payload.Kpis.ShouldContain(kpi => kpi.Id == "total-students" && kpi.Value >= 1);
		payload.Kpis.ShouldContain(kpi => kpi.Id == "registrations" && kpi.Value >= 1);
		payload.Categories.ShouldContain(category => category.Id == "records");
		payload.Categories.ShouldContain(category => category.Id == "attendance");
		payload.QuickLinks.ShouldContain(link => link.Id == "student-list");
		payload.EnrollmentTrend.ShouldContain(point =>
			point.Registrations >= 1 || point.Enrolled >= 1 || point.Inquiries >= 1);
		payload.GeneratedAt.ShouldBeGreaterThan(DateTime.MinValue);
		payload.Kpis.ShouldContain(kpi => kpi.Description != null && kpi.Href != null);
		payload.Kpis.ShouldContain(kpi => kpi.Label == "Registrations" && kpi.Value >= 1);
		registration.StudentId.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Should_Project_Stable_Next_Scheduled_Refresh_Time()
	{
		var projectionService = new StudentDashboardPreviewProjectionService();
		var preview = new StudentDashboardPreview(
			Guid.NewGuid(),
			Guid.Parse(TestAuthHandler.TestTenantId),
			StudentDashboardKeys.Reporting,
			StudentDashboardScopeKeys.StudentWide);
		var now = new DateTime(2026, 06, 18, 0, 0, 0, DateTimeKind.Utc);

		var first = projectionService.ToDto(preview, now);
		var second = projectionService.ToDto(preview, now.AddMinutes(10));

		first.NextScheduledRefreshAt.ShouldBe(new DateTime(2026, 06, 18, 0, 30, 0, DateTimeKind.Utc));
		second.NextScheduledRefreshAt.ShouldBe(first.NextScheduledRefreshAt);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Scheduled_Refresh_Should_Queue_Existing_Student_Wide_Previews()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await ClearDashboardPreviewsAsync();

		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var widePreviewId = Guid.NewGuid();
		var scopedPreviewId = Guid.NewGuid();

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<StudentDashboardPreview, Guid>>();
			var dashboardAppService = scope.ServiceProvider.GetRequiredService<StudentDashboardAppService>();

			using (currentTenant.Change(tenantId))
			using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
			{
				await repository.InsertAsync(
					new StudentDashboardPreview(
						widePreviewId,
						tenantId,
						StudentDashboardKeys.Reporting,
						StudentDashboardScopeKeys.StudentWide),
					autoSave: true);
				await repository.InsertAsync(
					new StudentDashboardPreview(
						scopedPreviewId,
						tenantId,
						StudentDashboardKeys.Reporting,
						"scoped-test"),
					autoSave: true);

				await dashboardAppService.RefreshExistingPreviewsAsync();
				await uow.CompleteAsync();
			}
		}

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<StudentDashboardPreview, Guid>>();

			using (currentTenant.Change(tenantId))
			using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
			{
				var widePreview = await repository.GetAsync(widePreviewId);
				widePreview.Status.ShouldBe(StudentDashboardPreviewStatus.Refreshing);
				widePreview.RefreshStartedAt.ShouldNotBeNull();
				widePreview.RefreshRequestedByUserId.ShouldBeNull();

				var scopedPreview = await repository.GetAsync(scopedPreviewId);
				scopedPreview.Status.ShouldBe(StudentDashboardPreviewStatus.Empty);
				scopedPreview.RefreshStartedAt.ShouldBeNull();

				await uow.CompleteAsync();
			}
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Unknown_Dashboard_Key()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.GetAsync("/api/students/dashboard/previews/Unknown");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var content = await response.Content.ReadAsStringAsync();
		content.ShouldContain(StudentDashboardErrorCodes.InvalidKey);
	}

	private async Task ExecuteRefreshAsync(StudentDashboardPreviewDto queuedPreview)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var previewRepository = scope.ServiceProvider.GetRequiredService<IRepository<StudentDashboardPreview, Guid>>();
		var previewService = scope.ServiceProvider.GetRequiredService<StudentDashboardPreviewService>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var userId = Guid.Parse(TestAuthHandler.TestUserId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var preview = await previewRepository.FindAsync(x =>
				x.DashboardKey == StudentDashboardKeys.Reporting &&
				x.ScopeKey == queuedPreview.ScopeKey);
			preview.ShouldNotBeNull();

			await previewService.ExecutePreviewRefreshAsync(new StudentDashboardPreviewRefreshJobArgs
			{
				TenantId = tenantId,
				PreviewId = preview.Id,
				DashboardKey = preview.DashboardKey,
				ScopeKey = preview.ScopeKey,
				RequestedByUserId = userId
			});

			await uow.CompleteAsync();
		}
	}

	private async Task<StudentDashboardPreview> GetPreviewAsync(string scopeKey)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var previewRepository = scope.ServiceProvider.GetRequiredService<IRepository<StudentDashboardPreview, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var preview = await previewRepository.FindAsync(x =>
				x.DashboardKey == StudentDashboardKeys.Reporting &&
				x.ScopeKey == scopeKey);
			preview.ShouldNotBeNull();
			await uow.CompleteAsync();
			return preview;
		}
	}

	private async Task ClearDashboardPreviewsAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<StudentDashboardPreview, Guid>>();
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

	private HttpClient CreatePermissionClient(params string[] permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			permissions.Length == 0 ? "__none__" : string.Join(",", permissions));
		return client;
	}
}
