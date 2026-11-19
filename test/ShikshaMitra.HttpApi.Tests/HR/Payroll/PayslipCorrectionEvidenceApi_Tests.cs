using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Statutory;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipCorrectionEvidenceFactAttribute : FactAttribute
{
	public PayslipCorrectionEvidenceFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_CORRECTION_EVIDENCE_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_CORRECTION_EVIDENCE_TEST_POSTGRES to a fresh owned loopback payroll_tests_* database.";
	}
}

public sealed class PayslipCorrectionEvidenceTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_CORRECTION_EVIDENCE_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipCorrectionEvidenceApi_Tests
{
	[PayslipCorrectionEvidenceFact]
	public async Task Reissued_coverage_evidence_invalidates_later_payroll_and_preserves_other_contribution_periods()
	{
		using var factory = new PayslipCorrectionEvidenceTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		await workflow.ConfigureAsync(true);
		var now = DateTime.UtcNow;
		var sourceMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var laterMonth = sourceMonth.AddMonths(1);
		var (_, periodEnd) = EsiStatutoryCalculator.GetContributionPeriod(sourceMonth);
		var continuityApplies = laterMonth <= periodEnd;
		var outsideMonth = periodEnd.AddDays(1);
		if (outsideMonth == laterMonth) outsideMonth = outsideMonth.AddMonths(1);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipCoverageEvidence", dateOfJoining: sourceMonth);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, sourceMonth, "GROSS", 22000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, sourceMonth);
		await workflow.SeedAttendanceAsync(employee.Id, laterMonth);
		await workflow.SeedAttendanceAsync(employee.Id, outsideMonth);
		var sourceRun = await workflow.CalculateAsync(sourceMonth);
		var sourceItem = await workflow.EmployeeAsync(sourceRun.Id, employee.Id);
		sourceItem.EmployeeStateInsuranceCoverageDecision.ShouldBe("ABOVE_WAGE_CEILING");
		EmployeeEsi(sourceItem).ShouldBe(0);
		sourceItem.DeductionComponents.Single(x => x.Name == "Professional Tax").Amount.ShouldBe(200);
		sourceItem.NetPay.ShouldBe(21800);
		sourceItem.EmployerCost.ShouldBe(22000);
		await workflow.ReviewAsync(sourceRun.Id, employee.Id, sourceMonth);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(sourceRun.Id, employee.Id, sourceMonth));
		var documentsUrl = $"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={sourceMonth:yyyy-MM}";
		var original = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!.Single();

