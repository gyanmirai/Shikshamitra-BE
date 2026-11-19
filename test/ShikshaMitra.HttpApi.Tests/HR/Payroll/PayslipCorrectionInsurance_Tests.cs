using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipCorrectionInsuranceFactAttribute : FactAttribute
{
	public PayslipCorrectionInsuranceFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_CORRECTION_INSURANCE_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_CORRECTION_INSURANCE_TEST_POSTGRES to a fresh disposable loopback payroll_tests_* database.";
	}
}

public sealed class PayslipCorrectionInsuranceTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_CORRECTION_INSURANCE_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipCorrectionInsurance_Tests
{
	[PayslipCorrectionInsuranceFact]
	public async Task Covered_corrections_allow_pending_registration_and_preserve_originals()
	{
		using var factory = new PayslipCorrectionInsuranceTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		await workflow.ConfigureAsync(true);
		var now = DateTime.UtcNow;
		var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var originals = new List<PayslipDocumentDto>();
		foreach (var scenario in new[] { "full", "zero", "exempt", "noncovered", "off" })
		{
			if (scenario == "off") await workflow.ConfigureAsync(false);
			var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "CorrectionInsurance_" + scenario, dateOfJoining: month);
			await workflow.EnrollAsync(employee.Id, null);
			var basis = scenario == "zero" ? "EMPLOYER_COST" : "GROSS";
			var amount = scenario switch { "exempt" => 4000m, "noncovered" => 23000m, _ => 10000m };
			await workflow.SaveCompensationAsync(employee.Id, month, basis, amount);
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
			await workflow.SeedAttendanceAsync(employee.Id, month);
			var run = await workflow.CalculateAsync(month);
			await workflow.ReviewAsync(run.Id, employee.Id, month);
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(run.Id, employee.Id, month));
			var documentsUrl = $"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={month:yyyy-MM}";
			var original = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!.Single();
			originals.Add(original);
			var slipUrl = $"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={month:yyyy-MM}";
			var originalSlip = (await api.GetFromJsonAsync<SalarySlipResponse>(slipUrl, json))!;
			var started = await api.PostAsJsonAsync("/api/payroll/payslip-corrections", new StartPayslipCorrectionDto
			{
				DocumentId = original.Id,
				IdempotencyKey = Guid.NewGuid(),
				Reason = "Synthetic correction insurance verification: " + scenario
			}, json);
			await EsiPayrollWorkflowScenario.RequireSuccess(started);
			var draft = (await started.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
			// The original was issued with registration pending. Keep its effective profile unchanged.
			// Pending registration still permits ordinary salary preparation in an unlocked future month.
			await workflow.SaveCompensationAsync(employee.Id, month.AddMonths(1), basis, amount);
			var attendance = scenario == "zero"
				? draft.Attendance.Select(x => new PayslipCorrectionAttendanceDto { Date = x.Date, Status = EmployeeAttendanceStatusType.FullDayLeave }).ToList()
				: draft.Attendance;
			async Task<PayslipCorrectionDto> CalculateAsync(string stamp)
			{
				var response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/calculate",
					new CalculatePayslipCorrectionDto { ConcurrencyStamp = stamp, Attendance = attendance }, json);
				await EsiPayrollWorkflowScenario.RequireSuccess(response);
				return (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
			}
			var calculated = await CalculateAsync(draft.ConcurrencyStamp);
			calculated.Status.ShouldBe("CALCULATED");
			var candidate = calculated.Candidate!;
			candidate.EmployeeStateInsuranceNumber.ShouldBeNullOrWhiteSpace();
			var covered = scenario is not ("noncovered" or "off");
			if (covered)
			{
				candidate.EmployeeStateInsuranceCoverageDecision.ShouldBe("WITHIN_WAGE_CEILING");
				candidate.IssueCodes.ShouldContain("ESI_NUMBER_MISSING");
				if (scenario == "zero")
				{
					candidate.TotalEarnings.ShouldBe(0);
					candidate.NetPay.ShouldBe(0);
					candidate.EmployerContributionTotal.ShouldBe(0);
				}
				else
				{
					candidate.EmployerContributionTotal.ShouldBeGreaterThan(0);
					var employeeShare = candidate.DeductionComponents.Where(x => x.Name.Contains("ESI", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
					if (scenario == "exempt") employeeShare.ShouldBe(0);
					else employeeShare.ShouldBeGreaterThan(0);
				}
				// Registration is follow-up data. The normal review/reissue below must succeed without it.
				(await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!.Count.ShouldBe(1);
				(await api.GetFromJsonAsync<SalarySlipResponse>(slipUrl, json))!.NetSalary.ShouldBe(originalSlip.NetSalary);
				Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf"))).ShouldBe(original.Sha256);
			}
			else
			{
				candidate.IssueCodes.ShouldNotContain(IssueCodes.EsiInsuranceNumberRequired);
				candidate.EmployerContributionTotal.ShouldBe(0);
				candidate.DeductionComponents.ShouldNotContain(x => x.Name.Contains("ESI", StringComparison.OrdinalIgnoreCase));
				if (scenario == "noncovered") candidate.EmployeeStateInsuranceCoverageDecision.ShouldBe("ABOVE_WAGE_CEILING");
			}
			var review = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/review",
				new PayslipCorrectionActionDto { ConcurrencyStamp = calculated.ConcurrencyStamp }, json);
			await EsiPayrollWorkflowScenario.RequireSuccess(review);
			var reviewed = (await review.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
			var apply = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/apply",
				new PayslipCorrectionActionDto { ConcurrencyStamp = reviewed.ConcurrencyStamp }, json);
			await EsiPayrollWorkflowScenario.RequireSuccess(apply);
			var applied = (await apply.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
			applied.Status.ShouldBe("APPLIED");
			var versions = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!;
			versions.Count.ShouldBe(2);
			versions.Single(x => x.IsCurrent).Id.ShouldBe(applied.ResultDocumentId!.Value);
			versions.Single(x => x.IsCurrent).ReplacesDocumentId.ShouldBe(original.Id);
			var correctedSlip = (await api.GetFromJsonAsync<SalarySlipResponse>(slipUrl, json))!;
			correctedSlip.NetSalary.ShouldBe(calculated.Candidate!.NetPay);
			correctedSlip.Status.ShouldBe("Finalized");
			foreach (var prior in originals)
				Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{prior.Id}/pdf"))).ShouldBe(prior.Sha256);
			factory.Messages.ShouldBeEmpty();
		}
	}
}
