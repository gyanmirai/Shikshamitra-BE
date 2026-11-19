using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipCtcSettlementTheoryAttribute : TheoryAttribute
{
	public PayslipCtcSettlementTheoryAttribute()
	{
		if (new[] { 10000, 4000 }.Any(amount => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(
			$"PAYROLL_PAYSLIP_CTC_SETTLEMENT_{amount}_TEST_POSTGRES"))))
			Skip = "Set both PAYROLL_PAYSLIP_CTC_SETTLEMENT_AMOUNT_TEST_POSTGRES variables to separate fresh disposable loopback payroll_tests_* databases.";
	}
}

public sealed class PayslipCtcSettlementTestFactory(int amount) : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => $"PAYROLL_PAYSLIP_CTC_SETTLEMENT_{amount}_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipCtcSettlement_Tests
{
	[PayslipCtcSettlementTheory]
	[InlineData(10000, false)]
	[InlineData(4000, true)]
	public async Task Paid_ctc_recovery_rejects_zero_pay_then_settles_once_after_target_and_source_revalidation(
		int monthlyBudget, bool employeeShareExempt)
	{
		using var factory = new PayslipCtcSettlementTestFactory(monthlyBudget);
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		await workflow.ConfigureAsync(true);
		var now = DateTime.UtcNow;
		var sourceMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var targetMonth = sourceMonth.AddMonths(1);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PaidCtcSettlement_" + monthlyBudget, dateOfJoining: sourceMonth);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, sourceMonth, "EMPLOYER_COST", monthlyBudget);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, sourceMonth);
		await workflow.SeedAttendanceAsync(employee.Id, targetMonth);
		await SetTargetAttendanceFixtureAsync(factory, employee.Id, targetMonth, EmployeeAttendanceStatusType.FullDayLeave);

		var source = await workflow.CalculateAsync(sourceMonth);
		var originalItem = await workflow.EmployeeAsync(source.Id, employee.Id);
		Money(originalItem.EmployerCost).ShouldBe(monthlyBudget);
		Money(originalItem.TotalEarnings + originalItem.EmployerContributionTotal).ShouldBe(monthlyBudget);
		Money(originalItem.TotalEarnings - originalItem.TotalDeduction).ShouldBe(Money(originalItem.NetPay));
		originalItem.EmployeeStateInsuranceCoverageDecision.ShouldBe("WITHIN_WAGE_CEILING");
		originalItem.EmployerContributionTotal.ShouldBeGreaterThan(0);
		AssertEmployeeShare(originalItem.DeductionComponents, employeeShareExempt);
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
				Reference = "Capture-only CTC settlement payment evidence"
			}, json));
		var zeroTarget = await workflow.CalculateAsync(targetMonth);
		var zeroItem = await workflow.EmployeeAsync(zeroTarget.Id, employee.Id);
		zeroItem.NetPay.ShouldBe(0);
		zeroItem.TotalEarnings.ShouldBe(0);
		zeroItem.EmployerContributionTotal.ShouldBe(0);
		await workflow.ReviewAsync(zeroTarget.Id, employee.Id, targetMonth);

		var response = await api.PostAsJsonAsync("/api/payroll/payslip-corrections", new StartPayslipCorrectionDto
		{
			DocumentId = original.Id,
			IdempotencyKey = Guid.NewGuid(),
			Reason = "Synthetic paid CTC attendance recovery"
		}, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var draft = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		draft.Attendance.First(x => x.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Status = EmployeeAttendanceStatusType.FullDayLeave;
		var reviewed = await CalculateAndReviewAsync(draft.ConcurrencyStamp);
		var candidate = reviewed.Candidate!;
		candidate.NetPay.ShouldBeLessThan((double) draft.OriginalNetPay);
		candidate.EmployerCost.ShouldBeLessThan((double) draft.OriginalEmployerCost);
		AssertEmployeeShare(candidate.DeductionComponents, employeeShareExempt);
		var settleUrl = $"/api/payroll/payslip-corrections/{draft.Id}/settle";
		SettlePayslipCorrectionDto Request(Guid preview) => new()
		{
			ConcurrencyStamp = reviewed.ConcurrencyStamp,
			TargetMonth = targetMonth.ToString("yyyy-MM"),
			PreviewRunId = preview
		};
		response = await api.PostAsJsonAsync(settleUrl, Request(zeroTarget.Id), json);
		response.IsSuccessStatusCode.ShouldBeFalse();
		(await response.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.PayslipSettlementInvalid);
		var retained = (await api.GetFromJsonAsync<List<PayslipCorrectionDto>>(
			$"/api/payroll/payslip-corrections?documentId={original.Id}", json))!.Single();
		retained.Status.ShouldBe("REVIEWED");
		retained.SettlementMonth.ShouldBeNull();
		(await workflow.StatusAsync(zeroTarget.Id)).IsCurrent.ShouldBeTrue();
		(await workflow.EmployeeAsync(zeroTarget.Id, employee.Id)).NetPay.ShouldBe(0);
		await AssertOriginalAsync();

		// Change only owned fixture attendance; the production revision tracker must invalidate authority.
		await SetTargetAttendanceFixtureAsync(factory, employee.Id, targetMonth, EmployeeAttendanceStatusType.Present);
		response = await api.PostAsJsonAsync(settleUrl, Request(zeroTarget.Id), json);
		response.IsSuccessStatusCode.ShouldBeFalse();
		(await response.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.PayslipConflict);
		reviewed = await CalculateAndReviewAsync(retained.ConcurrencyStamp);
		Money(reviewed.Candidate!.NetPay).ShouldBe(Money(candidate.NetPay));
		Money(reviewed.Candidate.EmployerCost).ShouldBe(Money(candidate.EmployerCost));
		response = await api.PostAsJsonAsync(settleUrl, Request(zeroTarget.Id), json);
		response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await response.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.PayrollPreviewStale);

		var target = await workflow.CalculateAsync(targetMonth);
		var before = await workflow.EmployeeAsync(target.Id, employee.Id);
		Money(before.EmployerCost).ShouldBe(monthlyBudget);
		await workflow.ReviewAsync(target.Id, employee.Id, targetMonth);
		var request = Request(target.Id);
		var concurrent = await Task.WhenAll(api.PostAsJsonAsync(settleUrl, request, json), api.PostAsJsonAsync(settleUrl, request, json));
		foreach (var result in concurrent) await EsiPayrollWorkflowScenario.RequireSuccess(result);
		(await workflow.StatusAsync(target.Id)).Status.ShouldBe("SUPERSEDED");
		(await workflow.StatusAsync(target.Id)).IsCurrent.ShouldBeFalse();
		(await workflow.LockAsync(target.Id, employee.Id, targetMonth)).IsSuccessStatusCode.ShouldBeFalse();

		var fresh = await workflow.CalculateAsync(targetMonth);
		var after = await workflow.EmployeeAsync(fresh.Id, employee.Id);
		Money(after.NetPay - before.NetPay).ShouldBe(Money(candidate.NetPay - (double) draft.OriginalNetPay));
		Money(after.EmployerCost - before.EmployerCost).ShouldBe(Money(candidate.EmployerCost - (double) draft.OriginalEmployerCost));
		Money(after.EmployerContributionTotal - before.EmployerContributionTotal)
			.ShouldBe(Money(candidate.EmployerContributionTotal - originalItem.EmployerContributionTotal));
		after.EmployeeStateInsuranceWages.ShouldBe(before.EmployeeStateInsuranceWages);
		after.EmployeeStateInsuranceCoverageDecision.ShouldBe(before.EmployeeStateInsuranceCoverageDecision);
		after.GrossSalary.ShouldBe(before.GrossSalary);
		Money(after.EarningComponents.Sum(x => x.Amount) - after.TotalDeduction).ShouldBe(Money(after.NetPay));
		await workflow.ReviewAsync(fresh.Id, employee.Id, targetMonth);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(fresh.Id, employee.Id, targetMonth));
		var issued = (await api.GetFromJsonAsync<SalarySlipResponse>(
			$"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={targetMonth:yyyy-MM}", json))!;
		Money(issued.NetSalary).ShouldBe(Money(after.NetPay));
		Money(issued.TotalEarning - issued.TotalDeduction).ShouldBe(Money(issued.NetSalary));
		var targetDocument = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
			$"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={targetMonth:yyyy-MM}", json))!.Single();
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{targetDocument.Id}/pdf")))
			.ShouldBe(targetDocument.Sha256);
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PostAsJsonAsync(settleUrl, request, json));
		var applied = (await api.GetFromJsonAsync<List<PayslipCorrectionDto>>(
			$"/api/payroll/payslip-corrections?documentId={original.Id}", json))!.Single();
		applied.Status.ShouldBe("APPLIED");
		applied.SourceDocumentId.ShouldBe(original.Id);
		applied.SourceMonth.ShouldBe(sourceMonth.ToString("yyyy-MM"));
		applied.SettlementMonth.ShouldBe(targetMonth.ToString("yyyy-MM"));
		await AssertOriginalAsync();
		factory.Messages.ShouldBeEmpty();
		var artifactDirectory = Environment.GetEnvironmentVariable("PAYSLIP_VERIFY_ARTIFACTS");
		if (!string.IsNullOrWhiteSpace(artifactDirectory))
		{
			Directory.CreateDirectory(artifactDirectory);
			await File.WriteAllBytesAsync(Path.Combine(artifactDirectory, $"ctc-{monthlyBudget}-source.pdf"),
				await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf"));
			await File.WriteAllBytesAsync(Path.Combine(artifactDirectory, $"ctc-{monthlyBudget}-target.pdf"),
				await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{targetDocument.Id}/pdf"));
			await File.WriteAllTextAsync(Path.Combine(artifactDirectory, $"ctc-{monthlyBudget}-facts.json"),
				JsonSerializer.Serialize(new
				{
					monthlyBudget,
					employeeShareExempt,
					source = original,
					target = targetDocument,
					originalItem,
					candidate,
					after,
					targetSlip = issued
				}, json));
		}

		async Task<PayslipCorrectionDto> CalculateAndReviewAsync(string stamp)
		{
			var calculatedResponse = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/calculate",
				new CalculatePayslipCorrectionDto { ConcurrencyStamp = stamp, Attendance = draft.Attendance }, json);
			await EsiPayrollWorkflowScenario.RequireSuccess(calculatedResponse);
			var calculated = (await calculatedResponse.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
			var reviewResponse = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/review",
				new PayslipCorrectionActionDto { ConcurrencyStamp = calculated.ConcurrencyStamp }, json);
			await EsiPayrollWorkflowScenario.RequireSuccess(reviewResponse);
			return (await reviewResponse.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		}
		async Task AssertOriginalAsync()
		{
			var protectedDocument = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(sourceUrl, json))!.Single();
			protectedDocument.Id.ShouldBe(original.Id);
			protectedDocument.PaymentStatus.ShouldBe("Paid");
			protectedDocument.Sha256.ShouldBe(original.Sha256);
			Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf"))).ShouldBe(original.Sha256);
			var protectedSlip = (await api.GetFromJsonAsync<SalarySlipResponse>(
				$"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={sourceMonth:yyyy-MM}", json))!;
			Money(protectedSlip.NetSalary).ShouldBe(Money(originalItem.NetPay));
		}
	}

	private static decimal Money(double value) => Math.Round(Convert.ToDecimal(value), 2, MidpointRounding.AwayFromZero);
	private static void AssertEmployeeShare(List<ShikshaMitra.HR.Payroll.RunPayroll.RunPayrollComponentItem> deductions, bool exempt)
	{
		var amount = deductions.Where(x => x.Name == "Employee State Insurance (ESI) - Auto").Sum(x => x.Amount);
		if (exempt) amount.ShouldBe(0);
		else amount.ShouldBeGreaterThan(0);
	}
	private static async Task SetTargetAttendanceFixtureAsync(PayslipPostgresTestFactory factory,
		Guid employeeId, DateTime month, EmployeeAttendanceStatusType status)
	{
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		var start = DateOnly.FromDateTime(month);
		var end = start.AddMonths(1);
		foreach (var attendance in await repository.GetListAsync(x => x.EmployeeId == employeeId && x.AttendanceDate >= start && x.AttendanceDate < end))
		{
			attendance.StatusType = status;
			attendance.Remarks = "Owned disposable CTC target attendance fixture";
			await repository.UpdateAsync(attendance);
		}
		await unit.CompleteAsync();
	}
}