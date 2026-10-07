using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayslipCorrectionPostgresFactAttribute : FactAttribute
{
	public PayslipCorrectionPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_CORRECTION_TEST_POSTGRES")))
			Skip = "Set PAYROLL_CORRECTION_TEST_POSTGRES to a fresh disposable loopback payroll_tests_* database.";
	}
}

public class PayslipCorrectionPostgresTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_CORRECTION_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipCorrectionApi_Tests
{
	[PayslipCorrectionPostgresFact]
	public async Task Unpaid_correction_is_reviewed_reissued_and_idempotent_without_changing_original_artifact()
	{
		using var factory = new PayslipCorrectionPostgresTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var client = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		var now = DateTime.UtcNow;
		var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(client, "PayslipCorrection", dateOfJoining: month);
		await workflow.ConfigureAsync(true);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, month, "GROSS", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, month);
		var run = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(run.Id, employee.Id, month);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(run.Id, employee.Id, month));
		var url = $"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={month:yyyy-MM}";
		var original = (await client.GetFromJsonAsync<List<PayslipDocumentDto>>(url, json))!.Single();
		Guid? laterRunId = null;
		var laterMonth = month.AddMonths(1);
		if (month.Month is not (3 or 9))
		{
			await workflow.SeedAttendanceAsync(employee.Id, laterMonth);
			var later = await workflow.CalculateAsync(laterMonth);
			laterRunId = later.Id;
			await workflow.ReviewAsync(later.Id, employee.Id, laterMonth);
		}
		var start = await client.PostAsJsonAsync("/api/payroll/payslip-corrections", new StartPayslipCorrectionDto {
			DocumentId = original.Id, IdempotencyKey = Guid.NewGuid(), Reason = "Correct one synthetic unpaid absence" }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(start);
		var draft = (await start.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		draft.Attendance.Count.ShouldBe(DateTime.DaysInMonth(month.Year, month.Month));
		draft.OriginalEarnings.ShouldBe(10000m);
		draft.OriginalNetPay.ShouldBe(9925m);
		var forbidden = factory.CreateClient();
		forbidden.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
		(await forbidden.GetAsync($"/api/payroll/payslip-corrections?documentId={original.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		foreach (var path in new[] { "calculate", "review", "cancel", "apply" })
			(await forbidden.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/{path}",
				new CalculatePayslipCorrectionDto { ConcurrencyStamp = draft.ConcurrencyStamp }, json)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var stale = await client.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/calculate",
			new CalculatePayslipCorrectionDto { ConcurrencyStamp = "obsolete", Attendance = draft.Attendance }, json);
		stale.IsSuccessStatusCode.ShouldBeFalse();
		var invalidAttendance = await client.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/calculate",
			new CalculatePayslipCorrectionDto { ConcurrencyStamp = draft.ConcurrencyStamp, Attendance = [
				new() { Date = DateOnly.FromDateTime(month.AddMonths(1)), Status = EmployeeAttendanceStatusType.Present }] }, json);
		invalidAttendance.IsSuccessStatusCode.ShouldBeFalse();
		var attendance = Enumerable.Range(1, DateTime.DaysInMonth(month.Year, month.Month))
			.Select(day => new PayslipCorrectionAttendanceDto { Date = new DateOnly(month.Year, month.Month, day),
				Status = EmployeeAttendanceStatusType.Present }).ToList();
		attendance.First(x => x.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Status = EmployeeAttendanceStatusType.FullDayLeave;
		var calculatedResponse = await client.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/calculate",
			new CalculatePayslipCorrectionDto { ConcurrencyStamp = draft.ConcurrencyStamp, Attendance = attendance }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(calculatedResponse);
		var calculated = (await calculatedResponse.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		var reviewedResponse = await client.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/review",
			new PayslipCorrectionActionDto { ConcurrencyStamp = calculated.ConcurrencyStamp }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(reviewedResponse);
		var reviewed = (await reviewedResponse.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		var apply = new PayslipCorrectionActionDto { ConcurrencyStamp = reviewed.ConcurrencyStamp };
		// Fail at the actual PDF INSERT save, after the correction has updated payroll inside its transaction.
		// The same reviewed draft must remain retryable and no replacement or changed wages may escape rollback.
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (scope.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(new[] {
			new Claim(AbpClaimTypes.UserId, TestAuthHandler.TestUserId),
			new Claim(AbpClaimTypes.UserName, TestAuthHandler.TestUserName),
			new Claim(AbpClaimTypes.TenantId, TestAuthHandler.TestTenantId), new Claim(AbpClaimTypes.Role, "admin") }, "Test"))))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var context = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			context.SavingChanges += (_, _) => {
				if (context.ChangeTracker.Entries<PayslipDocument>().Any(x => x.State == EntityState.Added))
					throw new InvalidOperationException("Synthetic correction PDF persistence failure");
			};
			await Should.ThrowAsync<InvalidOperationException>(() => scope.ServiceProvider
				.GetRequiredService<IPayslipCorrectionAppService>().ApplyAsync(draft.Id, apply));
		}
		(await client.GetFromJsonAsync<List<PayslipDocumentDto>>(url, json))!.Count.ShouldBe(1);
		(await client.GetFromJsonAsync<SalarySlipResponse>($"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={month:yyyy-MM}", json))!
			.NetSalary.ShouldBe((double)draft.OriginalNetPay);
		(await client.GetFromJsonAsync<List<PayslipCorrectionDto>>($"/api/payroll/payslip-corrections?documentId={original.Id}", json))!
			.Single().Status.ShouldBe("REVIEWED");
		// Both requests use the tenant fence: payment must not pass a pending reissue.
		var applyTask = client.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/apply", apply, json);
		var paymentTask = client.PostAsJsonAsync("/api/payroll/payslip-documents/payment", new RecordPayslipPaymentDto {
			DocumentId = original.Id, IdempotencyKey = Guid.NewGuid(), PaidAt = DateTimeOffset.UtcNow.AddMinutes(-1), Reference = "Synthetic competing payment" }, json);
		await Task.WhenAll(applyTask, paymentTask);
		(await paymentTask).IsSuccessStatusCode.ShouldBeFalse();
		var appliedResponse = await applyTask;
		await EsiPayrollWorkflowScenario.RequireSuccess(appliedResponse);
		var applied = (await appliedResponse.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		applied.Status.ShouldBe("APPLIED");
		var repeated = await client.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/apply", apply, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(repeated);
		(await repeated.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!.ResultDocumentId.ShouldBe(applied.ResultDocumentId);
		var versions = (await client.GetFromJsonAsync<List<PayslipDocumentDto>>(url, json))!;
		versions.Count.ShouldBe(2);
		versions.Single(x => x.IsCurrent).Id.ShouldBe(applied.ResultDocumentId!.Value);
		versions.Single(x => x.IsCurrent).ReplacesDocumentId.ShouldBe(original.Id);
		Convert.ToHexString(SHA256.HashData(await client.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf"))).ShouldBe(original.Sha256);
		var slip = (await client.GetFromJsonAsync<SalarySlipResponse>($"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={month:yyyy-MM}", json))!;
		slip.NetSalary.ShouldBe(calculated.Candidate!.NetPay);
		slip.Status.ShouldBe("Finalized");
		if (laterRunId.HasValue)
		{
			(await workflow.StatusAsync(laterRunId.Value)).Status.ShouldBe("SUPERSEDED");
			(await workflow.StatusAsync(laterRunId.Value)).IsCurrent.ShouldBeFalse();
			(await workflow.LockAsync(laterRunId.Value, employee.Id, laterMonth)).IsSuccessStatusCode.ShouldBeFalse();
			var freshLater = await workflow.CalculateAsync(laterMonth);
			await workflow.ReviewAsync(freshLater.Id, employee.Id, laterMonth);
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(freshLater.Id, employee.Id, laterMonth));
		}
		(await workflow.LockAsync(run.Id, employee.Id, month)).IsSuccessStatusCode.ShouldBeFalse();
		var secondStart = await client.PostAsJsonAsync("/api/payroll/payslip-corrections", new StartPayslipCorrectionDto {
			DocumentId = applied.ResultDocumentId!.Value, IdempotencyKey = Guid.NewGuid(), Reason = "Synthetic cancelled follow-up" }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(secondStart);
		var second = (await secondStart.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		second.Attendance.Single(x => x.Date == attendance.First(x => x.Status == EmployeeAttendanceStatusType.FullDayLeave).Date)
			.Status.ShouldBe(EmployeeAttendanceStatusType.FullDayLeave);
		var cancel = new PayslipCorrectionActionDto { ConcurrencyStamp = second.ConcurrencyStamp };
		await EsiPayrollWorkflowScenario.RequireSuccess(await client.PostAsJsonAsync($"/api/payroll/payslip-corrections/{second.Id}/cancel", cancel, json));
		await EsiPayrollWorkflowScenario.RequireSuccess(await client.PostAsJsonAsync($"/api/payroll/payslip-corrections/{second.Id}/cancel", cancel, json));
		var history = (await client.GetFromJsonAsync<List<PayslipCorrectionDto>>($"/api/payroll/payslip-corrections?documentId={original.Id}", json))!;
		history.Count.ShouldBe(2);
		history.Single(x => x.Id == second.Id).Status.ShouldBe("CANCELLED");
		(await client.GetFromJsonAsync<List<PayslipDocumentDto>>(url, json))!.Count.ShouldBe(2);
	}
}
