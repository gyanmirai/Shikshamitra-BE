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
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Statutory;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipSettlementBoundaryTheoryAttribute : TheoryAttribute
{
	public PayslipSettlementBoundaryTheoryAttribute()
	{
		if (new[] { 2, 3, 8, 9 }.Any(month => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(
			$"PAYROLL_SETTLEMENT_BOUNDARY_{month:00}_TEST_POSTGRES"))))
			Skip = "Set all four PAYROLL_SETTLEMENT_BOUNDARY_MM_TEST_POSTGRES variables to separate fresh disposable loopback payroll_tests_* databases.";
	}
}

public sealed class PayslipSettlementBoundaryTestFactory(string variable) : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => variable;
}

[Collection(ApiTestCollection.Name)]
public class PayslipSettlementBoundary_Tests
{
	[PayslipSettlementBoundaryTheory]
	[InlineData(2, 3, true)]
	[InlineData(3, 4, false)]
	[InlineData(8, 9, true)]
	[InlineData(9, 10, false)]
	public async Task Paid_source_settlement_keeps_current_period_coverage_and_reconciles_source_statutory_differences(
		int sourceMonthNumber, int targetMonthNumber, bool coverageContinues)
	{
		using var factory = new PayslipSettlementBoundaryTestFactory(
			$"PAYROLL_SETTLEMENT_BOUNDARY_{sourceMonthNumber:00}_TEST_POSTGRES");
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		var sourceMonth = new DateTime(DateTime.UtcNow.Year, sourceMonthNumber, 1, 0, 0, 0, DateTimeKind.Utc);
		var targetMonth = new DateTime(sourceMonth.Year, targetMonthNumber, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipBoundary", dateOfJoining: sourceMonth);
		await workflow.ConfigureAsync(true);
		await ConfigureHistoricalFixtureAsync(factory, EsiPayrollWorkflowScenario.Tenant, sourceMonth, targetMonth);
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
				Reference = "Synthetic contribution-boundary payment evidence"
			}, json));

		// Optimistic revision checks are scoped to the exact effective month; this is that month's first salary revision.
		await workflow.SaveCompensationAsync(employee.Id, targetMonth, "GROSS", 23000m);
		await workflow.SeedAttendanceAsync(employee.Id, targetMonth);
		var target = await workflow.CalculateAsync(targetMonth);
		var before = await workflow.EmployeeAsync(target.Id, employee.Id);
		AssertCurrentCoverage(before, coverageContinues);
		await workflow.ReviewAsync(target.Id, employee.Id, targetMonth);

		var response = await api.PostAsJsonAsync("/api/payroll/payslip-corrections", new StartPayslipCorrectionDto
		{
			DocumentId = original.Id,
			IdempotencyKey = Guid.NewGuid(),
			Reason = "Synthetic paid contribution-period correction"
		}, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var draft = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		draft.Attendance.First(x => x.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Status = EmployeeAttendanceStatusType.FullDayLeave;
		response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/calculate",
			new CalculatePayslipCorrectionDto { ConcurrencyStamp = draft.ConcurrencyStamp, Attendance = draft.Attendance }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var calculated = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		calculated.Candidate!.EmployeeStateInsuranceCoverageDecision.ShouldBe("WITHIN_WAGE_CEILING");
		calculated.Candidate.TotalDeduction.ShouldBeLessThan((double) draft.OriginalDeductions);
		response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/review",
			new PayslipCorrectionActionDto { ConcurrencyStamp = calculated.ConcurrencyStamp }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var reviewed = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		var request = new SettlePayslipCorrectionDto
		{
			ConcurrencyStamp = reviewed.ConcurrencyStamp,
			TargetMonth = targetMonth.ToString("yyyy-MM"),
			PreviewRunId = target.Id
		};
		var settleUrl = $"/api/payroll/payslip-corrections/{draft.Id}/settle";
		foreach (var result in await Task.WhenAll(api.PostAsJsonAsync(settleUrl, request, json), api.PostAsJsonAsync(settleUrl, request, json)))
			await EsiPayrollWorkflowScenario.RequireSuccess(result);
		(await workflow.StatusAsync(target.Id)).Status.ShouldBe("SUPERSEDED");
		(await workflow.LockAsync(target.Id, employee.Id, targetMonth)).IsSuccessStatusCode.ShouldBeFalse();
		(await api.PostAsJsonAsync("/api/payroll/run/reviews", new
		{
			month = request.TargetMonth,
			previewRunId = target.Id,
			employeeIds = new[] { employee.Id }
		}, json)).IsSuccessStatusCode.ShouldBeFalse();

		var regenerated = await workflow.CalculateAsync(targetMonth);
		var after = await workflow.EmployeeAsync(regenerated.Id, employee.Id);
		AssertCurrentCoverage(after, coverageContinues);
		after.EmployeeStateInsuranceWages.ShouldBe(before.EmployeeStateInsuranceWages);
		Money(after.NetPay - before.NetPay).ShouldBe(Money(calculated.Candidate.NetPay - (double) draft.OriginalNetPay));
		Money(after.EmployerCost - before.EmployerCost).ShouldBe(Money(calculated.Candidate.EmployerCost - (double) draft.OriginalEmployerCost));
		await workflow.ReviewAsync(regenerated.Id, employee.Id, targetMonth);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(regenerated.Id, employee.Id, targetMonth));
		var issuedTarget = (await api.GetFromJsonAsync<SalarySlipResponse>(
			$"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={targetMonth:yyyy-MM}", json))!;
		Money(issuedTarget.NetSalary).ShouldBe(Money(after.NetPay));
		Money(issuedTarget.TotalEarning - issuedTarget.TotalDeduction).ShouldBe(Money(issuedTarget.NetSalary));
		var targetDocuments = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
			$"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={targetMonth:yyyy-MM}", json))!;
		targetDocuments.Count.ShouldBe(1);
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{targetDocuments[0].Id}/pdf")))
			.ShouldBe(targetDocuments[0].Sha256);
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PostAsJsonAsync(settleUrl, request, json));
		var corrections = (await api.GetFromJsonAsync<List<PayslipCorrectionDto>>(
			$"/api/payroll/payslip-corrections?documentId={original.Id}", json))!;
		corrections.Count.ShouldBe(1);
		corrections[0].Status.ShouldBe("APPLIED");
		corrections[0].SourceMonth.ShouldBe(sourceMonth.ToString("yyyy-MM"));
		corrections[0].SettlementMonth.ShouldBe(request.TargetMonth);
		(await api.GetFromJsonAsync<List<PayslipDocumentDto>>(sourceUrl, json))!.Single().PaymentStatus.ShouldBe("Paid");
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf"))).ShouldBe(original.Sha256);
		var protectedSource = (await api.GetFromJsonAsync<SalarySlipResponse>(
			$"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={sourceMonth:yyyy-MM}", json))!;
		Money(protectedSource.NetSalary).ShouldBe(Money(sourceItem.NetPay));
	}

	private static decimal Money(double amount) => Math.Round(Convert.ToDecimal(amount), 2);

	internal static async Task ConfigureHistoricalFixtureAsync(PayslipPostgresTestFactory factory,
		Guid tenantId, DateTime sourceMonth, DateTime targetMonth)
	{
		// The full-demo fixture deliberately disables ESIC in September and starts enabled configuration in October.
		// Configure these owned historical test months explicitly; the settings API correctly changes only the current month.
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var configurations = scope.ServiceProvider.GetRequiredService<IRepository<EsiPayrollConfiguration, Guid>>();
		foreach (var month in new[] { sourceMonth, targetMonth })
		{
			foreach (var existing in await configurations.GetListAsync(x => x.EffectiveMonth == month && !x.IsCancelled))
			{
				existing.Cancel("Synthetic contribution-boundary fixture replaces the demo month setting");
				await configurations.UpdateAsync(existing, autoSave: true);
			}
			await configurations.InsertAsync(new EsiPayrollConfiguration(Guid.NewGuid(), tenantId, month, true,
				null, null, $"SYNTHETIC_PAYSLIP_BOUNDARY_{month:yyyyMM}"), autoSave: true);
		}
		await unit.CompleteAsync();
	}

	private static void AssertCurrentCoverage(ShikshaMitra.HR.Payroll.RunPayroll.RunPayrollItem item, bool continues)
	{
		item.EmployeeStateInsuranceCoverageDecision.ShouldBe(continues ? "CONTRIBUTION_PERIOD_CONTINUATION" : "ABOVE_WAGE_CEILING");
		var employeeShare = item.DeductionComponents.Where(x => x.Name == "Employee State Insurance (ESI) - Auto").Sum(x => x.Amount);
		if (continues) employeeShare.ShouldBeGreaterThan(0);
		else employeeShare.ShouldBe(0);
	}
}