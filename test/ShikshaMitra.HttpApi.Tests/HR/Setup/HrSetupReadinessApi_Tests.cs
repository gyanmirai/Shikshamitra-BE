using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Volo.Abp.Authorization;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.HR.Setup;

[Collection(HrSetupReadinessTestCollection.Name), TestModule(TestTraits.Modules.HR), TestTier(TestTraits.Tiers.Critical)]
public class HrSetupReadinessApi_Tests(HrSetupReadinessTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Url = "/api/hr/setup-readiness";
	private sealed record School(Guid Tenant, Guid Actor, Guid Year);

	[Fact]
	public async Task Empty_school_reports_configuration_without_creating_defaults_or_staff()
	{
		var school = await SeedAsync(); using var client = As(school, "HR.Employee.EmployeeList.Add");
		var result = await GetAsync(client, school.Year);
		result.IsReady.ShouldBeFalse();
		foreach (var code in new[] { "shift", "teaching-role", "non-teaching-role", "calendar", "payroll-policy", "leave-policy", "attendance-policy", "earnings", "salary-template" })
			result.Checks.Single(x => x.Code == code).State.ShouldBe("missing");
		result.Checks.Where(x => x.Code != "academic-year").All(x => !x.CanConfigure).ShouldBeTrue();
		await InAsync(school.Tenant, async services => {
			(await services.GetRequiredService<IRepository<EsiPayrollConfiguration, Guid>>().GetCountAsync()).ShouldBe(0);
			(await services.GetRequiredService<IRepository<EarningComponent, Guid>>().GetCountAsync()).ShouldBe(0);
			(await services.GetRequiredService<IRepository<Employee, Guid>>().GetCountAsync()).ShouldBe(0);
		});
	}

	[Fact]
	public async Task Each_existing_operator_action_can_read_configuration_readiness()
	{
		var school = await SeedAsync();
		foreach (var action in HrSetupPermissions.Operators)
		{
			using var client = As(school, action);
			var result = await GetAsync(client, school.Year);
			result.IsReady.ShouldBeFalse();
		}
	}

	[Fact]
	public async Task Usable_defaults_and_template_complete_setup_without_review_acknowledgements()
	{
		var school = await SeedAsync(complete: true); using var client = As(school);
		var result = await GetAsync(client, school.Year);
		result.IsReady.ShouldBeTrue(); result.Checks.ShouldAllBe(x => x.State == "ready");
		result.Checks.ShouldNotContain(x => x.Code == "pf" || x.Code == "esi" || x.Code == "deductions");
	}

	[Fact]
	public async Task Academic_year_is_explicit_and_tenant_is_server_authoritative()
	{
		var school = await SeedAsync(complete: true); var foreign = await SeedAsync(); using var client = As(school);
		var missingYear = (await client.GetFromJsonAsync<HrSetupReadinessDto>(Url))!;
		missingYear.IsReady.ShouldBeFalse(); missingYear.AcademicYearId.ShouldBeNull();
		missingYear.AvailableYears.ShouldAllBe(x => x.Id == school.Year);
		missingYear.Checks.Single(x => x.Code == "calendar").State.ShouldBe("unknown");
		(await client.GetAsync($"{Url}?academicYearId={foreign.Year}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		using var forged = As(school with { Actor = foreign.Actor });
		(await forged.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Theory]
	[InlineData("HR.LeaveManagement.LeaveCenter.View,HR.LeaveManagement.LeaveCenter.Add")]
	[InlineData("HR.Employee.EmployeeList.View")]
	[InlineData("None")]
	public async Task Self_service_and_read_only_users_cannot_read_tenant_setup(string grants)
	{
		var school = await SeedAsync(); using var client = As(school, grants);
		(await client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var anonymous = CreateAnonymousClient();
		(await anonymous.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Inactive_earnings_and_duplicate_defaults_are_not_ready()
	{
		var school = await SeedAsync(complete: true);
		await InAsync(school.Tenant, async services => {
			var earnings = services.GetRequiredService<IRepository<EarningComponent, Guid>>();
			var component = (await earnings.GetListAsync()).Single(); component.Deactivate(); await earnings.UpdateAsync(component, true);
			var year = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(school.Year);
			var calendar = (await services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>().GetListAsync()).Single();
			await services.GetRequiredService<IRepository<PayrollPolicy, Guid>>().InsertAsync(new PayrollPolicy(Guid.NewGuid(), school.Tenant, year.Id, calendar.Id, "Second default payroll", HrPolicyStatus.Active, true, DayCalculationType.WorkingDays, null, [], SaturdayWorkPolicy.FullDay, []), true);
		});
		using var client = As(school); var result = await GetAsync(client, school.Year);
		result.IsReady.ShouldBeFalse();
		result.Checks.Single(x => x.Code == "earnings").State.ShouldBe("inactive");
		result.Checks.Single(x => x.Code == "salary-template").State.ShouldBe("invalid");
		result.Checks.Single(x => x.Code == "payroll-policy").State.ShouldBe("conflicting");
	}

	[Fact]
	public async Task Calendar_references_and_employee_group_coverage_are_validated()
	{
		var school = await SeedAsync(complete: true);
		await InAsync(school.Tenant, async services => {
			var roles = services.GetRequiredService<IIdentityRoleRepository>(); var role = (await roles.GetListAsync()).Single();
			role.SetProperty(EmployeeGroupAudience.ExtraPropertyName, EmployeeGroupAudience.Teaching); await roles.UpdateAsync(role, true);
			var calendars = services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>(); var calendar = (await calendars.GetListAsync()).Single();
			calendar.Update(calendar.Name, HrPolicyStatus.Draft, calendar.EffectiveFrom, calendar.EffectiveTo, calendar.WeeklyOffPattern, calendar.GetCustomWeeklyOffs(), calendar.HolidaySyncMode);
			await calendars.UpdateAsync(calendar, true);
		});
		using var client = As(school); var result = await GetAsync(client, school.Year);
		result.Checks.Single(x => x.Code == "calendar").State.ShouldBe("inactive");
		result.Checks.Single(x => x.Code == "attendance-policy").State.ShouldBe("invalid");
		result.Checks.Single(x => x.Code == "teaching-role").State.ShouldBe("ready");
		result.Checks.Single(x => x.Code == "non-teaching-role").State.ShouldBe("missing");
	}

	[Fact]
	public async Task Invalid_legacy_fields_are_reported_as_invalid_configuration()
	{
		var school = await SeedAsync(complete: true);
		await InAsync(school.Tenant, async services => {
			var earnings = services.GetRequiredService<IRepository<EarningComponent, Guid>>();
			var earning = (await earnings.GetListAsync()).Single();
			typeof(EarningComponent).GetProperty(nameof(EarningComponent.Name))!.SetValue(earning, " ");
			await earnings.UpdateAsync(earning, true);
			var calendars = services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>();
			var calendar = (await calendars.GetListAsync()).Single();
			typeof(WorkCalendarPolicy).GetProperty(nameof(WorkCalendarPolicy.Name))!.SetValue(calendar, " ");
			await calendars.UpdateAsync(calendar, true);
		});
		using var client = As(school); var result = await GetAsync(client, school.Year);
		result.IsReady.ShouldBeFalse();
		result.Checks.Single(x => x.Code == "earnings").State.ShouldBe("invalid");
		result.Checks.Single(x => x.Code == "calendar").State.ShouldBe("invalid");
		result.Checks.Single(x => x.Code == "salary-template").State.ShouldBe("invalid");
	}

	[Fact]
	public async Task Only_template_required_statutory_dependencies_are_reported()
	{
		var school = await SeedAsync(complete: true);
		await InAsync(school.Tenant, async services => {
			var templates = services.GetRequiredService<IRepository<SalaryStructureTemplate, Guid>>();
			var original = (await templates.GetListAsync()).Single(); original.Archive(); await templates.UpdateAsync(original, true);
			var earning = (await services.GetRequiredService<IRepository<EarningComponent, Guid>>().GetListAsync()).Single();
			var template = new SalaryStructureTemplate(Guid.NewGuid(), school.Tenant, "PF dependencies", null);
			var version = new SalaryStructureTemplateVersion(Guid.NewGuid(), school.Tenant, template.Id, 1,
				[new("BASIC", earning.Id, SalaryTemplateLineKind.Earning, SalaryTemplateFormulaType.PercentageOfGross, PayrollComponentSemanticType.Basic, 100m, null, 1),
				 new("PF", Guid.NewGuid(), SalaryTemplateLineKind.Deduction, SalaryTemplateFormulaType.EmployeeProvidentFund, PayrollComponentSemanticType.EmployeeProvidentFund, null, "BASIC", 2)]);
			version.SetEsiAssignment(EsiManagement.External, null, "Synthetic external management."); version.Publish(new(2026, 4, 1)); template.MarkPublished(1);
			await templates.InsertAsync(template, true);
			await services.GetRequiredService<IRepository<SalaryStructureTemplateVersion, Guid>>().InsertAsync(version, true);
		});
		using var client = As(school); var result = await GetAsync(client, school.Year);
		result.IsReady.ShouldBeFalse();
		result.Checks.Single(x => x.Code == "pf").State.ShouldBe("missing");
		result.Checks.Single(x => x.Code == "deductions").State.ShouldBe("missing");
		result.Checks.ShouldNotContain(x => x.Code == "esi" || x.Code == "professional-tax");
	}

	[Fact]
	public async Task Direct_application_calls_enforce_operator_permissions_without_a_controller()
	{
		var school = await SeedAsync();
		await InAsync(school.Tenant, async services => {
			var accessor = services.GetRequiredService<IHttpContextAccessor>(); var previous = accessor.HttpContext;
			accessor.HttpContext = new DefaultHttpContext();
			accessor.HttpContext.Request.Headers[AlwaysGrantPermissionChecker.TestPermissionsHeader] = "None";
			using var principal = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
				[new Claim(AbpClaimTypes.UserId, school.Actor.ToString()), new Claim(AbpClaimTypes.TenantId, school.Tenant.ToString())], "Synthetic")));
			try { await Should.ThrowAsync<AbpAuthorizationException>(() => services.GetRequiredService<IHrSetupReadinessAppService>().GetAsync(school.Year)); }
			finally { accessor.HttpContext = previous; }
		});
	}

	private async Task<HrSetupReadinessDto> GetAsync(HttpClient client, Guid year)
	{
		var response = await client.GetAsync($"{Url}?academicYearId={year}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<HrSetupReadinessDto>())!;
	}
	private HttpClient As(School school, string? grants = null)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, school.Tenant.ToString());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, school.Actor.ToString());
		if (grants != null) client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, grants);
		return client;
	}
	private async Task<School> SeedAsync(bool complete = false)
	{
		Guid tenantId = default;
		await InAsync(null, async services => {
			var tenant = await services.GetRequiredService<TenantManager>().CreateAsync($"Readiness-{Guid.NewGuid():N}");
			await services.GetRequiredService<ITenantRepository>().InsertAsync(tenant, true); tenantId = tenant.Id;
		});
		School school = null!;
		await InAsync(tenantId, async services => {
			var actor = new IdentityUser(Guid.NewGuid(), "operator", "operator@example.test", tenantId);
			await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(actor, true);
			var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync("Readiness year", new(2026, 4, 1), new(2027, 3, 31));
			await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
			if (complete)
			{
				await services.GetRequiredService<IRepository<ShiftManagement, Guid>>().InsertAsync(HrStarterPolicyDefaults.Shift(tenantId), true);
				var role = new IdentityRole(Guid.NewGuid(), "Approved staff", tenantId);
				role.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.Employee);
				role.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, true);
				await services.GetRequiredService<IIdentityRoleRepository>().InsertAsync(role, true);
				var calendar = HrStarterPolicyDefaults.Calendar(Guid.NewGuid(), year);
				await services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>().InsertAsync(calendar, true);
				await services.GetRequiredService<IRepository<PayrollPolicy, Guid>>().InsertAsync(HrStarterPolicyDefaults.Payroll(Guid.NewGuid(), year, calendar.Id), true);
				await services.GetRequiredService<IRepository<AttendancePolicy, Guid>>().InsertAsync(HrStarterPolicyDefaults.Attendance(Guid.NewGuid(), year, calendar.Id), true);
				await services.GetRequiredService<ILeavePolicyRepository>().InsertAsync(HrStarterPolicyDefaults.Leave(Guid.NewGuid(), year, Guid.NewGuid(), Guid.NewGuid()), true);
				var earning = new EarningComponent(Guid.NewGuid(), tenantId, "Basic", null, EarningComponentType.Fixed, CalculationType.Amount, 0m, null, null, ComponentDependency.Gross, true);
				await services.GetRequiredService<IRepository<EarningComponent, Guid>>().InsertAsync(earning, true);
				var template = new SalaryStructureTemplate(Guid.NewGuid(), tenantId, "Usable", null);
				var version = new SalaryStructureTemplateVersion(Guid.NewGuid(), tenantId, template.Id, 1,
					[new("BASIC", earning.Id, SalaryTemplateLineKind.Earning, SalaryTemplateFormulaType.PercentageOfGross, PayrollComponentSemanticType.Basic, 100m, null, 1)]);
				version.SetEsiAssignment(EsiManagement.External, null, "Handled outside the platform for this synthetic fixture.");
				version.Publish(new(2026, 4, 1)); template.MarkPublished(1);
				await services.GetRequiredService<IRepository<SalaryStructureTemplate, Guid>>().InsertAsync(template, true);
				await services.GetRequiredService<IRepository<SalaryStructureTemplateVersion, Guid>>().InsertAsync(version, true);
			}
			school = new(tenantId, actor.Id, year.Id);
		});
		return school;
	}
	private async Task InAsync(Guid? tenantId, Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(scope.ServiceProvider); await unit.CompleteAsync();
	}
}
