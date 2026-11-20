using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Accounts.Reconciliation;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Trait("Database", "PostgreSQL")]
public class PayrollApprovalPostgres_Tests(PayrollApprovalPostgresTestFactory factory)
	: ShikshaMitraApiTestBase(factory), IClassFixture<PayrollApprovalPostgresTestFactory>
{
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);

	[PayrollApprovalPostgresFact]
	public async Task Approval_issues_the_exact_batch_replays_success_rolls_back_failures_and_revokes_review_sheets()
	{
		const string month = "2027-01";
		const string settingsUrl = "/api/payroll/run/approval-settings";
		const string submissionsUrl = "/api/payroll/run/submissions";
		const string exportsUrl = "/api/hr/table-exports";
		var hrId = Guid.Parse(TestAuthHandler.TestUserId);
		var approverId = Guid.NewGuid();
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await WithDatabaseAsync(async db =>
		{
			if (!await db.Set<IdentityUser>().AnyAsync(x => x.Id == hrId))
				db.Add(new IdentityUser(hrId, "payroll-hr-fixture", "payroll-hr-fixture@example.invalid", TenantId));
			db.Add(new IdentityUser(approverId, "payroll-approver-fixture", "payroll-approver-fixture@example.invalid", TenantId));
			db.Add(new UserScopeAssignment(Guid.NewGuid(), TenantId, userId: approverId));
			var school = await db.Set<Tenant>().SingleAsync(x => x.Id == TenantId);
			school.SetProperty("InstituteName", "Test school for payroll approval");
			school.SetProperty("Address1", "Synthetic testing address");
			var payslip = await db.Set<PayslipSettings>().SingleOrDefaultAsync(x => x.Id == TenantId);
			if (payslip == null)
			{
				payslip = new PayslipSettings(TenantId);
				db.Add(payslip);
			}
			payslip.Configure("Test school for payroll approval", "Synthetic testing address", true, Guid.NewGuid());
		});
		using var approver = Factory.CreateClient();
		approver.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, approverId.ToString());
		using var configure = await Client.PutAsJsonAsync(settingsUrl, new ConfigurePayrollApprovalCommand
		{ ApproverId = approverId, Reason = "Synthetic designated approver" });
		configure.StatusCode.ShouldBe(HttpStatusCode.OK, await configure.Content.ReadAsStringAsync());
		var settings = await PostAsync<PayrollApprovalSettingsDto>(Client, $"{settingsUrl}/activate",
			new PayrollApprovalSettingsCommand { ExpectedVersion = 1, Reason = "Synthetic approved rollout" });
		settings.Mode.ShouldBe(PayrollApprovalSettings.Active);
		var run = await PostAsync<PayrollPreviewStatusDto>(Client, "/api/payroll/run/previews", new StartPayrollPreviewRequest
		{ Month = month, CommandKey = Guid.NewGuid() });
		var ids = new List<Guid>();
		await WithDatabaseAsync(async db =>
		{
			var people = await db.Set<Employee>().Where(x => x.DateOfJoining < new DateOnly(2027, 1, 1)
				&& x.EmployeeStatusType == ShikshaMitra.Common.EmployeeStatusType.Active)
				.OrderBy(x => x.Id).Take(5).ToListAsync();
			people.Count.ShouldBe(5);
			ids = people.Select(x => x.Id).ToList();
			var stored = await db.PayrollPreviewRuns.SingleAsync(x => x.Id == run.Id);
			var revision = await db.Set<PayrollInputRevision>().Where(x => x.Id == TenantId).Select(x => x.Revision).SingleAsync();
			stored.Start(revision, DateTime.UtcNow);
			foreach (var person in people)
			{
				var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), TenantId, month, person.Id)
				{
					BasicSalary = 10000m, GrossSalary = 10000m, FullGrossSalary = 10000m, NetPay = 9900m, TotalDeduction = 100m,
					BasisDays = 31, PayableUnits = 31, PresentDays = 31, PayableDays = 31, TotalWorkingDays = 31, ProrationFactor = 1m,
					EmployeeName = person.EmployeeName, EmployeeIdentifier = person.EmployeeCode ?? "", DayCalculationMode = "total-month-days",
					EarningComponentsJson = JsonSerializer.Serialize(new[] { new RunPayrollComponentItem { Name = "Basic", Amount = 10000, FullMonthAmount = 10000m } }),
					DeductionComponentsJson = JsonSerializer.Serialize(new[] { new RunPayrollComponentItem { Name = "Synthetic deduction", Amount = 100 } })
				};
				db.Add(snapshot);
				var item = PayrollRunProjection.ToItem(snapshot);
				db.Add(new PayrollPreviewEmployee(Guid.NewGuid(), TenantId, run.Id, person.Id, item.EmployeeName, item.EmployeeIdentifier, JsonSerializer.Serialize(item)));
			}
			stored.Complete(JsonSerializer.Serialize(new RunPayrollReadinessSummary { Month = month }), DateTime.UtcNow);
		});
		await PostAsync<PayrollLockResponse>(Client, "/api/payroll/run/reviews",
			new PayrollRunReviewRequest { Month = month, PreviewRunId = run.Id, EmployeeIds = ids });
		using var legacy = await Client.PostAsJsonAsync("/api/payroll/run", new RunPayrollRequest
		{ Month = month, PreviewRunId = run.Id, EmployeeIds = [ids[0]] });
		legacy.IsSuccessStatusCode.ShouldBeFalse();
		(await legacy.Content.ReadAsStringAsync()).ShouldContain("PAYROLL_SUBMISSION_REQUIRED");

		var beforeSubmit = await ExportAsync(Client, "excel", new { month, previewRunId = run.Id, employeeIds = new[] { ids[0] } });
		using (var xlsx = new XLWorkbook(new MemoryStream(beforeSubmit.Bytes)))
		{
			xlsx.Worksheet("Report").Cell(2, 1).GetString().ShouldContain("Pending finalization");
			xlsx.Worksheet("Report").Cell(6, 6).GetValue<decimal>().ShouldBe(9900m);
		}

		var first = await SubmitAsync(ids[0]);
		using (var denied = await Client.PostAsJsonAsync($"{submissionsUrl}/{first.Id}/finalize", FinalizeCommand(first)))
			denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await denied.Content.ReadAsStringAsync());
		(await Client.GetAsync($"{exportsUrl}/{beforeSubmit.Id}/download")).IsSuccessStatusCode.ShouldBeFalse();
		var approverSheet = await ExportAsync(approver, "pdf", new { submissionId = first.Id, version = first.Version });
		Encoding.ASCII.GetString(approverSheet.Bytes, 0, 5).ShouldBe("%PDF-");
		var pdfText = ReceiptPdfTextReader.Read(approverSheet.Bytes);
		pdfText.ShouldContain("Pending finalization");
		pdfText.ShouldContain("9,900.00");
		var firstConfirm = FinalizeCommand(first);
		var firstFinal = await PostAsync<PayrollSubmissionDto>(approver, $"{submissionsUrl}/{first.Id}/finalize", firstConfirm);
		firstFinal.State.ShouldBe(PayrollSubmission.Finalized);
		var replay = await PostAsync<PayrollSubmissionDto>(approver, $"{submissionsUrl}/{first.Id}/finalize", firstConfirm);
		replay.Version.ShouldBe(firstFinal.Version);
		(await approver.GetAsync($"{exportsUrl}/{approverSheet.Id}/download")).IsSuccessStatusCode.ShouldBeFalse();

		var second = await SubmitAsync(ids[1], ids[2]);
		var beforeFailure = await CountsAsync();
		factory.Barrier.FailNextCommand("INSERT INTO \"HR_PayslipDocuments\"");
		var secondCommand = FinalizeCommand(second);
		using (var failed = await approver.PostAsJsonAsync($"{submissionsUrl}/{second.Id}/finalize", secondCommand))
			failed.IsSuccessStatusCode.ShouldBeFalse();
		(await CountsAsync()).ShouldBe(beforeFailure);
		var pending = await approver.GetFromJsonAsync<PayrollSubmissionDetailDto>($"{submissionsUrl}/{second.Id}", JsonOptions);
		pending!.Submission.State.ShouldBe(PayrollSubmission.Pending);
		var secondFinal = await PostAsync<PayrollSubmissionDto>(approver, $"{submissionsUrl}/{second.Id}/finalize", secondCommand);
		secondFinal.State.ShouldBe(PayrollSubmission.Finalized);
		secondFinal.NetPay.ShouldBe(19800m);
		(await CountsAsync()).Documents.ShouldBe(3);

		var abandonedSheet = await ExportAsync(Client, "excel", new { month, previewRunId = run.Id, employeeIds = new[] { ids[3] } });
		var withdrawn = await SubmitAsync(ids[3]);
		await PostAsync<PayrollSubmissionDto>(Client, $"{submissionsUrl}/{withdrawn.Id}/withdraw",
			new ResolvePayrollSubmissionCommand { CommandKey = Guid.NewGuid(), ExpectedVersion = withdrawn.Version, Reason = "Synthetic replacement" });
		(await Client.GetAsync($"{exportsUrl}/{abandonedSheet.Id}/download")).IsSuccessStatusCode.ShouldBeFalse();
		var changed = await SubmitAsync(ids[3], ids[4]);
		await WithDatabaseAsync(async db =>
		{
			var snapshot = await db.Set<PayrollRunItemSnapshot>().SingleAsync(x => x.MonthYear == month && x.EmployeeId == ids[3]);
			var lines = JsonSerializer.Deserialize<List<RunPayrollComponentItem>>(snapshot.DeductionComponentsJson)!;
			lines[0].Name = "Changed deduction with the same net pay";
			snapshot.DeductionComponentsJson = JsonSerializer.Serialize(lines);
		});
		var returned = await PostAsync<PayrollSubmissionDto>(approver, $"{submissionsUrl}/{changed.Id}/finalize", FinalizeCommand(changed));
		returned.State.ShouldBe(PayrollSubmission.Returned);
		returned.ReasonCode.ShouldBe("EvidenceChanged");
		var register = await GetAndReadAsync<RunPayrollResponse>($"/api/payroll/run/register?month={month}");
		register.Readiness.LockedCount.ShouldBe(3);
		register.IsGenerated.ShouldBeFalse();
		register.Items.Single(x => x.EmployeeId == ids[3]).ReviewStatus.ShouldBe(ReviewStatuses.Pending);
		register.Items.Single(x => x.EmployeeId == ids[4]).ReviewStatus.ShouldBe(ReviewStatuses.Pending);

		Task<PayrollSubmissionDto> SubmitAsync(params Guid[] employees) => PostAsync<PayrollSubmissionDto>(Client,
			submissionsUrl, new SubmitPayrollCommand
			{
				Month = month, PreviewRunId = run.Id, EmployeeIds = employees.ToList(), CommandKey = Guid.NewGuid(),
				ExpectedSettingsVersion = settings.Version
			});
		static ResolvePayrollSubmissionCommand FinalizeCommand(PayrollSubmissionDto batch) =>
			new() { ExpectedVersion = batch.Version, CommandKey = Guid.NewGuid() };

		async Task<(Guid Id, byte[] Bytes)> ExportAsync(HttpClient actor, string format, object selection)
		{
			var request = new HrTableExportRequest
			{
				Table = "payroll-review-sheet", Format = format,
				Query = JsonSerializer.SerializeToElement(selection),
				Columns = [new() { Id = "employeeName", Label = "Staff" }, new() { Id = "netSalary", Label = "Net pay" },
					new() { Id = "deductionBreakdown", Label = "Deductions" }]
			};
			var job = await PostAsync<HrTableExportStatus>(actor, exportsUrl, request);
			var attempt = Guid.Empty;
			await WithDatabaseAsync(async db =>
				attempt = await db.Set<HrReportRun>().Where(x => x.Id == job.Id).Select(x => x.AttemptId).SingleAsync());
			using var scope = Factory.Services.CreateScope();
			await scope.ServiceProvider.GetRequiredService<HrTableExportJob>().ExecuteAsync(new HrTableExportJobArgs
			{ RunId = job.Id, TenantId = TenantId, AttemptId = attempt });
			using var file = await actor.GetAsync($"{exportsUrl}/{job.Id}/download");
			file.StatusCode.ShouldBe(HttpStatusCode.OK, file.IsSuccessStatusCode ? null : await file.Content.ReadAsStringAsync());
			return (job.Id, await file.Content.ReadAsByteArrayAsync());
		}

		async Task<(int Documents, int Slips, int Delivery, int Jobs)> CountsAsync()
		{
			var result = (0, 0, 0, 0);
			await WithDatabaseAsync(async db =>
			{
				result = (await db.Set<PayslipDocument>().CountAsync(x => x.MonthYear == month),
					await db.Set<SalarySlip>().CountAsync(x => x.MonthYear == month),
					await db.Set<PayslipDelivery>().CountAsync(),
					await db.Set<BackgroundJobRecord>().CountAsync());
			});
			return result;
		}
	}

	private async Task<T> PostAsync<T>(HttpClient client, string url, object command)
	{
		using var response = await client.PostAsJsonAsync(url, command, JsonOptions);
		response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
	}

	private async Task WithDatabaseAsync(Func<ShikshaMitraDbContext, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync());
		await unit.CompleteAsync();
	}
}

public sealed class PayrollApprovalPostgresTestFactory : PayrollPostgresTestFactory
{
	internal const string ConnectionKey = "PAYROLL_APPROVAL_TEST_POSTGRES";
	protected override string ConnectionVariable => ConnectionKey;
}

public sealed class PayrollApprovalPostgresFactAttribute : FactAttribute
{
	public PayrollApprovalPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PayrollApprovalPostgresTestFactory.ConnectionKey)))
			Skip = "Set PAYROLL_APPROVAL_TEST_POSTGRES to an empty, disposable loopback payroll_tests_* database.";
	}
}