		// This tests the existing approved exception contract, not legal entitlement to an exception.
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PostAsJsonAsync(
			$"/api/payroll/statutory-schemes/esi/employees/{employee.Id}/overrides", new CreateStatutoryOverrideDto
			{
				OverrideType = "ForceCovered",
				EffectiveFrom = sourceMonth,
				EffectiveTo = sourceMonth.AddMonths(1).AddDays(-1),
				Reason = "Synthetic correction of omitted contribution-period coverage evidence",
				EvidenceReference = "SYNTHETIC_APPROVED_COVERAGE_EVIDENCE"
			}, json));
		var response = await api.PostAsJsonAsync("/api/payroll/payslip-corrections", new StartPayslipCorrectionDto
		{
			DocumentId = original.Id,
			IdempotencyKey = Guid.NewGuid(),
			Reason = "Correct the synthetic source-period coverage evidence"
		}, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var draft = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/calculate",
			new CalculatePayslipCorrectionDto { ConcurrencyStamp = draft.ConcurrencyStamp, Attendance = draft.Attendance }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var calculated = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		calculated.Candidate!.EmployeeStateInsuranceCoverageDecision.ShouldBe("FORCE_COVERED_OVERRIDE");
		EmployeeEsi(calculated.Candidate).ShouldBe(165);
		calculated.Candidate.EmployerContributions.Sum(x => Convert.ToDouble(x.Amount)).ShouldBe(715);
		calculated.Candidate.DeductionComponents.Single(x => x.Name == "Professional Tax").Amount.ShouldBe(200);
		calculated.Candidate.TotalDeduction.ShouldBe(365);
		calculated.Candidate.NetPay.ShouldBe(21635);
		calculated.Candidate.EmployerCost.ShouldBe(22715);
		response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/review",
			new PayslipCorrectionActionDto { ConcurrencyStamp = calculated.ConcurrencyStamp }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var reviewed = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;

		// Generate these after all ordinary input changes, while the reviewed correction is still unapplied.
		var later = await workflow.CalculateAsync(laterMonth);
		(await workflow.EmployeeAsync(later.Id, employee.Id)).EmployeeStateInsuranceCoverageDecision.ShouldBe("ABOVE_WAGE_CEILING");
		EmployeeEsi(await workflow.EmployeeAsync(later.Id, employee.Id)).ShouldBe(0);
		await workflow.ReviewAsync(later.Id, employee.Id, laterMonth);
		var outside = await workflow.CalculateAsync(outsideMonth);
		EmployeeEsi(await workflow.EmployeeAsync(outside.Id, employee.Id)).ShouldBe(0);
		await workflow.ReviewAsync(outside.Id, employee.Id, outsideMonth);

		var apply = new PayslipCorrectionActionDto { ConcurrencyStamp = reviewed.ConcurrencyStamp };
		response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/apply", apply, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var applied = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		applied.Status.ShouldBe("APPLIED");
		var laterStatus = await workflow.StatusAsync(later.Id);
		laterStatus.Status.ShouldBe(continuityApplies ? "SUPERSEDED" : "SUCCEEDED");
		laterStatus.IsCurrent.ShouldBe(!continuityApplies);
		if (continuityApplies)
		{
			var staleReview = await api.PostAsJsonAsync("/api/payroll/run/reviews",
				new { month = laterMonth.ToString("yyyy-MM"), previewRunId = later.Id, employeeIds = new[] { employee.Id } });
			var staleReviewBody = await staleReview.Content.ReadAsStringAsync();
			staleReview.StatusCode.ShouldBe(HttpStatusCode.Conflict, staleReviewBody);
			staleReviewBody.ShouldContain(HRErrorCodes.PayrollPreviewStale);
			var staleLock = await workflow.LockAsync(later.Id, employee.Id, laterMonth);
			var staleLockBody = await staleLock.Content.ReadAsStringAsync();
			staleLock.StatusCode.ShouldBe(HttpStatusCode.Conflict, staleLockBody);
			staleLockBody.ShouldContain(HRErrorCodes.PayrollPreviewStale);
		}
		var outsideStatus = await workflow.StatusAsync(outside.Id);
		outsideStatus.Status.ShouldBe("SUCCEEDED");
		outsideStatus.IsCurrent.ShouldBeTrue();
		var freshLater = await workflow.CalculateAsync(laterMonth);
		var fresh = await workflow.EmployeeAsync(freshLater.Id, employee.Id);
		fresh.EmployeeStateInsuranceCoverageDecision.ShouldBe(continuityApplies ? "CONTRIBUTION_PERIOD_CONTINUATION" : "ABOVE_WAGE_CEILING");
		EmployeeEsi(fresh).ShouldBe(continuityApplies ? 165 : 0);
		fresh.DeductionComponents.Single(x => x.Name == "Professional Tax").Amount.ShouldBe(200);
		fresh.NetPay.ShouldBe(continuityApplies ? 21635 : 21800);
		fresh.EmployerCost.ShouldBe(continuityApplies ? 22715 : 22000);
		await workflow.ReviewAsync(freshLater.Id, employee.Id, laterMonth);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(freshLater.Id, employee.Id, laterMonth));
		var laterSlip = (await api.GetFromJsonAsync<SalarySlipResponse>(
			$"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={laterMonth:yyyy-MM}", json))!;
		laterSlip.Status.ShouldBe("Finalized");
		laterSlip.NetSalary.ShouldBe(fresh.NetPay);
		response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/apply", apply, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		(await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!.ResultDocumentId.ShouldBe(applied.ResultDocumentId);
		var versions = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!;
		versions.Count.ShouldBe(2);
		versions.Single(x => x.IsCurrent).Id.ShouldBe(applied.ResultDocumentId!.Value);
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf"))).ShouldBe(original.Sha256);
		factory.Messages.ShouldBeEmpty();
	}

	private static double EmployeeEsi(RunPayrollItem item) => item.DeductionComponents
		.Where(x => x.Name == "Employee State Insurance (ESI) - Auto").Sum(x => Convert.ToDouble(x.Amount));
}