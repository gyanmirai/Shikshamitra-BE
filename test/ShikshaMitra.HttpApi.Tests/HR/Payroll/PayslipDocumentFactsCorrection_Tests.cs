using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipFactsCorrectionFactAttribute : FactAttribute
{
	public PayslipFactsCorrectionFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_FACTS_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_FACTS_TEST_POSTGRES to a separate fresh disposable loopback payroll_tests_* database.";
	}
}

public sealed class PayslipFactsCorrectionTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_FACTS_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipDocumentFactsCorrection_Tests
{
	[PayslipFactsCorrectionFact]
	public async Task Unpaid_and_paid_identity_replacements_preserve_money_statutory_evidence_original_pdf_and_delivery_history()
	{
		using var factory = new PayslipFactsCorrectionTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		await workflow.ConfigureAsync(true);
		var settings = (await api.GetFromJsonAsync<PayslipSettingsDto>("/api/payroll/payslip-settings", json))!;
		settings.AutomaticEmail.ShouldBeFalse();
		var now = DateTime.UtcNow;
		var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		foreach (var paid in new[] { false, true })
		{
			var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipFacts_" + (paid ? "paid" : "unpaid"), dateOfJoining: month);
			employee.Department = "Original synthetic department";
			employee.Designation = "Original synthetic designation";
			await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync($"/api/employees/{employee.Id}/personal-info", employee, json));
			await workflow.EnrollAsync(employee.Id, "1234567890");
			var compensation = await workflow.SaveCompensationAsync(employee.Id, month, "EMPLOYER_COST", 10000m);
			var payrollUrl = $"/api/employees/{employee.Id}/payroll";
			var payroll = new PayrollContract
			{
				BankDetails = new BankDetailsContract
				{
					BankName = "State Bank of India", BranchName = "Synthetic branch", AccountHolderName = employee.FullName,
					AccountNumber = "1234567890", IfscCode = "SBIN0001234"
				},
				SalaryDetails = new SalaryDetailsContract
				{
					QuotedSalaryBasis = "EMPLOYER_COST", QuotedMonthlyAmount = compensation.QuotedMonthlyAmount,
					SourceSalaryTemplateId = compensation.SourceSalaryTemplateId,
					SourceSalaryTemplateVersionId = compensation.SourceSalaryTemplateVersionId,
					SourceSalaryTemplateVersion = compensation.SourceSalaryTemplateVersion, SalaryGeneratedAt = compensation.SavedAt
				}
			};
			await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync(payrollUrl, payroll, json));
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
			await workflow.SeedAttendanceAsync(employee.Id, month);
			var run = await workflow.CalculateAsync(month);
			await workflow.ReviewAsync(run.Id, employee.Id, month);
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(run.Id, employee.Id, month));
			var query = $"employeeId={employee.Id}&monthYear={month:yyyy-MM}";
			var slipsUrl = $"/api/payroll/salary-slips?{query}";
			var documentsUrl = $"/api/payroll/payslip-documents?{query}";
			var original = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!.Single();
			var before = (await api.GetFromJsonAsync<SalarySlipResponse>(slipsUrl, json))!;
			var originalBytes = await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf");
			if (paid)
				await EsiPayrollWorkflowScenario.RequireSuccess(await api.PostAsJsonAsync("/api/payroll/payslip-documents/payment",
					new RecordPayslipPaymentDto { DocumentId = original.Id, IdempotencyKey = Guid.NewGuid(),
						PaidAt = DateTimeOffset.UtcNow.AddMinutes(-1), Reference = "Synthetic facts-correction payment" }, json));
			var sendResponse = await api.PostAsJsonAsync("/api/payroll/payslip-deliveries",
				new SendPayslipDto { DocumentId = original.Id, IdempotencyKey = Guid.NewGuid() }, json);
			await EsiPayrollWorkflowScenario.RequireSuccess(sendResponse);
			var delivery = (await sendResponse.Content.ReadFromJsonAsync<PayslipDeliveryDto>(json))!;
			using (var scope = factory.Services.CreateScope())
				await scope.ServiceProvider.GetRequiredService<PayslipDeliveryBackgroundJob>().ExecuteAsync(
					new PayslipDeliveryJobArgs { TenantId = EsiPayrollWorkflowScenario.Tenant, DeliveryId = delivery.Id });
			var captured = factory.Messages.Single(x => x.Recipient == original.RecipientEmail);
			Convert.ToHexString(SHA256.HashData(captured.Pdf)).ShouldBe(original.Sha256);
			employee.FirstName = "CorrectedSynthetic";
			employee.Department = "Corrected synthetic department";
			employee.Designation = "Corrected synthetic designation";
			await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync($"/api/employees/{employee.Id}/personal-info", employee, json));
			payroll.BankDetails.AccountNumber = "9876543210";
			await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync(payrollUrl, payroll, json));
			var stillIssued = (await api.GetFromJsonAsync<SalarySlipResponse>(slipsUrl, json))!;
			stillIssued.EmployeeName.ShouldBe(before.EmployeeName);
			stillIssued.Designation.ShouldBe(before.Designation);
			stillIssued.MaskedAccountNumber.ShouldBe(before.MaskedAccountNumber);
			var replacementRequest = new ReplacePayslipFactsDto { DocumentId = original.Id, IdempotencyKey = Guid.NewGuid(),
				Reason = "Correct synthetic identity and bank details without changing payroll" };
			async Task<PayslipDocumentDto> ReplaceAsync()
			{
				var response = await api.PostAsJsonAsync("/api/payroll/payslip-documents/replace-facts", replacementRequest, json);
				await EsiPayrollWorkflowScenario.RequireSuccess(response);
				return (await response.Content.ReadFromJsonAsync<PayslipDocumentDto>(json))!;
			}
			var competing = await Task.WhenAll(ReplaceAsync(), ReplaceAsync());
			competing[0].Id.ShouldBe(competing[1].Id);
			var replacement = competing[0];
			replacement.Revision.ShouldBe(2);
			replacement.ReplacesDocumentId.ShouldBe(original.Id);
			replacement.ReplacementReason.ShouldBe(replacementRequest.Reason);
			(await ReplaceAsync()).Id.ShouldBe(replacement.Id);
			var after = (await api.GetFromJsonAsync<SalarySlipResponse>(slipsUrl, json))!;
			after.EmployeeName.ShouldBe("CorrectedSynthetic TestDoe");
			after.Department.ShouldBe(employee.Department);
			after.Designation.ShouldBe(employee.Designation);
			after.MaskedAccountNumber.ShouldEndWith("3210");
			after.Status.ShouldBe(paid ? "Paid" : "Finalized");
			FrozenPayroll(after, json).ShouldBe(FrozenPayroll(before, json));
			var history = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!;
			history.Count.ShouldBe(2);
			history.Single(x => x.IsCurrent).Id.ShouldBe(replacement.Id);
			history.Single(x => x.Id == original.Id).IsCurrent.ShouldBeFalse();
			Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf"))).ShouldBe(original.Sha256);
			var replacementBytes = await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{replacement.Id}/pdf");
			Convert.ToHexString(SHA256.HashData(replacementBytes)).ShouldBe(replacement.Sha256);
			Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/salary-slips/pdf?{query}"))).ShouldBe(replacement.Sha256);
			var deliveryHistory = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>($"/api/payroll/payslip-deliveries?salarySlipId={original.SalarySlipId}", json))!;
			deliveryHistory.Single().ShouldSatisfyAllConditions(
				() => deliveryHistory.Single().DocumentId.ShouldBe(original.Id),
				() => deliveryHistory.Single().Status.ShouldBe("PROVIDER_ACCEPTED"));
			replacementRequest.IdempotencyKey = Guid.NewGuid();
			(await api.PostAsJsonAsync("/api/payroll/payslip-documents/replace-facts", replacementRequest, json)).IsSuccessStatusCode.ShouldBeFalse();
			var artifacts = Environment.GetEnvironmentVariable("PAYSLIP_VERIFY_ARTIFACTS");
			if (!string.IsNullOrWhiteSpace(artifacts))
			{
				Directory.CreateDirectory(artifacts);
				var prefix = paid ? "paid" : "unpaid";
				await File.WriteAllBytesAsync(Path.Combine(artifacts, prefix + "-original.pdf"), originalBytes);
				await File.WriteAllBytesAsync(Path.Combine(artifacts, prefix + "-corrected.pdf"), replacementBytes);
				await File.WriteAllTextAsync(Path.Combine(artifacts, prefix + "-original.json"), JsonSerializer.Serialize(before, json));
				await File.WriteAllTextAsync(Path.Combine(artifacts, prefix + "-corrected.json"), JsonSerializer.Serialize(after, json));
			}
		}
		factory.Messages.Count.ShouldBe(2);
	}

	private static string FrozenPayroll(SalarySlipResponse slip, JsonSerializerOptions json) => JsonSerializer.Serialize(new
	{
		slip.MonthYear, slip.FullGrossSalary, slip.GrossSalary, slip.AdditionalEarnings, slip.TotalEarning,
		slip.TotalDeduction, slip.NetPay, slip.NetSalary, slip.NetPayInWords, slip.Earnings, slip.Deductions,
		slip.PayrollBasisDays, slip.PayableDays, slip.PresentDays, slip.HalfWorkingDays, slip.LopDays,
		slip.ProrationFactor, slip.PayrollDayCalculationMode, slip.EmployeeStateInsuranceRuleId,
		slip.EsiConfigurationId, slip.EmployeeStateInsuranceRuleVersion, slip.EmployeeStateInsuranceWages,
		slip.EmployeeStateInsuranceCoverageDecision, slip.EmployeeStateInsuranceNumber,
		slip.EmployeeStateInsuranceContributionPeriodStart, slip.EmployeeStateInsuranceContributionPeriodEnd
	}, json);
}
