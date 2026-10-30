using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using System.Net.Http.Json;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using ShikshaMitra.Infrastructure.Data;
using Xunit;
using Shouldly;

namespace ShikshaMitra.Dashboard;

[Collection(ApiTestCollection.Name)]
public class MainDashboardFocusApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData("teacher", "finance")]
	[InlineData("principalAdmin", "unknown")]
	[InlineData("studentParent", "leadership")]
	public async Task Rejects_Presentation_Outside_Template_Family(string template, string role)
	{
		var result = await Client.GetAsync($"/api/dashboard/summary?dashboardTemplate={template}&presentationRole={role}");
		result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Rejects_Unlinked_Student_Selection()
	{
		var result = await Client.GetAsync("/api/dashboard/summary?dashboardTemplate=studentParent&studentId=00000000-0000-0000-0000-000000000123");
		result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Rejects_An_Unbounded_Focus_Date_Window()
	{
		var result = await Client.GetAsync("/api/dashboard/summary?dashboardTemplate=leadership&from=2020-01-01&to=2026-10-29");
		result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Keeps_Existing_Arrays_Alongside_Additive_Focus_Contract()
	{
		var result = await Client.GetAsync("/api/dashboard/summary?dashboardTemplate=teacher&presentationRole=teacher");
		result.StatusCode.ShouldBe(HttpStatusCode.OK);
		using var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
		json.RootElement.GetProperty("presentationRole").GetString().ShouldBe("teacher");
		json.RootElement.GetProperty("students").GetArrayLength().ShouldBe(0);
		json.RootElement.TryGetProperty("selectedStudentId", out _).ShouldBeTrue();
		json.RootElement.TryGetProperty("focus", out _).ShouldBeTrue();
		json.RootElement.TryGetProperty("kpiCards", out _).ShouldBeTrue();
	}

	[Fact]
	public async Task Denied_Focus_Has_No_Operational_Payload()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraModulePermissions.Dashboard);
		var result = await client.GetAsync($"/api/dashboard/summary?dashboardTemplate=financeHr&presentationRole=finance&academicYearId={Factory.SeededAcademicYearId}");
		result.StatusCode.ShouldBe(HttpStatusCode.OK);
		var summary = await result.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions);
		summary!.Focus.ShouldBeNull();
		summary.KpiCards.ShouldBeEmpty();
		summary.ActionItems.ShouldBeEmpty();
	}

	[Fact]
	public async Task Module_Visibility_Does_Not_Grant_Finance_Or_Hr_Source_Overview()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(",", ShikshaMitraModulePermissions.Dashboard, ShikshaMitraModulePermissions.Finance, ShikshaMitraSubModulePermissions.HRDashboard));
		var response = await client.GetAsync($"/api/dashboard/summary?dashboardTemplate=financeHr&academicYearId={Factory.SeededAcademicYearId}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions);
		summary!.Focus.ShouldBeNull();
		summary.ModuleSnapshots.ShouldBeEmpty();
	}

	[Fact]
	public async Task Readiness_Only_Module_Link_Does_Not_Claim_Measured_Health()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(",", ShikshaMitraModulePermissions.Dashboard, ShikshaMitraModulePermissions.Transport));
		var response = await client.GetAsync("/api/dashboard/summary?dashboardTemplate=leadership");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions);
		var link = summary!.ModuleSnapshots.Single(x => x.Id == "leadership-transport-health");
		link.Health.ShouldBeNull();
		link.Status.ShouldBe("info");
	}

	[Fact]
	public async Task Finance_Without_A_Year_Does_Not_Fabricate_Zero_Collection_Or_Good_Health()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(",", ShikshaMitraModulePermissions.Dashboard, ShikshaMitraModulePermissions.Finance,
				ShikshaMitraSubModulePermissions.FinanceDashboard, ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.View));
		var response = await client.GetAsync("/api/dashboard/summary?dashboardTemplate=financeHr&presentationRole=finance");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions);
		var module = summary!.ModuleSnapshots.Single(x => x.Id == "finance-dashboard");
		module.Value!.ToString().ShouldBe("Open");
		module.Health.ShouldBeNull();
		module.Count.ShouldBeNull();
		summary.Focus!.Status.ShouldBe("unavailable");
	}

	[Fact]
	public async Task Payroll_Overview_Does_Not_Create_Or_Change_Saved_Preview_Rows()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		async Task<string> Snapshot()
		{
			using var services = Factory.Services.CreateScope();
			using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
			using var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var rows = await services.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().GetListAsync();
			return JsonSerializer.Serialize(rows.OrderBy(x => x.Id));
		}
		var before = await Snapshot();
		var response = await Client.GetAsync($"/api/dashboard/summary?dashboardTemplate=financeHr&presentationRole=hr&academicYearId={Factory.SeededAcademicYearId}&from=2026-05-01&to=2026-05-23");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await Snapshot()).ShouldBe(before);
	}

	[Theory]
	[InlineData("financeHr", "finance", "collections")]
	[InlineData("principalAdmin", "admissions", "pipeline")]
	public async Task Reads_Canonical_Scoped_Focus_With_No_Translation_Error(string template, string role, string kind)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var response = await Client.GetAsync($"/api/dashboard/summary?dashboardTemplate={template}&presentationRole={role}&academicYearId={Factory.SeededAcademicYearId}&from=2026-05-01&to=2026-05-23");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions);
		summary!.Focus.ShouldNotBeNull();
		summary.Focus.Kind.ShouldBe(kind);
		summary.Focus.Status.ShouldNotBe("error");
		summary.Focus.RequiredPermissions.ShouldNotBeEmpty();
		summary.Focus.AccessMode.ShouldBe("staff");
	}

	[Fact]
	public async Task Admissions_Anchors_Use_Exact_Application_View_And_Are_Denied_Without_It()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var allowed = Factory.CreateClient();
		allowed.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(",", ShikshaMitraModulePermissions.Dashboard, ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View));
		var response = await allowed.GetAsync($"/api/dashboard/summary?dashboardTemplate=principalAdmin&presentationRole=admissions&academicYearId={Factory.SeededAcademicYearId}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions);
		var admissions = summary!.KpiCards.Single(x => x.Id == "principal-pending-admissions");
		admissions.RequiredPermissions.ShouldBe([ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View]);
		admissions.DrilldownUrl.ShouldBe("/student/admissions/applications");
		summary.ZoneErrors.ShouldNotContainKey("actions");
		var denied = Factory.CreateClient();
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(",", ShikshaMitraModulePermissions.Dashboard, ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View));
		var hidden = await denied.GetFromJsonAsync<DashboardSummaryDto>($"/api/dashboard/summary?dashboardTemplate=principalAdmin&presentationRole=admissions&academicYearId={Factory.SeededAcademicYearId}", JsonOptions);
		hidden!.KpiCards.ShouldNotContain(x => x.Id == "principal-pending-admissions");
		hidden.Focus.ShouldBeNull();
	}
}
