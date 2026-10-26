using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ClosedXML.Excel;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Identity;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Reports;

// Keep the real HTTP pipeline, EF tenant filters, authorization and owning services, with a
// disposable database containing only the fixtures this read-only contract needs.
public class HrOperationalTableApiTestFactory : ShikshaMitraApiTestFactory
{
	protected override bool InitializeTestData => false;
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection([
			new("BackgroundWorkers:IsEnabled", "false")]));
	}
	protected override IHost CreateHost(IHostBuilder builder)
	{
		var host = base.CreateHost(builder);
		Task.Run(async () => {
			using var scope = host.Services.CreateScope();
			using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null);
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var entity = await scope.ServiceProvider.GetRequiredService<ITenantManager>().CreateAsync("Synthetic operational tables");
			typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(entity, Guid.Parse(TestAuthHandler.TestTenantId));
			await scope.ServiceProvider.GetRequiredService<IRepository<Tenant, Guid>>().InsertAsync(entity);
			using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(entity.Id))
				await scope.ServiceProvider.GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(Guid.Parse(TestAuthHandler.TestUserId), "synthetic-table-reader", "table-reader@example.invalid", entity.Id));
			await unit.CompleteAsync();
		}).GetAwaiter().GetResult();
		return host;
	}
}

[Collection(ApiTestCollection.Name), TestModule(TestTraits.Modules.HR)]
public class HrOperationalTableQueryApi_Tests(HrOperationalTableApiTestFactory factory) : ShikshaMitraApiTestBase(factory), IClassFixture<HrOperationalTableApiTestFactory>
{
	private const string Root = "/api/hr/table-query";
	private static HrOperationalTableQueryRequest Request(string table = "employee-table", string column = "fullName") => new() {
		Table = table, Query = JsonSerializer.SerializeToElement(new { monthYear = "10-2026" }), Size = 5,
		Criteria = new() { Sorts = [new() { Column = column, Direction = "desc" }] }
	};
	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Typed_Filters_Count_And_Sort_Should_Precede_Response_Pagination_Across_Source_Pages()
	{
		var prefix = "TableQuery-" + Guid.NewGuid().ToString("N") + "-";
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<DocumentSetting, Guid>>();
			await repository.InsertManyAsync(Enumerable.Range(0, 125).Select(index => new DocumentSetting(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId),
				prefix + index.ToString("000"), index % 20 + 1, false, [DocumentType.Pdf])), true);
			// Tenant filter must also hold while the source population is scanned.
			await repository.InsertAsync(new DocumentSetting(Guid.NewGuid(), Guid.NewGuid(), prefix + "other-tenant", 20, false, [DocumentType.Pdf]), true);
			await unit.CompleteAsync();
		}
		var native = (await Client.GetFromJsonAsync<DocumentListResponse>("/api/employees/documents?page=2&pageSize=100", JsonOptions))!;
		native.TotalCount.ShouldBe(125);
		native.Items.Single(item => item.Title == prefix + "119").DocumentSize.ShouldBe(20);
		var input = Request("hr-document-settings", "documentSize");
		input.Criteria.Filters = [new() { Column = "title", Type = "startsWith", Value = JsonSerializer.SerializeToElement(prefix) },
			new() { Column = "documentSize", Type = "greaterThan", Value = JsonSerializer.SerializeToElement(18m) }];
		var response = await Client.PostAsJsonAsync(Root, input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var first = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		first.GetProperty("totalCount").GetInt32().ShouldBe(12);
		first.GetProperty("items").GetArrayLength().ShouldBe(5);
		first.GetProperty("items")[0].GetProperty("title").GetString().ShouldBe(prefix + "019");
		input.Offset = 5;
		response = await Client.PostAsJsonAsync(Root, input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var second = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		second.GetProperty("items")[0].GetProperty("title").GetString().ShouldBe(prefix + "119");
		second.GetProperty("totalCount").GetInt32().ShouldBe(12);

		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var exportResponse = await Client.PostAsJsonAsync("/api/hr/table-exports", new HrTableExportRequest {
			Table = input.Table, Query = JsonSerializer.SerializeToElement(new { criteria = input.Criteria }, HrReportingEngine.Json),
			Columns = [new() { Id = "title", Label = "Document" }, new() { Id = "documentSize", Label = "Size" }]
		}, JsonOptions);
		exportResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await exportResponse.Content.ReadAsStringAsync());
		var export = (await exportResponse.Content.ReadFromJsonAsync<HrTableExportStatus>(JsonOptions))!;
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		{
			Guid attempt;
			using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
			{
				attempt = (await scope.ServiceProvider.GetRequiredService<IRepository<HrReportRun, Guid>>().GetAsync(export.Id)).AttemptId;
				await unit.CompleteAsync();
			}
			await scope.ServiceProvider.GetRequiredService<HrTableExportJob>().ExecuteAsync(new() { RunId = export.Id, AttemptId = attempt, TenantId = Guid.Parse(TestAuthHandler.TestTenantId) });
		}
		var state = (await Client.GetFromJsonAsync<HrTableExportStatus>($"/api/hr/table-exports/{export.Id}", JsonOptions))!;
		state.Status.ShouldBe("Completed", state.Error); state.RowCount.ShouldBe(12);
		using var workbook = new XLWorkbook(new MemoryStream(await Client.GetByteArrayAsync($"/api/hr/table-exports/{export.Id}/download")));
		var sheet = workbook.Worksheet("Report");
		sheet.LastColumnUsed()!.ColumnNumber().ShouldBe(2);
		sheet.Cell(6, 1).GetString().ShouldBe(prefix + "019");
		sheet.Cell(11, 1).GetString().ShouldBe(prefix + "119");
		sheet.Rows(6, 17).Select(row => row.Cell(1).GetString()).Distinct().Count().ShouldBe(12);
		sheet.RowsUsed().SelectMany(row => row.CellsUsed()).ShouldNotContain(cell => cell.GetString().Contains("other-tenant", StringComparison.Ordinal));
	}
	[Theory, TestTier(TestTraits.Tiers.Critical)]
	[InlineData("employee-table", "fullName")]
	[InlineData("employee-onboarding-table", "candidate")]
	[InlineData("hr-shifts", "shiftName")]
	[InlineData("hr-pf-rules", "name")]
	[InlineData("hr-professional-tax", "taxAmount")]
	[InlineData("hr-earning-components", "amount")]
	[InlineData("hr-deduction-components", "amount")]
	[InlineData("hr-icard-templates", "name")]
	[InlineData("hr-performance-templates", "name")]
	[InlineData("hr-performance-cycles", "name")]
	[InlineData("esi-employee-impact", "employeeName")]
	public async Task Reporting_Permission_Should_Not_Replace_The_Owning_Screen_View(string table, string field)
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.HR.Reports.Export);
		var response = await client.PostAsJsonAsync(Root, Request(table, field), JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}
	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Claimed_Admin_Role_Should_Not_Replace_Current_Directory_Row_Scope()
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "admin");
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View);
		var response = await client.PostAsJsonAsync(Root, Request(), JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var page = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		page.GetProperty("totalCount").GetInt32().ShouldBe(0);
		page.GetProperty("items").GetArrayLength().ShouldBe(0);
	}
	[Theory, TestTier(TestTraits.Tiers.Critical)]
	[InlineData("bankAccountNumber")]
	[InlineData("fullName; DROP TABLE AppEmployees")]
	[InlineData("actions")]
	public async Task Hidden_And_Unrecognized_Fields_Should_Be_Rejected(string column)
	{
		(await Client.PostAsJsonAsync(Root, Request(column: column), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}
	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Anonymous_Requests_Should_Not_Run_An_Operational_Query()
	{
		using var anonymous = CreateAnonymousClient();
		(await anonymous.PostAsJsonAsync(Root, Request(), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}
}
