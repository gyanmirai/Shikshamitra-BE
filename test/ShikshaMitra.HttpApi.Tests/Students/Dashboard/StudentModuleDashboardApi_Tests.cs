using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Records;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;
using Xunit.Abstractions;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.Students.Dashboard;

[Collection(StudentModuleDashboardApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
[TestTier(TestTraits.Tiers.Critical)]
public partial class StudentModuleDashboardApi_Tests(StudentModuleDashboardApiTestFactory factory, ITestOutputHelper output) : ShikshaMitraApiTestBase(factory)
{
	private readonly ITestOutputHelper _output = output;
	private const string Root = "/api/students/dashboard";
	private HttpClient Permissions(params string[] permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permissions.Length == 0 ? "__none__" : string.Join(',', permissions));
		return client;
	}

	[Fact]
	public async Task Dashboard_requires_authentication_and_exact_view_permission()
	{
		using var anonymous = CreateAnonymousClient();
		(await anonymous.GetAsync(Root + "/context")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		using var denied = Permissions();
		(await denied.GetAsync(Root + "/context")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await denied.GetAsync($"{Root}/sections/records?academicYearId={Factory.SeededAcademicYearId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Dashboard_only_returns_links_with_action_and_visibility_grants()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var dashboardOnly = Permissions(P.Dashboard.View);
		var common = await ReadContextAsync(dashboardOnly);
		common.Sections.ShouldBeEmpty(); common.Links.ShouldBeEmpty();
		using var actionOnly = Permissions(P.Dashboard.View, P.Records_StudentList.View);
		(await ReadContextAsync(actionOnly)).Links.ShouldBeEmpty();
		using var directory = Permissions(P.Dashboard.View, P.Records_StudentList.View, ShikshaMitraSubModulePermissions.Records);
		var context = await ReadContextAsync(directory);
		context.Sections.ShouldBe(["records"]);
		context.Links.Single().Href.ShouldBe("/student/records/directory");
	}

	[Fact]
	public async Task Missing_identity_and_foreign_tenant_cannot_read_context()
	{
		using var client = Permissions(P.Dashboard.View);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
		(await client.GetAsync(Root + "/context")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var foreign = Permissions(P.Dashboard.View);
		foreign.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		// ABP rejects an unknown tenant before the dashboard controller runs.
		(await foreign.GetAsync(Root + "/context")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Unauthorized_sections_and_foreign_years_fail_closed()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var client = Permissions(P.Dashboard.View);
		(await client.GetAsync($"{Root}/sections/records?academicYearId={Factory.SeededAcademicYearId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var records = Permissions(P.Dashboard.View, P.Records_StudentList.View, ShikshaMitraSubModulePermissions.Records);
		(await records.GetAsync($"{Root}/sections/records?academicYearId={Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await records.GetAsync($"{Root}/sections/unknown?academicYearId={Factory.SeededAcademicYearId}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Theory]
	[InlineData("records", P.Records_StudentList.View, ShikshaMitraSubModulePermissions.Records)]
	[InlineData("admissions", P.AdmissionApplications.View, ShikshaMitraSubModulePermissions.Enrollment)]
	[InlineData("attendance", P.Attendance_MarkAttendance.View, ShikshaMitraSubModulePermissions.Attendance)]
	public async Task Sections_translate_to_database_aggregates(string section, string permission, string visibility)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var client = Permissions(P.Dashboard.View, permission, visibility);
		var response = await client.GetAsync($"{Root}/sections/{section}?academicYearId={Factory.SeededAcademicYearId}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var result = await response.Content.ReadFromJsonAsync<StudentModuleDashboardSectionDto>(JsonOptions);
		result.ShouldNotBeNull(); result.Key.ShouldBe(section); result.AcademicYearId.ShouldBe(Factory.SeededAcademicYearId);
		result.Metrics.ShouldNotBeEmpty(); result.ClassStrength.Count.ShouldBeLessThanOrEqualTo(12);
		result.Metrics.ShouldAllBe(metric => metric.Permission == permission && metric.Href.StartsWith("/student/", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Direct_application_service_requires_dashboard_permission()
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var principal = scope.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (principal.Change(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity())))
		{
			var service = scope.ServiceProvider.GetRequiredService<IStudentModuleDashboardAppService>();
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetContextAsync());
		}
	}

	private static async Task<StudentModuleDashboardContextDto> ReadContextAsync(HttpClient client)
	{
		var response = await client.GetAsync(Root + "/context");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<StudentModuleDashboardContextDto>(JsonOptions))!;
	}
}
