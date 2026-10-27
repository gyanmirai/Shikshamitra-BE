using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Xunit;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.BlobStoring;
using Volo.Abp.Identity;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Reports;

[Collection(ApiTestCollection.Name), TestModule(TestTraits.Modules.HR)]
public class HrTableExportApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Root = "/api/hr/table-exports";
	private static HrTableExportRequest Request() => new() { Table = "employee-table", Query = JsonSerializer.SerializeToElement(new { }), Columns = [new() { Id = "fullName", Label = "Staff" }] };
	[Theory, TestTier(TestTraits.Tiers.Critical)]
	[InlineData("excel")]
	[InlineData("pdf")]
	public async Task ICard_Selection_Should_Remain_Query_Only_Even_With_Directory_View(string format)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var view = Factory.CreateClient();
		view.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View);
		var request = Request(); request.Table = "employee-icard-selection-table"; request.Format = format;
		var query = await view.PostAsJsonAsync("/api/hr/table-query", new HrOperationalTableQueryRequest { Table = request.Table, Query = request.Query, Size = 5 }, JsonOptions);
		query.StatusCode.ShouldBe(HttpStatusCode.OK, await query.Content.ReadAsStringAsync());
		var response = await view.PostAsJsonAsync(Root, request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		(await response.Content.ReadAsStringAsync()).ShouldContain("I-card document workflow");
	}
	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Legacy_ICard_Export_With_Missing_Staff_Ids_Should_Not_Download_After_Staff_Leaves_Current_Directory_Scope()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employeeId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "ICardExportScope");
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var userId = Guid.NewGuid();
		var stageId = EmployeeTestHelper.DefaultMediumStageId;
		stageId.ShouldNotBe(Guid.Empty);
		var request = Request(); request.Table = "employee-icard-selection-table";
		var runId = Guid.NewGuid();
		string staffName;
		using (var services = Factory.Services.CreateScope())
		using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (var unit = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			await services.ServiceProvider.GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(userId, "icard-reader-" + userId.ToString("N"), "icard-" + userId.ToString("N") + "@example.invalid", tenantId));
			await services.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: userId, educationMediumStageId: stageId));
			var organizations = services.ServiceProvider.GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>();
			if (!await organizations.AnyAsync(item => item.EmployeeId == employeeId && item.EducationMediumStageId == stageId))
				await organizations.InsertAsync(new EmployeeOrganizationAssignment(Guid.NewGuid(), tenantId, employeeId, stageId));
			staffName = (await services.ServiceProvider.GetRequiredService<IEmployeeRepository>().GetAsync(employeeId)).FirstName!;
			await services.ServiceProvider.GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
			await unit.CompleteAsync();
		}
		using var view = Factory.CreateClient();
		view.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, userId.ToString());
		view.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View);
		var queryRequest = new HrOperationalTableQueryRequest { Table = request.Table, Query = request.Query, Size = 100, Criteria = new() { Search = staffName } };
		var before = await view.PostAsJsonAsync("/api/hr/table-query", queryRequest, JsonOptions);
		before.StatusCode.ShouldBe(HttpStatusCode.OK, await before.Content.ReadAsStringAsync());
		(await before.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("items").EnumerateArray().ShouldContain(item => item.GetProperty("id").GetGuid() == employeeId);
		using (var services = Factory.Services.CreateScope())
		using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (var unit = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			// Simulate the old generic export metadata that omitted EmployeeSummary.id.
			var run = new HrReportRun(runId, tenantId, userId, "Legacy I-card selection", "table-export", 1, "table:" + request.Table, "{}", "{}", JsonSerializer.Serialize(new HrReportExportScopeSnapshot { TenantId = tenantId, MediumStageIds = [stageId] }), DateTimeOffset.UtcNow);
			run.ExtraProperties["tableExportRequest"] = JsonSerializer.Serialize(request, HrReportingEngine.Json);
			run.Start(run.AttemptId, DateTimeOffset.UtcNow).ShouldBeTrue();
			run.Complete(run.AttemptId, 1, JsonSerializer.Serialize(new HrReportingResult { EmployeeIds = [], EmployeeCodes = [], Rows = [new Dictionary<string, object?> { ["fullName"] = staffName }] }, HrReportingEngine.Json));
			run.AttachExport("excel");
			await services.ServiceProvider.GetRequiredService<IRepository<HrReportRun, Guid>>().InsertAsync(run, true);
			await services.ServiceProvider.GetRequiredService<IBlobContainer>().SaveAsync(HrReportingWorkspaceAppService.BlobName(run, "excel"), new byte[] { 1, 2, 3 }, overrideExisting: false);
			await services.ServiceProvider.GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>().DeleteAsync(item => item.EmployeeId == employeeId, true);
			await unit.CompleteAsync();
		}
		var after = await view.PostAsJsonAsync("/api/hr/table-query", queryRequest, JsonOptions);
		after.StatusCode.ShouldBe(HttpStatusCode.OK, await after.Content.ReadAsStringAsync());
		(await after.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("items").EnumerateArray().ShouldNotContain(item => item.GetProperty("id").GetGuid() == employeeId);
		var download = await view.GetAsync(Root + "/" + runId + "/download");
		download.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await download.Content.ReadAsStringAsync());
		(await download.Content.ReadAsStringAsync()).ShouldContain("I-card document workflow");
	}
	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Source_View_Should_Allow_Export_Without_Reports_Permissions()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var view = Factory.CreateClient();
		view.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View);
		var response = await view.PostAsJsonAsync(Root, Request(), JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var job = (await response.Content.ReadFromJsonAsync<HrTableExportStatus>(JsonOptions))!;
		job.Status.ShouldBe("Queued");
		(await view.GetAsync(Root + "/" + job.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);
		using var revoked = Factory.CreateClient();
		revoked.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.HR.Reports.Export);
		(await revoked.GetAsync(Root + "/" + job.Id)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await revoked.GetAsync(Root + "/" + job.Id + "/download")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Another_User_Or_Tenant_Should_Not_Read_Or_Cancel_An_Export()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var response = await Client.PostAsJsonAsync(Root, Request(), JsonOptions);
		response.EnsureSuccessStatusCode();
		var job = (await response.Content.ReadFromJsonAsync<HrTableExportStatus>(JsonOptions))!;
		using var other = Factory.CreateClient(); other.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
		(await other.GetAsync(Root + "/" + job.Id)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await other.PostAsync(Root + "/" + job.Id + "/cancel", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		Guid otherTenantId;
		using (var services = Factory.Services.CreateScope())
		using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null))
		using (var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var created = await services.ServiceProvider.GetRequiredService<ITenantManager>().CreateAsync("Table export isolation " + Guid.NewGuid().ToString("N"));
			await services.ServiceProvider.GetRequiredService<ITenantRepository>().InsertAsync(created, true);
			otherTenantId = created.Id; await uow.CompleteAsync();
		}
		using var tenant = Factory.CreateClient(); tenant.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, otherTenantId.ToString());
		(await tenant.GetAsync(Root + "/" + job.Id)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Unknown_Table_And_Sensitive_Columns_Should_Be_Rejected()
	{
		var request = Request(); request.Table = "AppEmployees";
		(await Client.PostAsJsonAsync(Root, request, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		request = Request(); request.Columns[0].Id = "bankAccountNumber";
		(await Client.PostAsJsonAsync(Root, request, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}
	[Theory, TestTier(TestTraits.Tiers.Critical)]
	[InlineData("hr-earning-components", "earningComponentName", ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.View)]
	[InlineData("hr-deduction-components", "deductionComponentName", ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.View)]
	[InlineData("hr-pf-rules", "name", ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsPfRule.View)]
	[InlineData("hr-professional-tax", "taxAmount", ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsProfessionalTax.View)]
	[InlineData("hr-shifts", "shiftName", ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_Setting_ShiftManagement.View)]
	public async Task Controller_Only_Readers_Should_Recheck_Their_Exact_Screen_Permission(string table, string field, string permission)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var input = Request(); input.Table = table; input.Columns = [new() { Id = field, Label = "Value" }];
		using var denied = Factory.CreateClient();
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.HR.Reports.Export);
		(await denied.PostAsJsonAsync(Root, input, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var allowed = Factory.CreateClient();
		allowed.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission);
		var response = await allowed.PostAsJsonAsync(Root, input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
	}
	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Role_Claims_Should_Not_Override_Current_Directory_Scope()
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "admin");
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View);
		var page = await client.GetFromJsonAsync<ShikshaMitra.HR.Employees.EmployeeListResponse>("/api/employees?page=1&pageSize=10", JsonOptions);
		page!.Items.ShouldBeEmpty(); page.TotalCount.ShouldBe(0);
	}

    [Fact, TestTier(TestTraits.Tiers.Critical)]
    public async Task Attendance_Import_Rows_Should_Page_All_Rows_And_Reject_No_Scope_Exports()
    {
        await EnsureDefaultTestUserHasTenantWideScopeAsync();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
        var batchId = Guid.NewGuid();
        using (var services = Factory.Services.CreateScope())
        using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
        using (var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            var batch = new ShikshaMitra.HR.Payroll.AttendanceImports.AttendanceImportBatch(batchId, tenantId, "scope-test.csv", Guid.Parse(TestAuthHandler.TestUserId), "Test", DateTime.UtcNow, "2026-10", "2026-10", 61, 61, 0, 61, 0, [], []);
            await services.ServiceProvider.GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<ShikshaMitra.HR.Payroll.AttendanceImports.AttendanceImportBatch, Guid>>().InsertAsync(batch, true);
            var rows = services.ServiceProvider.GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<ShikshaMitra.HR.Payroll.AttendanceImports.AttendanceImportRow, Guid>>();
            for (var i = 1; i <= 61; i++) await rows.InsertAsync(new(Guid.NewGuid(), tenantId, batchId, i, "create", "2026-10-01", "UNKNOWN-" + i, "Import staff " + i, null, "09:00", "17:00", "Present", null, [], []));
            await uow.CompleteAsync();
        }
        var first = await GetAndReadAsync<Volo.Abp.Application.Dtos.PagedResultDto<ShikshaMitra.HR.Payroll.AttendanceImports.AttendanceImportRowDto>>($"/api/hr/attendance/automated-runs/{batchId}/rows?maxResultCount=20");
        first.TotalCount.ShouldBe(61); first.Items.Count.ShouldBe(20);
        var last = await GetAndReadAsync<Volo.Abp.Application.Dtos.PagedResultDto<ShikshaMitra.HR.Payroll.AttendanceImports.AttendanceImportRowDto>>($"/api/hr/attendance/automated-runs/{batchId}/rows?skipCount=60&maxResultCount=20");
        last.Items.Count.ShouldBe(1); last.Items[0].RowNumber.ShouldBe(61);
        using var denied = Factory.CreateClient();
        denied.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
        denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_BiometricAttendance.View);
        (await denied.GetAsync($"/api/hr/attendance/automated-runs/{batchId}/rows")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var request = Request(); request.Table = "hr-attendance-import-rows"; request.Query = JsonSerializer.SerializeToElement(new { importId = batchId }); request.Columns = [new() { Id = "employeeName", Label = "Staff" }];
        (await denied.PostAsJsonAsync(Root, request, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
