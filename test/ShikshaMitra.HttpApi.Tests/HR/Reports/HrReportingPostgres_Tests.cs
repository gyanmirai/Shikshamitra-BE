using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.IO;
using ClosedXML.Excel;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.BlobStoring;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Reports;

public class HrReportingPostgresFactAttribute : FactAttribute
{
	public HrReportingPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HR_REPORTING_TEST_POSTGRES"))) Skip = "Set HR_REPORTING_TEST_POSTGRES to an empty disposable loopback reporting_tests_* database.";
	}
}
public class HrReportingPostgresFactory : ShikshaMitraApiTestFactory
{
	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("HR_REPORTING_TEST_POSTGRES"));
		if (connection.Host is not ("localhost" or "127.0.0.1" or "::1") || connection.Database?.StartsWith("reporting_tests_", StringComparison.Ordinal) != true) throw new InvalidOperationException("Reporting tests require an empty disposable loopback reporting_tests_* database.");
		services.Configure<AbpDbContextOptions>(options => options.Configure(context => context.DbContextOptions.UseNpgsql(connection.ConnectionString)));
		using var database = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection.ConnectionString).Options);
		database.GetService<IRelationalDatabaseCreator>().CreateTables();
	}
}

[Trait("Database", "PostgreSQL")]
[Collection(ApiTestCollection.Name)]
public class HrReportingPostgres_Tests(HrReportingPostgresFactory factory) : ShikshaMitraApiTestBase(factory), IClassFixture<HrReportingPostgresFactory>
{
	private const string Root = "/api/hr/reports/runs";
	[HrReportingPostgresFact]
	public async Task Retention_Should_Remove_Expired_Rows_And_Files_While_Keeping_History()
	{
		await PrepareIdentityAsync();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var run = await PostAndReadAsync<HrReportRunDto>(Root, new HrReportCreateRunDto { TemplateId = "builtin:staff-directory", Revision = 1 });
		using var services = Factory.Services.CreateScope();
		using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		await services.ServiceProvider.GetRequiredService<HrReportingRunJob>().ExecuteAsync(new() { RunId = run.Id, TenantId = tenantId, AttemptId = await AttemptAsync(run.Id) });
		var uows = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var blobs = services.ServiceProvider.GetRequiredService<IBlobContainer>();
		var names = new List<string>();
		using (var update = uows.Begin(requiresNew: true, isTransactional: true))
		{
			var db = await services.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			var stored = await db.Set<HrReportRun>().SingleAsync(x => x.Id == run.Id);
			stored.Status.ShouldBe("Completed");
			db.Entry(stored).Property(x => x.ExpiresAt).CurrentValue = DateTimeOffset.UtcNow.AddDays(-1);
			foreach (var format in new[] { "excel", "pdf" })
			{
				var name = HrReportingWorkspaceAppService.BlobName(stored, format);
				await blobs.SaveAsync(name, new byte[] { 1, 2, 3 }, overrideExisting: true); names.Add(name);
			}
			await update.CompleteAsync();
		}
		await services.ServiceProvider.GetRequiredService<HrReportingRetentionService>().PurgeAsync();
		var history = await GetAndReadAsync<HrReportRunDto>(Root + "/" + run.Id);
		history.Status.ShouldBe("Expired"); history.IsExpired.ShouldBeTrue(); history.Name.ShouldBe(run.Name);
		(await Client.GetAsync(Root + "/" + run.Id + "/rows")).IsSuccessStatusCode.ShouldBeFalse();
		foreach (var name in names) (await blobs.ExistsAsync(name)).ShouldBeFalse();
		using var check = uows.Begin(requiresNew: true);
		var pages = services.ServiceProvider.GetRequiredService<IRepository<HrReportRunPage, Guid>>();
		(await pages.CountAsync(x => x.RunId == run.Id)).ShouldBe(0);
		await check.CompleteAsync();
	}
	[HrReportingPostgresFact]
	public async Task PostgreSql_Reader_Should_Filter_And_Count_Beyond_20000_Staff_Rows()
	{
		await PrepareIdentityAsync();
		using var services = Factory.Services.CreateScope();
		using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		var uows = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		using (var seed = uows.Begin(requiresNew: true, isTransactional: true))
		{
			var db = await services.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			await db.Database.ExecuteSqlInterpolatedAsync($"""
				WITH clones AS MATERIALIZED (
				SELECT jsonb_populate_record(NULL::"AppEmployees", to_jsonb(source) || jsonb_build_object(
				  'Id', gen_random_uuid(), 'UserId', NULL, 'EmployeeCode', 'REPORT-BULK-' || n,
				  'Department', 'Reporting bulk acceptance', 'FirstName', 'Synthetic bulk staff',
				  'Email', NULL, 'PanNo', 'RBUL' || lpad(n::text, 6, '0'), 'AdharNo', '999' || lpad(n::text, 9, '0'))) AS employee
				FROM (SELECT * FROM "AppEmployees" WHERE "TenantId" = {Guid.Parse(TestAuthHandler.TestTenantId)} AND NOT "IsDeleted" LIMIT 1) source
				CROSS JOIN generate_series(1, 25001) n
				)
				INSERT INTO "AppEmployees" SELECT (employee).* FROM clones
				""");
			await seed.CompleteAsync();
		}
		try
		{
			var definition = HrReportingCatalogue.BuiltIns().Single(x => x.Id == "builtin:staff-directory").Definition;
			definition.Filters.Conditions = [new() { Field = "employeeCode", Operator = "equals", Value = "REPORT-BULK-25001" }];
			var input = new HrReportDesignerPreviewDto { Definition = definition, Parameters = new() { Departments = ["Reporting bulk acceptance"] } };
			var filtered = await PostAndReadAsync<HrReportingPreviewDto>("/api/hr/reports/designer-preview", input);
			filtered.TotalCount.ShouldBe(1);
			filtered.Rows.Single()["employeeCode"]!.ToString().ShouldBe("REPORT-BULK-25001");
			definition.Filters.Conditions.Clear(); definition.Layout = "summary"; definition.Groups = ["department"];
			definition.Summaries = [new() { Field = "employeeCode", Function = "distinct" }];
			var summary = await PostAndReadAsync<HrReportingPreviewDto>("/api/hr/reports/designer-preview", input);
			summary.Totals["rowCount"].ShouldBe(25001m); summary.Totals["distinct_employeeCode"].ShouldBe(25001m);
			var expected = await GetAndReadAsync<EmployeeListResponse>("/api/employees?page=1&pageSize=1");
			var export = await PostAndReadAsync<HrTableExportStatus>("/api/hr/table-exports", new HrTableExportRequest {
				Table = "employee-table", Query = JsonSerializer.SerializeToElement(new { }),
				Columns = [new() { Id = "fullName", Label = "Staff" }, new() { Id = "doi", Label = "Joining date" }]
			});
			var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
			await services.ServiceProvider.GetRequiredService<HrTableExportJob>().ExecuteAsync(new() { RunId = export.Id, TenantId = tenantId, AttemptId = await AttemptAsync(export.Id) });
			var completed = await GetAndReadAsync<HrTableExportStatus>("/api/hr/table-exports/" + export.Id);
			completed.Status.ShouldBe("Completed", completed.Error); completed.RowCount.ShouldBe(expected.TotalCount); completed.RowCount.ShouldBeGreaterThan(25000);
			var bytes = await Client.GetByteArrayAsync("/api/hr/table-exports/" + export.Id + "/download");
			using (var workbook = new XLWorkbook(new MemoryStream(bytes)))
			{
				workbook.Worksheet("Report").LastRowUsed()!.RowNumber().ShouldBe(expected.TotalCount + 5);
				workbook.Worksheet("Report").Column(2).CellsUsed().ShouldContain(x => x.DataType == XLDataType.DateTime);
			}
			using (var change = uows.Begin(requiresNew: true, isTransactional: true))
			{
				var db = await services.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"AppEmployees\" SET \"FirstName\" = 'Changed after export' WHERE \"TenantId\" = {tenantId} AND \"Department\" = 'Reporting bulk acceptance'");
				await change.CompleteAsync();
			}
			(await Client.GetByteArrayAsync("/api/hr/table-exports/" + export.Id + "/download")).ShouldBe(bytes);
		}
		finally
		{
			using var cleanup = uows.Begin(requiresNew: true, isTransactional: true);
			var db = await services.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			await db.Set<Employee>().Where(x => x.Department == "Reporting bulk acceptance" && x.EmployeeCode.StartsWith("REPORT-BULK-")).ExecuteDeleteAsync();
			await cleanup.CompleteAsync();
		}
	}
	[HrReportingPostgresFact]
	public async Task Payroll_Registers_Should_Reconcile_With_Issued_Evidence_And_Label_Saved_Drafts()
	{
		await PrepareIdentityAsync();
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		var workflow = new EsiPayrollWorkflowScenario(Factory);
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		await workflow.ConfigureAsync(true);
		var issued = await EmployeeTestHelper.CreateEmployeeAsync(workflow.Api, "ReportingIssued", dateOfJoining: month);
		await workflow.EnrollAsync(issued.Id, "1234567890");
		await workflow.SaveCompensationAsync(issued.Id, month, "GROSS", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(issued.Id));
		await workflow.SeedAttendanceAsync(issued.Id, month);
		var calculated = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(calculated.Id, issued.Id, month);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(calculated.Id, issued.Id, month));
		var documents = (await workflow.Api.GetFromJsonAsync<List<PayslipDocumentDto>>($"/api/payroll/payslip-documents?employeeId={issued.Id}&monthYear={month:yyyy-MM}", JsonOptions))!;
		documents.Count.ShouldBe(1);
		var originalPdf = await workflow.Api.GetByteArrayAsync($"/api/payroll/payslip-documents/{documents[0].Id}/pdf");
		var draft = await EmployeeTestHelper.CreateEmployeeAsync(workflow.Api, "ReportingDraft", dateOfJoining: month);
		await workflow.EnrollAsync(draft.Id, "2345678901");
		await workflow.SaveCompensationAsync(draft.Id, month, "GROSS", 8000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(draft.Id));
		await workflow.SeedAttendanceAsync(draft.Id, month);
		await workflow.CalculateAsync(month);
		var definition = HrReportingCatalogue.BuiltIns().Single(x => x.Id == "builtin:salary-register").Definition;
		var input = new HrReportDesignerPreviewDto { Definition = definition, Parameters = new() { Period = "month", Month = month.ToString("yyyy-MM"), EmployeeIds = [issued.Id, draft.Id] } };
		var finalized = await PostAndReadAsync<HrReportingPreviewDto>("/api/hr/reports/designer-preview", input);
		finalized.Rows.Count.ShouldBe(1); finalized.Totals["totalEarnings"].ShouldBe(10000m); finalized.Totals["netPay"].ShouldBe(9925m);
		input.Parameters.IncludeDrafts = true;
		var included = await PostAndReadAsync<HrReportingPreviewDto>("/api/hr/reports/designer-preview", input);
		included.Rows.Count.ShouldBe(2); included.Rows.ShouldContain(x => x["status"]!.ToString() == "Draft"); included.Totals["netPay"].ShouldBe(17865m);
		input.Definition = HrReportingCatalogue.BuiltIns().Single(x => x.Id == "builtin:salary-components").Definition;
		var components = await PostAndReadAsync<HrReportingPreviewDto>("/api/hr/reports/designer-preview", input);
		components.Columns.ShouldContain(x => x.Id.StartsWith("Earning:") && x.Type == "money");
		components.Columns.ShouldContain(x => x.Id.StartsWith("Deduction:") && x.Type == "money");
		(await workflow.Api.GetByteArrayAsync($"/api/payroll/payslip-documents/{documents[0].Id}/pdf")).ShouldBe(originalPdf);
	}
	private async Task PrepareIdentityAsync()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var services = Factory.Services.CreateScope();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId); var userId = Guid.Parse(TestAuthHandler.TestUserId);
		using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var users = services.ServiceProvider.GetRequiredService<IRepository<IdentityUser, Guid>>();
		if (await users.FindAsync(userId) == null) await users.InsertAsync(new IdentityUser(userId, "reporting-test", "reporting-test@example.invalid", tenantId));
		await uow.CompleteAsync();
	}
	[HrReportingPostgresFact]
	public async Task Materialization_Should_Be_Stable_After_Source_Changes_And_Page_Across_Jsonb_Chunks()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var userId = Guid.Parse(TestAuthHandler.TestUserId); var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (var services = Factory.Services.CreateScope())
		{
			using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
			using var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var users = services.ServiceProvider.GetRequiredService<IRepository<IdentityUser, Guid>>();
			if (await users.FindAsync(userId) == null) await users.InsertAsync(new IdentityUser(userId, "reporting-test", "reporting-test@example.invalid", tenantId));
			await uow.CompleteAsync();
		}
		var creation = await Client.PostAsJsonAsync(Root, new HrReportCreateRunDto { TemplateId = "builtin:staff-directory", Revision = 1 }, JsonOptions);
		creation.StatusCode.ShouldBe(HttpStatusCode.OK);
		var run = (await creation.Content.ReadFromJsonAsync<HrReportRunDto>(JsonOptions))!;
		using (var services = Factory.Services.CreateScope())
			await services.ServiceProvider.GetRequiredService<HrReportingRunJob>().ExecuteAsync(new() { RunId = run.Id, TenantId = tenantId, AttemptId = await AttemptAsync(run.Id) });
		var completed = (await Client.GetFromJsonAsync<HrReportRunDto>(Root + "/" + run.Id, JsonOptions))!;
		completed.Status.ShouldBe("Completed", completed.ErrorMessage); completed.RowCount.ShouldBeGreaterThan(0);
		var before = await Client.GetStringAsync(Root + "/" + run.Id + "/rows");
		var files = new Dictionary<string, byte[]>();
		foreach (var format in new[] { "excel", "pdf" })
		{
			(await Client.PostAsync(Root + "/" + run.Id + "/exports?format=" + format, null)).EnsureSuccessStatusCode();
			using var services = Factory.Services.CreateScope();
			await services.ServiceProvider.GetRequiredService<HrReportingRenderJob>().ExecuteAsync(new() { RunId = run.Id, TenantId = tenantId, Format = format });
			files[format] = await Client.GetByteArrayAsync(Root + "/" + run.Id + "/download?format=" + format);
		}
		Guid capturedId;
		Guid chunkedRunId;
		using (var services = Factory.Services.CreateScope())
		{
			using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
			using var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var stored = await services.ServiceProvider.GetRequiredService<IRepository<HrReportRun, Guid>>().GetAsync(run.Id);
			capturedId = JsonSerializer.Deserialize<HrReportingResult>(stored.ResultJson, HrReportingEngine.Json)!.EmployeeIds.First();
			var db = await services.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			var employee = await db.Set<Employee>().SingleAsync(x => x.Id == capturedId);
			db.Entry(employee).Property(x => x.FirstName).CurrentValue = "Changed after capture";
			var captured = JsonSerializer.Deserialize<HrReportingResult>(stored.ResultJson, HrReportingEngine.Json)!;
			var chunked = new HrReportRun(Guid.NewGuid(), tenantId, userId, "Chunk pagination", stored.TemplateId, stored.Revision, stored.DataSource, stored.DefinitionJson, stored.ParametersJson, stored.ScopeJson, DateTimeOffset.UtcNow);
			chunked.Start(chunked.AttemptId, DateTimeOffset.UtcNow);
			chunked.Complete(chunked.AttemptId, 250, JsonSerializer.Serialize(captured, HrReportingEngine.Json));
			await services.ServiceProvider.GetRequiredService<IRepository<HrReportRun, Guid>>().InsertAsync(chunked, true);
			var pageRepository = services.ServiceProvider.GetRequiredService<IRepository<HrReportRunPage, Guid>>();
			for (var page = 0; page < 2; page++)
			{
				var values = Enumerable.Range(page * 200, page == 0 ? 200 : 50).Select(i => new Dictionary<string, object?> { ["employeeCode"] = i.ToString() }).ToList();
				await pageRepository.InsertAsync(new(Guid.NewGuid(), tenantId, chunked.Id, page, JsonSerializer.Serialize(values, HrReportingEngine.Json), chunked.ExpiresAt));
			}
			chunkedRunId = chunked.Id;
			await uow.CompleteAsync();
		}
		(await Client.GetStringAsync(Root + "/" + run.Id + "/rows")).ShouldBe(before);
		foreach (var (format, bytes) in files) (await Client.GetByteArrayAsync(Root + "/" + run.Id + "/download?format=" + format)).ShouldBe(bytes);
		// Duplicate deliveries must preserve the already published bytes, including concurrent formats.
		await Task.WhenAll(RenderAgainAsync(run.Id, tenantId, "excel"), RenderAgainAsync(run.Id, tenantId, "excel"), RenderAgainAsync(run.Id, tenantId, "pdf"));
		foreach (var (format, bytes) in files) (await Client.GetByteArrayAsync(Root + "/" + run.Id + "/download?format=" + format)).ShouldBe(bytes);
		var crossing = (await Client.GetFromJsonAsync<HrReportResultPageDto>(Root + "/" + chunkedRunId + "/rows?page=2&pageSize=125", JsonOptions))!;
		crossing.Rows.Count.ShouldBe(125); crossing.TotalCount.ShouldBe(250);
		crossing.Rows.First()["employeeCode"]!.ToString().ShouldBe("125"); crossing.Rows.Last()["employeeCode"]!.ToString().ShouldBe("249");
		var criteria = new HrReportTableQueryDto {
			Filters = [new() { Column = "employeeCode", Type = "startsWith", Value = JsonSerializer.SerializeToElement("2") }],
			Sorts = [new() { Column = "employeeCode", Direction = "desc" }] };

		var expected = Enumerable.Range(0, 250).Select(i => i.ToString()).Where(code => code.StartsWith("2", StringComparison.Ordinal)).OrderByDescending(code => code, StringComparer.OrdinalIgnoreCase).ToList();
		var filtered = await PostAndReadAsync<HrReportResultPageDto>(Root + "/" + chunkedRunId + "/rows/query", new HrReportTablePageQueryDto { Page = 2, PageSize = 25, Criteria = criteria });
		filtered.TotalCount.ShouldBe(expected.Count);
		filtered.Rows.Select(row => row["employeeCode"]!.ToString()).ShouldBe(expected.Skip(25).Take(25));
		var exportRequest = new HrTableExportRequest { Table = "hr-generated-report", Query = JsonSerializer.SerializeToElement(new { runId = chunkedRunId, criteria }, JsonOptions), Columns = [new() { Id = "employeeCode", Label = "Code" }] };
		var export = await PostAndReadAsync<HrTableExportStatus>("/api/hr/table-exports", exportRequest);
		using (var services = Factory.Services.CreateScope())
			await services.ServiceProvider.GetRequiredService<HrTableExportJob>().ExecuteAsync(new() { RunId = export.Id, TenantId = tenantId, AttemptId = await AttemptAsync(export.Id) });
		var filteredBytes = await Client.GetByteArrayAsync("/api/hr/table-exports/" + export.Id + "/download");
		using (var workbook = new XLWorkbook(new MemoryStream(filteredBytes)))
		{
			var sheet = workbook.Worksheets.First();
			var header = sheet.RowsUsed().Single(row => row.Cell(1).GetString() == "Code");
			var exported = sheet.RowsUsed().Where(row => row.RowNumber() > header.RowNumber()).Select(row => row.Cell(1).GetString()).Where(expected.Contains).ToList();
			exported.ShouldBe(expected);
		}
		using (var denied = Factory.CreateClient())
		{
			denied.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
			(await denied.PostAsJsonAsync(Root + "/" + chunkedRunId + "/rows/query", new HrReportTablePageQueryDto { Criteria = criteria }, JsonOptions)).IsSuccessStatusCode.ShouldBeFalse();
		}
		// A different tenant must not retrieve a captured run, even with the same action permissions.
		using var other = Factory.CreateClient(); other.DefaultRequestHeaders.Add("X-Test-TenantId", Guid.NewGuid().ToString());
		(await other.GetAsync(Root + "/" + run.Id)).IsSuccessStatusCode.ShouldBeFalse();
	}
	private async Task RenderAgainAsync(Guid runId, Guid tenantId, string format)
	{
		using var services = Factory.Services.CreateScope();
		await services.ServiceProvider.GetRequiredService<HrReportingRenderJob>().ExecuteAsync(new() { RunId = runId, TenantId = tenantId, Format = format });
	}
	private async Task<Guid> AttemptAsync(Guid id)
	{
		using var services = Factory.Services.CreateScope();
		using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var run = await services.ServiceProvider.GetRequiredService<IRepository<HrReportRun, Guid>>().GetAsync(id);
		await uow.CompleteAsync(); return run.AttemptId;
	}
}
