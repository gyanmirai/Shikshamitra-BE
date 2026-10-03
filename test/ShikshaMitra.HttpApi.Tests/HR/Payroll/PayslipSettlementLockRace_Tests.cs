using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipSettlementLockFactAttribute : FactAttribute
{
	public PayslipSettlementLockFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_SETTLEMENT_LOCK_TEST_POSTGRES")))
			Skip = "Set PAYROLL_SETTLEMENT_LOCK_TEST_POSTGRES to a fresh disposable loopback payroll_tests_* database.";
	}
}

public sealed class PayslipSettlementLockTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_SETTLEMENT_LOCK_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipSettlementLockRace_Tests
{
	[PayslipSettlementLockFact]
	public async Task Settlement_and_target_lock_have_one_winner_without_mixed_issued_evidence()
	{
		using var factory = new PayslipSettlementLockTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		var now = DateTime.UtcNow;
		var sourceMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var targetMonth = sourceMonth.AddMonths(1);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipSettlementLock", dateOfJoining: sourceMonth);
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
				PaidAt = DateTimeOffset.UtcNow.AddMinutes(-1), Reference = "Synthetic target-lock race" }, json));
		await workflow.SeedAttendanceAsync(employee.Id, targetMonth);
		var started = await api.PostAsJsonAsync("/api/payroll/payslip-corrections", new StartPayslipCorrectionDto {
			DocumentId = original.Id, IdempotencyKey = Guid.NewGuid(), Reason = "Synthetic settlement-versus-lock race" }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(started);
		var draft = (await started.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		draft.Attendance.First(x => x.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Status = EmployeeAttendanceStatusType.FullDayLeave;
		var response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/calculate",
			new CalculatePayslipCorrectionDto { ConcurrencyStamp = draft.ConcurrencyStamp, Attendance = draft.Attendance }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var calculated = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/review",
			new PayslipCorrectionActionDto { ConcurrencyStamp = calculated.ConcurrencyStamp }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var reviewed = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		var target = await workflow.CalculateAsync(targetMonth);
		var before = await workflow.EmployeeAsync(target.Id, employee.Id);
		await workflow.ReviewAsync(target.Id, employee.Id, targetMonth);
		var settle = api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/settle", new SettlePayslipCorrectionDto {
			ConcurrencyStamp = reviewed.ConcurrencyStamp, TargetMonth = targetMonth.ToString("yyyy-MM"), PreviewRunId = target.Id }, json);
		var locking = workflow.LockAsync(target.Id, employee.Id, targetMonth);
		await Task.WhenAll(settle, locking);
		var settled = (await settle).IsSuccessStatusCode;
		var locked = (await locking).IsSuccessStatusCode;
		settled.ShouldNotBe(locked, "exactly one competing transaction must succeed");
		var correction = (await api.GetFromJsonAsync<List<PayslipCorrectionDto>>(
			$"/api/payroll/payslip-corrections?documentId={original.Id}", json))!.Single();
		correction.Status.ShouldBe(settled ? "APPLIED" : "REVIEWED");
		if (settled)
		{
			(await workflow.StatusAsync(target.Id)).Status.ShouldBe("SUPERSEDED");
			var regenerated = await workflow.CalculateAsync(targetMonth);
			var after = await workflow.EmployeeAsync(regenerated.Id, employee.Id);
			Math.Round(after.NetPay - before.NetPay, 2).ShouldBe(Math.Round(calculated.Candidate!.NetPay - (double)draft.OriginalNetPay, 2));
			await workflow.ReviewAsync(regenerated.Id, employee.Id, targetMonth);
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(regenerated.Id, employee.Id, targetMonth));
		}
		var targetDocuments = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
			$"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={targetMonth:yyyy-MM}", json))!;
		targetDocuments.Count.ShouldBe(1);
		var targetSlip = (await api.GetFromJsonAsync<SalarySlipResponse>(
			$"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={targetMonth:yyyy-MM}", json))!;
		targetSlip.NetSalary.ShouldBe(settled ? before.NetPay + calculated.Candidate!.NetPay - (double)draft.OriginalNetPay : before.NetPay, 0.005);
		(await api.GetFromJsonAsync<List<PayslipDocumentDto>>(sourceUrl, json))!.Single().PaymentStatus.ShouldBe("Paid");
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf"))).ShouldBe(original.Sha256);
	}
}
