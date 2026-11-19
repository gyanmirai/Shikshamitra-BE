using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.Students.Dashboard;

public partial class StudentModuleDashboardApi_Tests
{
	[Fact]
	public void Attention_blocker_uses_the_owning_blocked_first_rule_and_excludes_final_readiness()
	{
		var caseId = Guid.NewGuid();
		var steps = new[] {
			new StudentOnboardingStep(Guid.NewGuid(), null, caseId, StudentOnboardingStepKeys.FinalReadiness, "Final readiness", 0, true, StudentOnboardingStepStatus.Blocked),
			new StudentOnboardingStep(Guid.NewGuid(), null, caseId, "division", "Division assignment", 1, true),
			new StudentOnboardingStep(Guid.NewGuid(), null, caseId, "documents", "Required documents", 2, true, StudentOnboardingStepStatus.Blocked) };
		StudentOnboardingWorkSummary.PrimaryBlocker(steps, []).ShouldBe("Required documents");
		StudentOnboardingWorkSummary.PrimaryBlocker(steps.Take(2), []).ShouldBe("Division assignment");
	}

	[Fact]
	public async Task Admin_record_approval_count_matches_owning_filtered_list_and_excludes_self_wrong_stage_and_prior_actor()
	{
		var id = await SeedAdminAsync(); var expectedId = Guid.NewGuid();
		await WithTenantAsync(async services => {
			var tenant = Guid.Parse(TestAuthHandler.TestTenantId); var other = Guid.Parse(TestAuthHandler.TestUserId);
			var studentId = (await SeedCaseAsync(services, id, Factory.SeededDivisionId)).StudentId;
			var repository = services.GetRequiredService<IRepository<StudentRecordChange, Guid>>();
			StudentRecordChange Item(Guid key, Guid requester, string stages, int required = 1) => new(key, tenant, studentId,
				Factory.SeededAcademicYearId, requester, "general", "Correction", key.ToString(), "Dashboard approval fixture", "{}", "{}", "{}", null, stages, required, DateTimeOffset.UtcNow.AddDays(1));
			await repository.InsertAsync(Item(expectedId, other, "[{\"roleNames\":[\"Admin\"]}]"), true);
			await repository.InsertAsync(Item(Guid.NewGuid(), id, "[{\"roleNames\":[\"Admin\"]}]"), true);
			await repository.InsertAsync(Item(Guid.NewGuid(), other, "[{\"roleNames\":[\"Principal\"]}]"), true);
			var prior = Item(Guid.NewGuid(), other, "[{\"roleNames\":[\"Admin\"]},{\"roleNames\":[\"Admin\"]}]", 2);
			prior.Approve(id); await repository.InsertAsync(prior, true);
			await services.GetRequiredService<IRepository<StudentRecordChangeDecision, Guid>>().InsertAsync(new(Guid.NewGuid(), tenant, prior.Id, id, 0, "Approved", "Prior decision"), true);
		});
		using var client = AdminClient(id, P.Records_Changes.View, P.Records_Changes.Approve, ShikshaMitraSubModulePermissions.Records);
		var response = await client.GetAsync($"{Root}/sections/records?academicYearId={Factory.SeededAcademicYearId}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var section = (await response.Content.ReadFromJsonAsync<StudentModuleDashboardSectionDto>(JsonOptions))!;
		section.Metrics.ShouldBeEmpty(); section.WorkGroups.Single(g => g.Id == "recordApprovals").Value.ShouldBe(1);
		var listResponse = await client.GetAsync($"/api/students/records/changes?academicYearId={Factory.SeededAcademicYearId}&approvableOnly=true");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
		var list = (await listResponse.Content.ReadFromJsonAsync<Volo.Abp.Application.Dtos.PagedResultDto<StudentRecordChangeDto>>(JsonOptions))!;
		list.TotalCount.ShouldBe(1); list.Items.Single().Id.ShouldBe(expectedId); list.Items.Single().CanApprove.ShouldBeTrue();
	}

	[Fact]
	public async Task Admin_attention_is_bounded_and_task_counts_use_the_selected_scope()
	{
		var id = await SeedAdminAsync();
		await WithTenantAsync(async services => {
			for (var index = 0; index < 6; index++) {
				var item = await SeedCaseAsync(services, id); item.RequireAttention("Required setup incomplete");
				await services.GetRequiredService<IRepository<ShikshaMitra.Students.StudentEnrollments.Onboarding.StudentOnboardingCase, Guid>>().UpdateAsync(item, true);
			}
		});
		using var client = AdminClient(id, P.StudentOnboarding.View, ShikshaMitraSubModulePermissions.StudentOnboarding);
		var url = $"{Root}/sections/admissions?academicYearId={Factory.SeededAcademicYearId}&boardId={Factory.SeededBoardId}";
		var response = await client.GetAsync(url);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var result = (await response.Content.ReadFromJsonAsync<StudentModuleDashboardSectionDto>(JsonOptions))!;
		result.AttentionTotalCount.ShouldBeGreaterThanOrEqualTo(6); result.AttentionItems.Count.ShouldBe(4);
		result.AttentionItems.ShouldAllBe(item => item.Href.Contains("?case=") && item.Href.Contains("dashboard=attention"));
		result.OnboardingTasks.Single(t => t.Id == "division").Value.ShouldBeGreaterThanOrEqualTo(6);
		result.OnboardingTasks.Single(t => t.Id == "ready").Value.ShouldBe(0);
		var filtered = await client.GetFromJsonAsync<StudentModuleDashboardSectionDto>($"{Root}/sections/admissions?academicYearId={Factory.SeededAcademicYearId}&boardId={Guid.NewGuid()}", JsonOptions);
		filtered!.Metrics.ShouldAllBe(metric => metric.Value == 0); filtered.AttentionItems.ShouldBeEmpty();
	}

	[Fact]
	public async Task Dashboard_filters_do_not_require_student_directory_permission_and_reject_foreign_years()
	{
		var id = await SeedAdminAsync(); using var client = AdminClient(id, P.StudentOnboarding.View, ShikshaMitraSubModulePermissions.StudentOnboarding);
		var response = await client.GetAsync($"{Root}/filters?academicYearId={Factory.SeededAcademicYearId}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var filters = await response.Content.ReadFromJsonAsync<StudentModuleDashboardFiltersDto>(JsonOptions);
		filters!.Boards.ShouldContain(b => b.Id == Factory.SeededBoardId);
		filters.Divisions.ShouldContain(d => d.Id == Factory.SeededDivisionId && d.ParentId == Factory.SeededStandardId);
		(await client.GetAsync($"{Root}/filters?academicYearId={Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var denied = Permissions();
		(await denied.GetAsync($"{Root}/filters?academicYearId={Factory.SeededAcademicYearId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Attendance_pending_queue_requires_owning_permission_and_supports_filtered_empty_population()
	{
		var id = await SeedAdminAsync(); using var denied = AdminClient(id);
		var url = $"{Root}/attendance-registers?academicYearId={Factory.SeededAcademicYearId}";
		(await denied.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var client = AdminClient(id, P.Attendance_MarkAttendance.View, ShikshaMitraSubModulePermissions.Attendance);
		var response = await client.GetAsync(url + $"&boardId={Guid.NewGuid()}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var queue = await response.Content.ReadFromJsonAsync<Volo.Abp.Application.Dtos.PagedResultDto<StudentModuleDashboardRegisterDto>>(JsonOptions);
		queue!.TotalCount.ShouldBe(0); queue.Items.ShouldBeEmpty();
		(await client.GetAsync(url + "&maxResultCount=101")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private HttpClient AdminClient(Guid id, params string[] permissions)
	{
		var client = Permissions(permissions.Prepend(P.Dashboard.View).ToArray());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, id.ToString()); return client;
	}

	private async Task<Guid> SeedAdminAsync()
	{
		var id = await SeedUserAsync();
		await WithTenantAsync(async services => {
			var tenant = Guid.Parse(TestAuthHandler.TestTenantId); var roles = services.GetRequiredService<IdentityRoleManager>();
			if (!await roles.RoleExistsAsync("Admin")) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "Admin", tenant))).Succeeded.ShouldBeTrue();
			var users = services.GetRequiredService<IdentityUserManager>();
			(await users.AddToRoleAsync(await users.GetByIdAsync(id), "Admin")).Succeeded.ShouldBeTrue();
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenant, userId: id), true);
		});
		return id;
	}
}
