using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.BlobStoring;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Reports;

[Collection(ApiTestCollection.Name), TestModule(TestTraits.Modules.HR)]
public class HrReportingHistoryPerformanceApi_Tests(HrOperationalTableApiTestFactory factory)
	: ShikshaMitraApiTestBase(factory), IClassFixture<HrOperationalTableApiTestFactory>
{
	private const string Root = "/api/hr/reports";
	private static HrReportDesignDto Definition() => new() { DataSource = "employee", Columns = [new() { Field = "employeeCode" }, new() { Field = "employeeName" }], Parameters = [] };
	[Theory]
	[InlineData(HrReportDataSources.PayrollRegister)]
	[InlineData(HrReportDataSources.PayrollStatement)]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Salary_History_Should_Keep_Issued_Boundary_And_Batched_Authorization(string source)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var ownerId = Guid.Parse(TestAuthHandler.TestUserId);
		var prefix = "Issued history " + Guid.NewGuid().ToString("N")[..8];
		using var granted = Factory.CreateClient();
		granted.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(',',
			ShikshaMitraSubmodulePermissions.HR.Reports.View, ShikshaMitraSubmodulePermissions.HR.Reports.Export,
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View));
		// Exercise the actual merged queue path, rather than setting every marker in the fixture.
		var queuedResponse = await granted.PostAsJsonAsync(Root + "/runs", new HrReportCreateRunDto {
			TemplateId = source == HrReportDataSources.PayrollRegister ? "builtin:salary-register" : "builtin:salary-statement", Revision = 1,
			// Queue validation requires one selected ID for statements. Worker execution and
			// employee/payroll generation belong to the separate issuance acceptance fixtures.
			Parameters = new() { Period = "month", Month = "2026-10", EmployeeIds = source == HrReportDataSources.PayrollStatement ? [Guid.NewGuid()] : [] } }, JsonOptions);
		queuedResponse.EnsureSuccessStatusCode();
		var queued = (await queuedResponse.Content.ReadFromJsonAsync<HrReportRunDto>(JsonOptions))!;
		var ids = new List<Guid>();
		byte[] issuedBytes = [1, 2, 3];
		using (var services = Factory.Services.CreateScope())
		using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var repository = services.ServiceProvider.GetRequiredService<IRepository<HrReportRun, Guid>>();
			var queuedRun = await repository.GetAsync(queued.Id);
			queuedRun.DataSource.ShouldBe(source);
			PayrollIssuedReportBoundary.IsIssuedOnly(queuedRun).ShouldBeTrue();
			JsonSerializer.Deserialize<HrReportParametersDto>(queuedRun.ParametersJson, HrReportingEngine.Json)!.IncludeDrafts.ShouldBeFalse();
			for (var index = 0; index < 5; index++)
			{
				var time = DateTimeOffset.UtcNow.AddHours(-2).AddMinutes(index);
				var definition = HrReportingCatalogue.BuiltIns().Single(x => x.Id == (source == HrReportDataSources.PayrollRegister ? "builtin:salary-register" : "builtin:salary-statement")).Definition;
				var row = new Dictionary<string, object?> { ["status"] = index == 2 ? "Draft" : "Issued", ["netPay"] = 42m };
				var run = new HrReportRun(Guid.NewGuid(), tenantId, ownerId, prefix + " " + index,
					"test:issued-history", 1, source, JsonSerializer.Serialize(definition, HrReportingEngine.Json),
					JsonSerializer.Serialize(new HrReportParametersDto { IncludeDrafts = index is 0 or 1 }),
					JsonSerializer.Serialize(new HrReportExportScopeSnapshot { TenantId = tenantId, HasTenantWideAccess = true }), time)
				{ CreationTime = time.UtcDateTime };
				// 0 models an issued-only server capture; 1 is an unsafe legacy draft capture;
				// 2 has unsafe retained metadata despite its marker; 4 is a safe legacy issued capture.
				if (index is 0 or 2 or 3) run.ExtraProperties["issuedSalaryDocumentsOnly"] = true;
				run.Start(run.AttemptId, time);
				run.Complete(run.AttemptId, 1, JsonSerializer.Serialize(new HrReportingResult {
					Columns = [new() { Id = "status", Type = "text" }, new() { Id = "netPay", Type = "money" }], Rows = [row] }, HrReportingEngine.Json));
				run.QueueExport("excel", time); run.AttachExport("excel");
				await repository.InsertAsync(run);
				await services.ServiceProvider.GetRequiredService<IRepository<HrReportRunPage, Guid>>().InsertAsync(new(
					Guid.NewGuid(), tenantId, run.Id, 0, JsonSerializer.Serialize(new[] { row }, HrReportingEngine.Json), run.ExpiresAt));
				await services.ServiceProvider.GetRequiredService<IBlobContainer>().SaveAsync(HrReportingWorkspaceAppService.BlobName(run, "excel"), issuedBytes);
				ids.Add(run.Id);
			}
			await uow.CompleteAsync();
		}
		using (var sql = new ReportRunSelectObserver())
		{
			var input = new HrReportTablePageQueryDto { Take = 2, Criteria = new() { Search = prefix } };
			var response = await granted.PostAsJsonAsync(Root + "/runs/query", input, JsonOptions);
			response.EnsureSuccessStatusCode();
			var page = (await response.Content.ReadFromJsonAsync<HrReportRunHistoryDto>(JsonOptions))!;
			page.Items.Select(x => x.Id).ShouldBe(new[] { ids[4], ids[3] });
			page.HasMore.ShouldBeTrue();
			sql.Selects.Count.ShouldBe(1, string.Join("\n", sql.Selects));
			sql.Selects.Clear(); input.Skip = 2;
			response = await granted.PostAsJsonAsync(Root + "/runs/query", input, JsonOptions);
			response.EnsureSuccessStatusCode();
			page = (await response.Content.ReadFromJsonAsync<HrReportRunHistoryDto>(JsonOptions))!;
			page.Items.Single().Id.ShouldBe(ids[0]); page.HasMore.ShouldBeFalse();
			sql.Selects.Count.ShouldBe(1);
		}
		foreach (var id in new[] { ids[1], ids[2] })
			foreach (var suffix in new[] { "", "/rows", "/download?format=excel" })
			{
				var denied = await granted.GetAsync(Root + "/runs/" + id + suffix);
				denied.IsSuccessStatusCode.ShouldBeFalse();
				(await denied.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.PayslipReportRequiresIssued);
			}
		var rows = await granted.GetFromJsonAsync<HrReportResultPageDto>(Root + "/runs/" + ids[0] + "/rows", JsonOptions);
		rows!.TotalCount.ShouldBe(1); rows.Rows.Single()["status"]!.ToString().ShouldBe("Issued");
		(await granted.GetByteArrayAsync(Root + "/runs/" + ids[0] + "/download?format=excel")).ShouldBe(issuedBytes);
		using var revoked = Factory.CreateClient();
		revoked.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(',',
			ShikshaMitraSubmodulePermissions.HR.Reports.View, ShikshaMitraSubmodulePermissions.HR.Reports.Export));
		var history = await revoked.PostAsJsonAsync(Root + "/runs/query", new HrReportTablePageQueryDto { Take = 10, Criteria = new() { Search = prefix } }, JsonOptions);
		history.EnsureSuccessStatusCode();
		(await history.Content.ReadFromJsonAsync<HrReportRunHistoryDto>(JsonOptions))!.Items.ShouldBeEmpty();
		foreach (var suffix in new[] { "", "/rows", "/download?format=excel" })
			(await revoked.GetAsync(Root + "/runs/" + ids[0] + suffix)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await granted.GetFromJsonAsync<HrReportRunDto>(Root + "/runs/" + queued.Id, JsonOptions))!.Id.ShouldBe(queued.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task History_Should_Authorize_Batched_Runs_Without_Per_Row_Run_Selects()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var prefix = "History budget " + Guid.NewGuid().ToString("N")[..8];
		var ids = new List<Guid>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var ownerId = Guid.Parse(TestAuthHandler.TestUserId);
		var otherTenantId = Guid.NewGuid();
		using (var services = Factory.Services.CreateScope())
		using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var repository = services.ServiceProvider.GetRequiredService<IRepository<HrReportRun, Guid>>();
			for (var index = 0; index < 6; index++)
			{
				var now = DateTimeOffset.UtcNow.AddHours(-2).AddMinutes(index);
				var source = index == 1 ? "payroll_register" : "employee";
				var definition = Definition(); definition.DataSource = source;
				var run = new HrReportRun(Guid.NewGuid(), tenantId, index == 5 ? Guid.NewGuid() : ownerId,
					prefix + " " + index, "test:history-budget", 1, source,
					JsonSerializer.Serialize(definition), JsonSerializer.Serialize(new HrReportParametersDto()),
					JsonSerializer.Serialize(new HrReportExportScopeSnapshot { TenantId = tenantId, HasTenantWideAccess = true }), now)
				{ CreationTime = now.UtcDateTime };
				if (index == 2) run.Start(run.AttemptId, now);
				if (index == 3)
				{
					run.Start(run.AttemptId, now);
					run.Complete(run.AttemptId, 1, JsonSerializer.Serialize(new HrReportingResult { EmployeeIds = [Guid.NewGuid()] }));
				}
				ids.Add(run.Id);
				await repository.InsertAsync(run);
			}
			using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null))
			{
				var other = await services.ServiceProvider.GetRequiredService<ITenantManager>().CreateAsync("Other reporting tenant " + prefix);
				typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(other, otherTenantId);
				await services.ServiceProvider.GetRequiredService<IRepository<Tenant, Guid>>().InsertAsync(other);
			}
			await uow.CompleteAsync();
		}
		using var limited = Factory.CreateClient();
		limited.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(',', ShikshaMitraSubmodulePermissions.HR.Reports.View, ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View));
		using var sql = new ReportRunSelectObserver();
		var input = new HrReportTablePageQueryDto { Take = 2, Criteria = new() { Search = prefix } };
		var response = await limited.PostAsJsonAsync(Root + "/runs/query", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		var page = (await response.Content.ReadFromJsonAsync<HrReportRunHistoryDto>(JsonOptions))!;
		page.Items.Select(x => x.Id).ShouldBe(new[] { ids[4], ids[2] });
		// Criteria mapping precedes the existing interrupted-work recovery, as before.
		page.Items.Single(x => x.Id == ids[2]).Status.ShouldBe("Running");
		page.HasMore.ShouldBeTrue();
		// A single batch SELECT; current source and captured-scope checks still run per candidate.
		sql.Selects.Count.ShouldBe(1, string.Join("\n", sql.Selects));
		sql.Selects.Clear();
		input.Skip = 2;
		response = await limited.PostAsJsonAsync(Root + "/runs/query", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		page = (await response.Content.ReadFromJsonAsync<HrReportRunHistoryDto>(JsonOptions))!;
		page.Items.Single().Id.ShouldBe(ids[0]);
		page.HasMore.ShouldBeFalse();
		sql.Selects.Count.ShouldBe(1);
		using var revoked = Factory.CreateClient();
		revoked.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.HR.Reports.View);
		var deniedHistory = await revoked.PostAsJsonAsync(Root + "/runs/query", new HrReportTablePageQueryDto { Take = 10, Criteria = new() { Search = prefix } }, JsonOptions);
		(await deniedHistory.Content.ReadFromJsonAsync<HrReportRunHistoryDto>(JsonOptions))!.Items.ShouldBeEmpty();
		(await limited.GetAsync(Root + "/runs/" + ids[3])).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await limited.GetAsync(Root + "/runs/" + ids[5])).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await limited.GetAsync(Root + "/runs/" + Guid.NewGuid())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await limited.GetFromJsonAsync<HrReportRunDto>(Root + "/runs/" + ids[2], JsonOptions))!.Status.ShouldBe("Failed");
		using var otherTenant = Factory.CreateClient();
		otherTenant.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, otherTenantId.ToString());
		(await otherTenant.GetAsync(Root + "/runs/" + ids[4])).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		var tenantHistory = await otherTenant.PostAsJsonAsync(Root + "/runs/query", input, JsonOptions);
		tenantHistory.EnsureSuccessStatusCode();
		(await tenantHistory.Content.ReadFromJsonAsync<HrReportRunHistoryDto>(JsonOptions))!.Items.ShouldBeEmpty();
	}

	private sealed class ReportRunSelectObserver : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
	{
		private readonly IDisposable _all;
		private readonly List<IDisposable> _subscriptions = [];
		public ConcurrentQueue<string> Selects { get; } = new();
		public ReportRunSelectObserver() => _all = DiagnosticListener.AllListeners.Subscribe(this);
		public void OnNext(DiagnosticListener listener)
		{
			if (listener.Name == "Microsoft.EntityFrameworkCore") _subscriptions.Add(listener.Subscribe(this));
		}
		public void OnNext(KeyValuePair<string, object?> value)
		{
			if (value.Key != RelationalEventId.CommandExecuted.Name || value.Value is not CommandExecutedEventData data) return;
			var sql = data.Command.CommandText;
			if (sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) && sql.Contains("\"HR_ReportRuns\"", StringComparison.Ordinal)) Selects.Enqueue(sql);
		}
		public void OnCompleted() { }
		public void OnError(Exception error) { }
		public void Dispose() { _all.Dispose(); foreach (var subscription in _subscriptions) subscription.Dispose(); }
	}
}
