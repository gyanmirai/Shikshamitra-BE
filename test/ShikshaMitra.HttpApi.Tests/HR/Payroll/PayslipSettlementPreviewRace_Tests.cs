using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipSettlementPreviewFactAttribute : FactAttribute
{
	public PayslipSettlementPreviewFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_SETTLEMENT_PREVIEW_TEST_POSTGRES")))
			Skip = "Set PAYROLL_SETTLEMENT_PREVIEW_TEST_POSTGRES to a fresh disposable loopback payroll_tests_* database.";
	}
}

public sealed class PayslipSettlementPreviewTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_SETTLEMENT_PREVIEW_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipSettlementPreviewRace_Tests
{
	[PayslipSettlementPreviewFact]
	public async Task Settlement_supersedes_an_already_running_target_worker_without_publishing_or_restoring_deleted_snapshots()
	{
		using var factory = new PayslipSettlementPreviewTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		var now = DateTime.UtcNow;
		var sourceMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var targetMonth = sourceMonth.AddMonths(1);
		var targetKey = targetMonth.ToString("yyyy-MM");
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipSettlementWorker", dateOfJoining: sourceMonth);
		await workflow.ConfigureAsync(true);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, sourceMonth, "GROSS", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, sourceMonth);
		var source = await workflow.CalculateAsync(sourceMonth);
		await workflow.ReviewAsync(source.Id, employee.Id, sourceMonth);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(source.Id, employee.Id, sourceMonth));
		var sourceUrl = $"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={sourceMonth:yyyy-MM}";
		var original = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(sourceUrl, json))!.Single();
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PostAsJsonAsync("/api/payroll/payslip-documents/payment",
			new RecordPayslipPaymentDto { DocumentId = original.Id, IdempotencyKey = Guid.NewGuid(),
				PaidAt = DateTimeOffset.UtcNow.AddMinutes(-1), Reference = "Synthetic running-preview race" }, json));
		await workflow.SeedAttendanceAsync(employee.Id, targetMonth);
		var response = await api.PostAsJsonAsync("/api/payroll/payslip-corrections", new StartPayslipCorrectionDto {
			DocumentId = original.Id, IdempotencyKey = Guid.NewGuid(), Reason = "Synthetic settlement during target calculation" }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var draft = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		draft.Attendance.First(x => x.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Status = EmployeeAttendanceStatusType.FullDayLeave;
		response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/calculate",
			new CalculatePayslipCorrectionDto { ConcurrencyStamp = draft.ConcurrencyStamp, Attendance = draft.Attendance }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var calculated = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/review",
			new PayslipCorrectionActionDto { ConcurrencyStamp = calculated.ConcurrencyStamp }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var reviewed = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		var completed = await workflow.CalculateAsync(targetMonth);
		var before = await workflow.EmployeeAsync(completed.Id, employee.Id);
		await workflow.ReviewAsync(completed.Id, employee.Id, targetMonth);
		var running = await workflow.StartAsync(targetMonth);
		factory.Barrier.Arm();
		var worker = workflow.ExecuteAsync(running.Id);
		try
		{
			await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(25));
			(await workflow.StatusAsync(running.Id)).Status.ShouldBe("RUNNING");
			await EsiPayrollWorkflowScenario.RequireSuccess(await api.PostAsJsonAsync(
				$"/api/payroll/payslip-corrections/{draft.Id}/settle", new SettlePayslipCorrectionDto {
					ConcurrencyStamp = reviewed.ConcurrencyStamp, TargetMonth = targetKey, PreviewRunId = completed.Id }, json));
		}
		finally { factory.Barrier.Release(); }
		await worker.WaitAsync(TimeSpan.FromSeconds(45));
		var stopped = await workflow.StatusAsync(running.Id);
		stopped.Status.ShouldBe("SUPERSEDED");
		stopped.IsCurrent.ShouldBeFalse();
		stopped.LastCompletedRunId.ShouldBeNull();
		(await workflow.StatusAsync(completed.Id)).Status.ShouldBe("SUPERSEDED");
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			(await scope.ServiceProvider.GetRequiredService<IRepository<PayrollPreviewEmployee, Guid>>()
				.CountAsync(x => x.RunId == running.Id)).ShouldBe(0);
			(await scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>()
				.CountAsync(x => x.MonthYear == targetKey && x.EmployeeId == employee.Id)).ShouldBe(0);
			await unit.CompleteAsync();
		}
		(await workflow.LockAsync(completed.Id, employee.Id, targetMonth)).IsSuccessStatusCode.ShouldBeFalse();
		(await workflow.LockAsync(running.Id, employee.Id, targetMonth)).IsSuccessStatusCode.ShouldBeFalse();
		var fresh = await workflow.CalculateAsync(targetMonth);
		var after = await workflow.EmployeeAsync(fresh.Id, employee.Id);
		Math.Round(Convert.ToDecimal(after.NetPay - before.NetPay), 2)
			.ShouldBe(Math.Round(Convert.ToDecimal(calculated.Candidate!.NetPay) - draft.OriginalNetPay, 2));
		await workflow.ReviewAsync(fresh.Id, employee.Id, targetMonth);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(fresh.Id, employee.Id, targetMonth));
		(await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
			$"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={targetKey}", json))!.Count.ShouldBe(1);
		(await api.GetFromJsonAsync<List<PayslipDocumentDto>>(sourceUrl, json))!.Single().PaymentStatus.ShouldBe("Paid");
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf")))
			.ShouldBe(original.Sha256);
	}
}
