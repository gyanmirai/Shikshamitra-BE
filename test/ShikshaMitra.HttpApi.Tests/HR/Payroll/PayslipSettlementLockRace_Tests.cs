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

public sealed class PayslipSettlementLockOrderingTheoryAttribute : TheoryAttribute
{
	public PayslipSettlementLockOrderingTheoryAttribute()
	{
		if (new[] { "SETTLEMENT_FIRST", "LOCK_FIRST" }.Any(order => string.IsNullOrWhiteSpace(
			Environment.GetEnvironmentVariable($"PAYROLL_{order}_TEST_POSTGRES"))))
			Skip = "Set PAYROLL_SETTLEMENT_FIRST_TEST_POSTGRES and PAYROLL_LOCK_FIRST_TEST_POSTGRES to separate fresh disposable loopback payroll_tests_* databases.";
	}
}

public sealed class PayslipSettlementLockOrderingTestFactory(bool settlementFirst) : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => settlementFirst
		? "PAYROLL_SETTLEMENT_FIRST_TEST_POSTGRES" : "PAYROLL_LOCK_FIRST_TEST_POSTGRES";
}

public sealed class PayslipSettlementBoundaryLockOrderingTheoryAttribute : TheoryAttribute
{
	public PayslipSettlementBoundaryLockOrderingTheoryAttribute()
	{
		if (new[] { 3, 9 }.Any(month => new[] { "SETTLEMENT_FIRST", "LOCK_FIRST" }.Any(order =>
			string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable($"PAYROLL_BOUNDARY_{month:00}_{order}_TEST_POSTGRES")))))
			Skip = "Set the four PAYROLL_BOUNDARY_MM_ORDER_TEST_POSTGRES variables to separate fresh disposable loopback payroll_tests_* databases.";
	}
}

public sealed class PayslipSettlementBoundaryLockOrderingTestFactory(int sourceMonth, bool settlementFirst) : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => $"PAYROLL_BOUNDARY_{sourceMonth:00}_{(settlementFirst ? "SETTLEMENT_FIRST" : "LOCK_FIRST")}_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipSettlementLockRace_Tests
{
	[PayslipSettlementLockFact]
	public async Task Settlement_and_target_lock_have_one_winner_without_mixed_issued_evidence()
	{
		using var factory = new PayslipSettlementLockTestFactory();
		await VerifyOneWinnerAsync(factory);
	}

