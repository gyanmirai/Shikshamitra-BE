using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Policies;

[Collection(ApiTestCollection.Name), TestModule(TestTraits.Modules.HR), TestTier(TestTraits.Tiers.Critical)]
public partial class HrSetupReviewApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private static string Url(Guid year, string section) => $"/api/hr/setup-review/{year}/{section}";
	private sealed record School(Guid TenantId, Guid ActorId, Guid YearId, Guid CalendarId);

	[Theory]
	[InlineData("calendar"), InlineData("attendance"), InlineData("leave"), InlineData("payroll")]
	public async Task Valid_defaults_are_not_a_review_and_recording_preserves_configuration(string section)
	{
		var school = await SeedAsync(); using var client = As(school);
		var before = await GetAsync(client, school, section);
		before.ConfigurationState.ShouldBe("valid"); before.ReviewState.ShouldBe("record_not_present");
		before.StarterInitialized.ShouldBeFalse(); // Starter-like names alone are not provenance.
		var started = DateTimeOffset.UtcNow;
		var response = await client.PostAsJsonAsync(Url(school.YearId, section), new {
			before.Fingerprint, before.ConcurrencyStamp, ActorId = Guid.NewGuid(), ReviewedAt = "1990-01-01T00:00:00Z", TenantId = Guid.NewGuid() });
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var saved = (await response.Content.ReadFromJsonAsync<HrSetupReviewDto>())!;
		saved.ReviewState.ShouldBe("reviewed"); saved.Fingerprint.ShouldBe(before.Fingerprint);
		saved.ReviewedBy.ShouldBe(school.ActorId); saved.ReviewedAt!.Value.ShouldBeGreaterThanOrEqualTo(started.AddSeconds(-1));
		(await GetAsync(client, school, section)).ReviewState.ShouldBe("reviewed");
		await InAsync(school.TenantId, async services => {
			var year = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(school.YearId);
			AcademicYearHrSetupReview.Read(year).Sections[section].ActorId.ShouldBe(school.ActorId);
			var policy = (await services.GetRequiredService<ILeavePolicyRepository>().GetListByAcademicYearIdAsync(year.Id)).Single();
			policy.Rules.ShouldAllBe(x => x.LeaveCount == 0);
		});
	}

	[Fact]
	public async Task Starter_markers_are_separate_from_zero_allocation_and_review_evidence()
	{
		var school = await SeedAsync(starter: true); using var client = As(school);
		var leave = await GetAsync(client, school, "leave");
		leave.StarterInitialized.ShouldBeTrue(); leave.TenantStarterEnabled.ShouldBeTrue();
		leave.ReviewState.ShouldBe("record_not_present"); leave.Notices.ShouldContain(x => x.Contains("Zero leave allocation"));
		(await client.PostAsJsonAsync(Url(school.YearId, "leave"), Record(leave))).IsSuccessStatusCode.ShouldBeTrue();
		(await GetAsync(client, school, "leave")).Notices.ShouldContain(x => x.Contains("Zero leave allocation"));
	}

	[Fact]
	public async Task Missing_defaults_are_distinct_and_cannot_be_acknowledged_as_reviewed()
	{
		var school = await SeedAsync(policies: false); using var client = As(school);
		var missing = await GetAsync(client, school, "payroll");
		missing.ConfigurationState.ShouldBe("missing"); missing.ReviewState.ShouldBe("record_not_present"); missing.CanReview.ShouldBeFalse();
		(await client.PostAsJsonAsync(Url(school.YearId, "payroll"), Record(missing))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Configuration_drift_stales_record_and_rejects_old_fingerprint()
	{
		var school = await SeedAsync(); using var client = As(school);
		var initial = await GetAsync(client, school, "calendar");
		(await client.PostAsJsonAsync(Url(school.YearId, "calendar"), Record(initial))).EnsureSuccessStatusCode();
		var reviewed = await GetAsync(client, school, "calendar");
		await InAsync(school.TenantId, async services => {
			var repository = services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>();
			var calendar = await repository.GetAsync(school.CalendarId);
			calendar.Update("Changed synthetic calendar", calendar.Status, calendar.EffectiveFrom, calendar.EffectiveTo,
				AttendanceWeeklyOffPattern.Sunday, [], calendar.HolidaySyncMode);
			await repository.UpdateAsync(calendar, true);
		});
		var stale = await GetAsync(client, school, "calendar");
		stale.ReviewState.ShouldBe("stale"); stale.ReviewedBy.ShouldBe(school.ActorId); stale.Fingerprint.ShouldNotBe(reviewed.Fingerprint);
		var rejected = await client.PostAsJsonAsync(Url(school.YearId, "calendar"), Record(reviewed));
		rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest); (await rejected.Content.ReadAsStringAsync()).ShouldContain(HrSetupReviewSections.Conflict);
	}

	[Fact]
	public async Task Concurrent_review_revisions_do_not_overwrite_other_sections()
	{
		var school = await SeedAsync(); using var client = As(school);
		var calendar = await GetAsync(client, school, "calendar"); var attendance = await GetAsync(client, school, "attendance");
		(await client.PostAsJsonAsync(Url(school.YearId, "calendar"), Record(calendar))).EnsureSuccessStatusCode();
		var rejected = await client.PostAsJsonAsync(Url(school.YearId, "attendance"), Record(attendance));
		rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var current = await GetAsync(client, school, "attendance");
		(await client.PostAsJsonAsync(Url(school.YearId, "attendance"), Record(current))).EnsureSuccessStatusCode();
		(await GetAsync(client, school, "calendar")).ReviewState.ShouldBe("reviewed");
		(await GetAsync(client, school, "attendance")).ReviewState.ShouldBe("reviewed");
	}

	[Theory]
	[InlineData("calendar", "HR.Policies.View", "HR.Policies.Edit")]
	[InlineData("attendance", "HR.Policies.View", "HR.Policies.Edit")]
	[InlineData("payroll", "HR.Policies.View", "HR.Policies.Edit")]
	[InlineData("leave", "HR.LeaveManagement.Setting.LeavePolicy.View", "HR.LeaveManagement.Setting.LeavePolicy.Edit")]
	public async Task Exact_view_and_write_grants_are_required_independently(string section, string view, string edit)
	{
		var school = await SeedAsync(); using var client = As(school);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, view);
		var snapshot = await GetAsync(client, school, section); snapshot.CanReview.ShouldBeFalse();
		(await client.PostAsJsonAsync(Url(school.YearId, section), Record(snapshot))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, edit);
		(await client.GetAsync(Url(school.YearId, section))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.PostAsJsonAsync(Url(school.YearId, section), Record(snapshot))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, view + "," + edit);
		(await client.PostAsJsonAsync(Url(school.YearId, section), Record(snapshot))).EnsureSuccessStatusCode();
	}

	[Fact]
	public async Task Tenant_year_actor_and_fingerprint_boundaries_are_server_authoritative()
	{
		var school = await SeedAsync(); var foreign = await SeedAsync(); using var client = As(school);
		var snapshot = await GetAsync(client, school, "calendar");
		(await client.GetAsync(Url(foreign.YearId, "calendar"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await client.PostAsJsonAsync(Url(foreign.YearId, "calendar"), Record(snapshot))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await client.PostAsJsonAsync(Url(school.YearId, "calendar"), new RecordHrSetupReviewDto { Fingerprint = new string('0', 64), ConcurrencyStamp = snapshot.ConcurrencyStamp })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		using var forged = As(school with { ActorId = foreign.ActorId });
		(await forged.GetAsync(Url(school.YearId, "calendar"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var anonymous = CreateAnonymousClient();
		(await anonymous.GetAsync(Url(school.YearId, "calendar"))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task No_staff_is_not_a_completed_sample_and_sample_read_requires_both_section_views()
	{
		var school = await SeedAsync(); using var client = As(school);
		var sample = await GetAsync(client, school, "sample");
		sample.ConfigurationState.ShouldBe("no_staff"); sample.ReviewState.ShouldBe("record_not_present"); sample.CanReview.ShouldBeFalse();
		(await client.PostAsJsonAsync(Url(school.YearId, "sample"), Record(sample))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "HR.Policies.View,HR.Policies.PreviewImpact");
		(await client.GetAsync(Url(school.YearId, "sample"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Sample_uses_scoped_staff_and_actual_resolver_and_is_stale_after_staff_change()
	{
		var school = await SeedAsync(withStaff: true); using var client = As(school);
		var sample = await GetAsync(client, school, "sample");
		sample.ConfigurationState.ShouldBe("valid"); sample.EmployeeId.ShouldBe(school.ActorId); sample.Date.ShouldBe(new DateTime(2026, 6, 1));
		sample.Details.ShouldContain(x => x.Contains("source default"));
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "HR.Policies.View,HR.LeaveManagement.Setting.LeavePolicy.View");
		(await client.PostAsJsonAsync(Url(school.YearId, "sample"), Record(sample))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "HR.Policies.View,HR.LeaveManagement.Setting.LeavePolicy.View,HR.Policies.PreviewImpact");
		(await client.PostAsJsonAsync(Url(school.YearId, "sample"), Record(sample))).EnsureSuccessStatusCode();
		(await GetAsync(client, school, "sample")).ReviewState.ShouldBe("reviewed");
		await InAsync(school.TenantId, async services => {
			var repository = services.GetRequiredService<IRepository<Employee, Guid>>();
			var employee = await repository.GetAsync(school.ActorId);
			employee.SetProperty("SyntheticReviewChange", true); await repository.UpdateAsync(employee, true);
		});
		(await GetAsync(client, school, "sample")).ReviewState.ShouldBe("stale");
		(await client.GetAsync(Url(school.YearId, "sample") + "?date=2030-01-01")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await client.GetAsync(Url(school.YearId, "sample") + $"?employeeId={Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Existing_staff_without_actor_scope_is_deferred_and_never_substituted_for_no_staff()
	{
		var school = await SeedAsync(withStaff: true, staffScope: false); using var client = As(school);
		var sample = await GetAsync(client, school, "sample");
		sample.ConfigurationState.ShouldBe("deferred"); sample.CanReview.ShouldBeFalse(); sample.EmployeeId.ShouldBeNull();
		(await client.GetAsync(Url(school.YearId, "sample") + $"?employeeId={school.ActorId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Leave_rule_changes_invalidate_review_without_backfilling_other_allocations()
	{
		var school = await SeedAsync(); using var client = As(school);
		var before = await GetAsync(client, school, "leave");
		(await client.PostAsJsonAsync(Url(school.YearId, "leave"), Record(before))).EnsureSuccessStatusCode();
		await InAsync(school.TenantId, async services => {
			var repository = services.GetRequiredService<ILeavePolicyRepository>();
			var policy = (await repository.GetListByAcademicYearIdAsync(school.YearId)).Single();
			var rule = policy.Rules.First();
			rule.Update(rule.PaidType, rule.EntitlementMode, rule.AccrualPolicyType, 5, rule.CarryForward, rule.CarryForwardLimit,
				rule.Encashable, rule.EncashmentTiming, rule.RequiresApproval, rule.MinNoticeDays, rule.MaxDaysPerRequest,
				rule.AllowHalfDay, rule.AttachmentRequiredAfterDays, rule.AllowNegativeBalance, rule.IsActive, rule.PayrollCategory);
			await repository.UpdateAsync(policy, true);
		});
		var after = await GetAsync(client, school, "leave"); after.ReviewState.ShouldBe("stale");
		after.Notices.ShouldContain(x => x.Contains("Zero leave allocation"));
	}

	[Fact]
	public async Task Inactive_calendar_reference_is_invalid_separately_from_review_state()
	{
		var school = await SeedAsync(); using var client = As(school);
		await InAsync(school.TenantId, async services => {
			var repository = services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>();
			var calendar = await repository.GetAsync(school.CalendarId); calendar.Archive(); await repository.UpdateAsync(calendar, true);
		});
		var attendance = await GetAsync(client, school, "attendance");
		attendance.ConfigurationState.ShouldBe("invalid"); attendance.ReviewState.ShouldBe("record_not_present"); attendance.CanReview.ShouldBeFalse();
		(await client.PostAsJsonAsync(Url(school.YearId, "attendance"), Record(attendance))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Direct_application_calls_require_a_school_identity_without_a_controller()
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null);
		var service = scope.ServiceProvider.GetRequiredService<IHrSetupReviewAppService>();
		await Should.ThrowAsync<Volo.Abp.Authorization.AbpAuthorizationException>(() => service.GetAsync(Guid.NewGuid(), "calendar", new()));
		await Should.ThrowAsync<Volo.Abp.Authorization.AbpAuthorizationException>(() => service.RecordAsync(Guid.NewGuid(), "calendar", new() {
			Fingerprint = new string('a', 64), ConcurrencyStamp = "synthetic-revision" }));
	}

	private static RecordHrSetupReviewDto Record(HrSetupReviewDto value) => new() {
		Fingerprint = value.Fingerprint, ConcurrencyStamp = value.ConcurrencyStamp, EmployeeId = value.EmployeeId, Date = value.Date };

	[Fact]
	public async Task A_recorded_sample_outside_changed_year_is_stale_and_new_sample_read_is_deliberate()
	{
		var school = await SeedAsync(withStaff: true); using var client = As(school);
		var sample = await GetAsync(client, school, "sample");
		(await client.PostAsJsonAsync(Url(school.YearId, "sample"), Record(sample))).EnsureSuccessStatusCode();
		await InAsync(school.TenantId, async services => {
			var repository = services.GetRequiredService<IRepository<AcademicYear, Guid>>();
			var year = await repository.GetAsync(school.YearId);
			await services.GetRequiredService<AcademicYearManager>().UpdateAsync(year, year.Name, year.StartDate.AddDays(1), year.EndDate);
			await repository.UpdateAsync(year, true);
		});
		var stale = await GetAsync(client, school, "sample");
		stale.ConfigurationState.ShouldBe("deferred"); stale.ReviewState.ShouldBe("stale"); stale.CanReview.ShouldBeFalse();
		var fresh = (await client.GetFromJsonAsync<HrSetupReviewDto>(Url(school.YearId, "sample") + "?newSample=true"))!;
		fresh.ConfigurationState.ShouldBe("valid"); fresh.ReviewState.ShouldBe("stale"); fresh.Date.ShouldBe(new DateTime(2026, 6, 2));
		(await GetAsync(client, school, "sample")).ConfigurationState.ShouldBe("deferred");
	}
	private async Task<HrSetupReviewDto> GetAsync(HttpClient client, School school, string section) {
		var response = await client.GetAsync(Url(school.YearId, section));
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<HrSetupReviewDto>())!;
	}
	private HttpClient As(School school) {
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, school.TenantId.ToString());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, school.ActorId.ToString());
		return client;
	}
	private async Task<School> SeedAsync(bool starter = false, bool policies = true, bool withStaff = false, bool staffScope = true) {
		Guid tenantId = default;
		await InAsync(null, async services => {
			var tenant = await services.GetRequiredService<TenantManager>().CreateAsync($"SetupReview-{Guid.NewGuid():N}");
			tenant.SetProperty(HrStarterPolicyDefaults.TenantEnabledProperty, starter);
			await services.GetRequiredService<ITenantRepository>().InsertAsync(tenant, true); tenantId = tenant.Id;
		});
		School result = null!;
		await InAsync(tenantId, async services => {
			var actor = new IdentityUser(Guid.NewGuid(), "reviewer", "reviewer@example.test", tenantId);
			await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(actor, true);
			if (withStaff) {
				var role = new IdentityRole(Guid.NewGuid(), "Synthetic Staff", tenantId);
				await services.GetRequiredService<IIdentityRoleRepository>().InsertAsync(role, true);
				await services.GetRequiredService<IRepository<Employee, Guid>>().InsertAsync(HrSetupReviewTestData.Employee(actor.Id, tenantId, role.Id), true);
				if (staffScope) await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: actor.Id,
					description: "Synthetic setup-review scope"), true);
			}
			var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync("Synthetic review year", new(2026, 6, 1), new(2027, 5, 31));
			year.SetProperty(HrStarterPolicyDefaults.YearInitializedProperty, starter);
			await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
			var calendarId = HrStarterPolicyDefaults.RecordId(year, "calendar");
			if (policies) {
				// Synthetic defaults intentionally keep starter-like names even for a legacy-marker test.
				await services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>().InsertAsync(HrStarterPolicyDefaults.Calendar(calendarId, year), true);
				await services.GetRequiredService<IRepository<AttendancePolicy, Guid>>().InsertAsync(HrStarterPolicyDefaults.Attendance(Guid.NewGuid(), year, calendarId), true);
				await services.GetRequiredService<IRepository<PayrollPolicy, Guid>>().InsertAsync(HrStarterPolicyDefaults.Payroll(Guid.NewGuid(), year, calendarId), true);
				await services.GetRequiredService<ILeavePolicyRepository>().InsertAsync(HrStarterPolicyDefaults.Leave(Guid.NewGuid(), year, Guid.NewGuid(), Guid.NewGuid()), true);
			}
			result = new(tenantId, actor.Id, year.Id, calendarId);
		});
		return result;
	}
	private async Task InAsync(Guid? tenantId, Func<IServiceProvider, Task> action) {
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(scope.ServiceProvider); await unit.CompleteAsync();
	}
}
