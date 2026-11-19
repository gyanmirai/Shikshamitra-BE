using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Onboarding;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Dashboard;

public partial class HRDashboardApi_Tests
{
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Appraisal_Reads_Should_Deny_OutOfScope_Identifiers_And_Hide_Confidential_Outcomes()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"ReviewScope{Guid.NewGuid():N}");
		await AssignStaffOrganizationAsync(employee.Id, EmployeeTestHelper.DefaultMediumStageId);
		var userId = Guid.NewGuid();
		await EnsureUserHasStageScopeAsync(userId, EmployeeTestHelper.DefaultMediumStageId);
		Guid insideId, outsideId;
		using (var serviceScope = Factory.Services.CreateScope())
		{
			var services = serviceScope.ServiceProvider;
			var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
			using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
			using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
			var template = new PerformanceTemplate(Guid.NewGuid(), tenantId, "Scoped outcome", PerformanceStaffCategory.Teacher, null);
			await services.GetRequiredService<IRepository<PerformanceTemplate, Guid>>().InsertAsync(template);
			var cycle = new ProfessionalReviewCycle(Guid.NewGuid(), tenantId, null, "Scoped outcome cycle", default,
				DateTime.Today.AddDays(-30), DateTime.Today.AddDays(30), null, null, null, 5, true, null, null, ProfessionalReviewCycleStatus.InProgress);
			await services.GetRequiredService<IRepository<ProfessionalReviewCycle, Guid>>().InsertAsync(cycle);
			var reviews = services.GetRequiredService<IRepository<PerformanceReviewAssignment, Guid>>();
			var inside = new PerformanceReviewAssignment(Guid.NewGuid(), tenantId, cycle.Id, template.Id, employee.Id);
			inside.FinalizeReview(4.9m, "Confidential rating", DateTime.UtcNow);
			insideId = inside.Id;
			await reviews.InsertAsync(inside);
			var outside = new PerformanceReviewAssignment(Guid.NewGuid(), tenantId, cycle.Id, template.Id, Guid.NewGuid());
			outsideId = outside.Id;
			await reviews.InsertAsync(outside);
			var audit = services.GetRequiredService<IRepository<PerformanceAuditEvent, Guid>>();
			await audit.InsertAsync(new PerformanceAuditEvent(Guid.NewGuid(), tenantId, insideId, "Test", "Inside scope"));
			await audit.InsertAsync(new PerformanceAuditEvent(Guid.NewGuid(), tenantId, outsideId, "Test", "Outside scope"));
			await unit.CompleteAsync();
		}
		using var restricted = CreateClientAs(userId);
		restricted.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			"HR.PerformanceManagement.View,HR.PerformanceManagement.ViewAudit");
		var list = (await restricted.GetFromJsonAsync<ProfessionalReviewListResponse>("/api/hr/professional-management/reviews", JsonOptions))!;
		list.Items.ShouldContain(x => x.Id == insideId);
		list.Items.ShouldNotContain(x => x.Id == outsideId);
		list.Items.Single(x => x.Id == insideId).FinalScore.ShouldBeNull();
		list.Items.Single(x => x.Id == insideId).FinalRating.ShouldBeNull();
		var detail = (await restricted.GetFromJsonAsync<ProfessionalReviewDto>($"/api/hr/professional-management/reviews/{insideId}", JsonOptions))!;
		detail.FinalScore.ShouldBeNull();
		foreach (var suffix in new[] { "", "/evidence", "/observations", "/feedback" })
			(await restricted.GetAsync($"/api/hr/professional-management/reviews/{outsideId}{suffix}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await restricted.GetAsync($"/api/hr/professional-management/audit?reviewId={outsideId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var auditItems = (await restricted.GetFromJsonAsync<PerformanceAuditEventDto[]>("/api/hr/professional-management/audit", JsonOptions))!;
		auditItems.ShouldContain(x => x.ReviewAssignmentId == insideId);
		auditItems.ShouldNotContain(x => x.ReviewAssignmentId == outsideId);
		restricted.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		restricted.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "HR.Dashboard.View");
		(await restricted.GetAsync("/api/hr/professional-management/reviews")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Module_Overview_Should_Use_Current_Workflows_Regardless_Of_Attendance_Date()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5.5)).Date;
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"Modules{Guid.NewGuid():N}", dateOfJoining: today);
		await AssignStaffOrganizationAsync(employee.Id, EmployeeTestHelper.DefaultMediumStageId);
		var before = (await Client.GetFromJsonAsync<HRDashboardOverviewDto>($"{BaseUrl}/overview", JsonOptions))!;
		var assignments = await SeedDashboardModulesAsync(employee.Id, today);
		var current = (await Client.GetFromJsonAsync<HRDashboardOverviewDto>($"{BaseUrl}/overview", JsonOptions))!;
		var historical = (await Client.GetFromJsonAsync<HRDashboardOverviewDto>($"{BaseUrl}/overview?date=2026-01-01", JsonOptions))!;
		current.Workforce!.ActiveStaff.ShouldBe(current.Workforce.Teaching + current.Workforce.NonTeaching);
		current.Workforce.JoinedThisMonth.ShouldBeGreaterThanOrEqualTo(1);
		current.Onboarding!.ReadyCount.ShouldBe(before.Onboarding!.ReadyCount + 120);
		current.Onboarding.InProgressCount.ShouldBe(before.Onboarding.InProgressCount + 1);
		current.Appraisal!.PendingCount.ShouldBe(before.Appraisal!.PendingCount + 120);
		current.Appraisal.OverdueCount.ShouldBe(before.Appraisal.OverdueCount + 120);
		current.Appraisal.NextDeadline.ShouldNotBeNull();
		historical.Workforce.ShouldBe(current.Workforce);
		historical.Onboarding.ShouldBe(current.Onboarding);
		historical.Appraisal.ShouldBe(current.Appraisal);
		historical.Payroll!.Month.ShouldBe(today.ToString("yyyy-MM"));
		historical.AsOfDate.ShouldBe(today.ToString("yyyy-MM-dd"));

		var userId = Guid.NewGuid();
		await EnsureUserHasStageScopeAsync(userId, EmployeeTestHelper.DefaultMediumStageId);
		using var restricted = CreateClientAs(userId);
		restricted.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			"HR.Dashboard.View,HR.Employee.EmployeeOnboarding.View,HR.PerformanceManagement.View");
		var scoped = (await restricted.GetFromJsonAsync<HRDashboardOverviewDto>($"{BaseUrl}/overview", JsonOptions))!;
		scoped.Workforce.ShouldBeNull();
		scoped.OnboardingState.ShouldBe(HRDashboardSectionState.Unavailable);
		scoped.Onboarding.ShouldBeNull();
		scoped.Capabilities.CanOpenOnboarding.ShouldBeFalse();
		scoped.Appraisal!.PendingCount.ShouldBeGreaterThanOrEqualTo(120);
		var reviews = (await restricted.GetFromJsonAsync<ProfessionalReviewListResponse>("/api/hr/professional-management/reviews", JsonOptions))!;
		reviews.Items.Count(x => assignments.Contains(x.Id)).ShouldBe(120);
		reviews.Items.ShouldNotContain(x => x.EmployeeId == Guid.Empty);
		(await restricted.GetAsync($"/api/hr/professional-management/reviews/{assignments.Last()}/evidence")).StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	private async Task<Guid[]> SeedDashboardModulesAsync(Guid employeeId, DateTime today)
	{
		using var serviceScope = Factory.Services.CreateScope();
		var services = serviceScope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var applications = services.GetRequiredService<IRepository<EmployeeApplication, Guid>>();
		for (var i = 0; i < 122; i++)
		{
			var application = new EmployeeApplication(Guid.NewGuid(), tenantId, $"MODULE-{Guid.NewGuid():N}", Guid.NewGuid().ToString("N"), null,
				today.AddDays(10), today, "Test", null, "Candidate", null, null, null, null);
			if (i < 121)
			{
				application.Submit(today, new EmployeeApplicationPersonalInfoSnapshot(
					"Test", null, null, "Candidate", "candidate@example.com", "9876543210", null,
					null, null, null, null, null, null, null, true, null, null, null, null,
					null, null, null, null, null, null, null), null, null, [], null, null);
				application.ChangeStatus(EmployeeApplicationStatus.Screening, "Dashboard test fixture");
				if (i < 120)
				{
					application.ChangeStatus(EmployeeApplicationStatus.Interviewing, "Dashboard test fixture");
					application.ChangeStatus(EmployeeApplicationStatus.Offered, "Dashboard test fixture");
					application.ChangeStatus(EmployeeApplicationStatus.ReadyForOnboarding, "Dashboard test fixture");
				}
			}
			await applications.InsertAsync(application);
		}
		var templates = services.GetRequiredService<IRepository<PerformanceTemplate, Guid>>();
		var template = new PerformanceTemplate(Guid.NewGuid(), tenantId, "Dashboard module template", PerformanceStaffCategory.Teacher, null);
		await templates.InsertAsync(template);
		var cycles = services.GetRequiredService<IRepository<ProfessionalReviewCycle, Guid>>();
		var reviews = services.GetRequiredService<IRepository<PerformanceReviewAssignment, Guid>>();
		var stages = services.GetRequiredService<IRepository<PerformanceReviewStage, Guid>>();
		var ids = new Guid[120];
		for (var i = 0; i < ids.Length; i++)
		{
			var cycle = new ProfessionalReviewCycle(Guid.NewGuid(), tenantId, null, $"Module cycle {i}", default,
				today.AddDays(-30), today.AddDays(30), today.AddDays(-1), today.AddDays(-1), today.AddDays(3), 5, true, null, null,
				ProfessionalReviewCycleStatus.InProgress);
			await cycles.InsertAsync(cycle);
			var review = new PerformanceReviewAssignment(Guid.NewGuid(), tenantId, cycle.Id, template.Id, employeeId);
			ids[i] = review.Id;
			await reviews.InsertAsync(review);
			foreach (var type in new[] { PerformanceReviewStageType.SelfAppraisal, PerformanceReviewStageType.ReportingManager, PerformanceReviewStageType.HrVerification })
				await stages.InsertAsync(new PerformanceReviewStage(Guid.NewGuid(), tenantId, review.Id, type));
		}
		await unit.CompleteAsync();
		return ids;
	}
}