	[PayslipSettlementLockOrderingTheory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Tenant_fence_serializes_each_settlement_and_target_lock_order(bool settlementFirst)
	{
		using var factory = new PayslipSettlementLockOrderingTestFactory(settlementFirst);
		await VerifyOneWinnerAsync(factory, settlementFirst);
	}

	[PayslipSettlementBoundaryLockOrderingTheory]
	[InlineData(3, true)]
	[InlineData(3, false)]
	[InlineData(9, true)]
	[InlineData(9, false)]
	public async Task Contribution_boundary_settlement_and_target_lock_preserve_current_eligibility_in_each_order(
		int sourceMonth, bool settlementFirst)
	{
		using var factory = new PayslipSettlementBoundaryLockOrderingTestFactory(sourceMonth, settlementFirst);
		await VerifyOneWinnerAsync(factory, settlementFirst, sourceMonth);
	}

	private static async Task VerifyOneWinnerAsync(PayslipPostgresTestFactory factory, bool? settlementFirst = null,
		int? sourceMonthNumber = null)
	{
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		var now = DateTime.UtcNow;
		var sourceMonth = new DateTime(now.Year, sourceMonthNumber ?? now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var targetMonth = sourceMonth.AddMonths(1);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipSettlementLock", dateOfJoining: sourceMonth);
		await workflow.ConfigureAsync(true);
		if (sourceMonthNumber.HasValue)
			await PayslipSettlementBoundary_Tests.ConfigureHistoricalFixtureAsync(factory,
				EsiPayrollWorkflowScenario.Tenant, sourceMonth, targetMonth);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, sourceMonth, "GROSS", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, sourceMonth);
		var source = await workflow.CalculateAsync(sourceMonth);
		var sourceItem = await workflow.EmployeeAsync(source.Id, employee.Id);
		sourceItem.EmployeeStateInsuranceCoverageDecision.ShouldBe("WITHIN_WAGE_CEILING");
		await workflow.ReviewAsync(source.Id, employee.Id, sourceMonth);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(source.Id, employee.Id, sourceMonth));
		var sourceUrl = $"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={sourceMonth:yyyy-MM}";
		var original = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(sourceUrl, json))!.Single();
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PostAsJsonAsync("/api/payroll/payslip-documents/payment",
			new RecordPayslipPaymentDto
			{
				DocumentId = original.Id,
				IdempotencyKey = Guid.NewGuid(),
				PaidAt = DateTimeOffset.UtcNow.AddMinutes(-1),
				Reference = "Synthetic target-lock race"
			}, json));
		if (sourceMonthNumber.HasValue)
			await workflow.SaveCompensationAsync(employee.Id, targetMonth, "GROSS", 23000m);
		await workflow.SeedAttendanceAsync(employee.Id, targetMonth);
		var started = await api.PostAsJsonAsync("/api/payroll/payslip-corrections", new StartPayslipCorrectionDto
		{
			DocumentId = original.Id,
			IdempotencyKey = Guid.NewGuid(),
			Reason = "Synthetic settlement-versus-lock race"
		}, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(started);
		var draft = (await started.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		draft.Attendance.First(x => x.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Status = EmployeeAttendanceStatusType.FullDayLeave;
		var response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/calculate",
			new CalculatePayslipCorrectionDto { ConcurrencyStamp = draft.ConcurrencyStamp, Attendance = draft.Attendance }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var calculated = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		calculated.Candidate!.EmployeeStateInsuranceCoverageDecision.ShouldBe("WITHIN_WAGE_CEILING");
		calculated.Candidate.TotalDeduction.ShouldBeLessThan((double) draft.OriginalDeductions);
		response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/review",
			new PayslipCorrectionActionDto { ConcurrencyStamp = calculated.ConcurrencyStamp }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var reviewed = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		var target = await workflow.CalculateAsync(targetMonth);
		var before = await workflow.EmployeeAsync(target.Id, employee.Id);
		if (sourceMonthNumber.HasValue) AssertAboveCeilingTarget(before);
		await workflow.ReviewAsync(target.Id, employee.Id, targetMonth);
		Task<System.Net.Http.HttpResponseMessage> SettleAsync() => api.PostAsJsonAsync(
			$"/api/payroll/payslip-corrections/{draft.Id}/settle", new SettlePayslipCorrectionDto
			{
				ConcurrencyStamp = reviewed.ConcurrencyStamp,
				TargetMonth = targetMonth.ToString("yyyy-MM"),
				PreviewRunId = target.Id
			}, json);
		Task<System.Net.Http.HttpResponseMessage> LockAsync() => workflow.LockAsync(target.Id, employee.Id, targetMonth);
		Task<System.Net.Http.HttpResponseMessage> settle;
		Task<System.Net.Http.HttpResponseMessage> locking;
		if (settlementFirst.HasValue)
		{
			// Each matching read happens after the winning transaction has acquired the tenant revision fence.
			factory.Barrier.Arm(settlementFirst.Value ? "HR_PayslipCorrections" : "HR_PayrollRunItems");
			var first = settlementFirst.Value ? SettleAsync() : LockAsync();
			Task<System.Net.Http.HttpResponseMessage>? second = null;
			try
			{
				await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(25));
				factory.Barrier.Observe("INSERT INTO \"HR_PayrollInputRevisions\"");
				second = settlementFirst.Value ? LockAsync() : SettleAsync();
				await factory.Barrier.Observed.WaitAsync(TimeSpan.FromSeconds(25));
			}
			finally { factory.Barrier.Release(); }
			settle = settlementFirst.Value ? first : second!;
			locking = settlementFirst.Value ? second! : first;
		}
		else
		{
			settle = SettleAsync();
			locking = LockAsync();
		}
		await Task.WhenAll(settle, locking);
		var settled = (await settle).IsSuccessStatusCode;
		var locked = (await locking).IsSuccessStatusCode;
		settled.ShouldNotBe(locked, "exactly one competing transaction must succeed");
		if (settlementFirst.HasValue) settled.ShouldBe(settlementFirst.Value, "the operation holding the tenant fence must win");
		var correction = (await api.GetFromJsonAsync<List<PayslipCorrectionDto>>(
			$"/api/payroll/payslip-corrections?documentId={original.Id}", json))!.Single();
		correction.Status.ShouldBe(settled ? "APPLIED" : "REVIEWED");
		if (settled)
		{
			(await workflow.StatusAsync(target.Id)).Status.ShouldBe("SUPERSEDED");
			await EsiPayrollWorkflowScenario.RequireSuccess(await SettleAsync());
			var regenerated = await workflow.CalculateAsync(targetMonth);
			var after = await workflow.EmployeeAsync(regenerated.Id, employee.Id);
			Math.Round(after.NetPay - before.NetPay, 2).ShouldBe(Math.Round(calculated.Candidate!.NetPay - (double) draft.OriginalNetPay, 2));
			Math.Round(after.EmployerCost - before.EmployerCost, 2).ShouldBe(Math.Round(calculated.Candidate.EmployerCost - (double) draft.OriginalEmployerCost, 2));
			if (sourceMonthNumber.HasValue)
			{
				AssertAboveCeilingTarget(after);
				after.EmployeeStateInsuranceWages.ShouldBe(before.EmployeeStateInsuranceWages);
			}
			var settlementLines = after.EarningComponents.Concat(after.DeductionComponents)
				.Concat(after.EmployerContributions).Where(x => x.Description?.Contains($"correction {draft.Id};", StringComparison.Ordinal) == true).ToList();
			settlementLines.ShouldNotBeEmpty();
			settlementLines.All(x => x.Name.Contains(sourceMonth.ToString("yyyy-MM"), StringComparison.Ordinal) &&
				x.Description!.Contains($"Source document {original.Id};", StringComparison.Ordinal)).ShouldBeTrue();
			settlementLines.Select(x => x.Description).Distinct().Count().ShouldBe(settlementLines.Count);
			await workflow.ReviewAsync(regenerated.Id, employee.Id, targetMonth);
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(regenerated.Id, employee.Id, targetMonth));
		}
		else (await SettleAsync()).IsSuccessStatusCode.ShouldBeFalse();
		var targetDocuments = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
			$"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={targetMonth:yyyy-MM}", json))!;
		targetDocuments.Count.ShouldBe(1);
		var targetSlip = (await api.GetFromJsonAsync<SalarySlipResponse>(
			$"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={targetMonth:yyyy-MM}", json))!;
		targetSlip.NetSalary.ShouldBe(settled ? before.NetPay + calculated.Candidate!.NetPay - (double) draft.OriginalNetPay : before.NetPay, 0.005);
		Math.Round(targetSlip.TotalEarning - targetSlip.TotalDeduction, 2).ShouldBe(Math.Round(targetSlip.NetSalary, 2));
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{targetDocuments[0].Id}/pdf")))
			.ShouldBe(targetDocuments[0].Sha256);
		(await api.GetFromJsonAsync<List<PayslipDocumentDto>>(sourceUrl, json))!.Single().PaymentStatus.ShouldBe("Paid");
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf"))).ShouldBe(original.Sha256);
		var protectedSource = (await api.GetFromJsonAsync<SalarySlipResponse>(
			$"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={sourceMonth:yyyy-MM}", json))!;
		protectedSource.NetSalary.ShouldBe(sourceItem.NetPay, 0.005);
		factory.Messages.ShouldBeEmpty();
	}

	private static void AssertAboveCeilingTarget(ShikshaMitra.HR.Payroll.RunPayroll.RunPayrollItem item)
	{
		item.EmployeeStateInsuranceCoverageDecision.ShouldBe("ABOVE_WAGE_CEILING");
		item.DeductionComponents.Where(x => x.Name == "Employee State Insurance (ESI) - Auto").Sum(x => x.Amount).ShouldBe(0);
		item.EmployerContributions.Where(x => x.Name == "Employer State Insurance (ESI)").Sum(x => x.Amount).ShouldBe(0);
	}
}