using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.MultiTenancy;
using ShikshaMitra.Infrastructure.Auth;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class PayslipDocumentApi_Tests
{
	[PayrollRegistrationPostgresFact]
	public async Task Issued_document_keeps_bank_facts_and_pdf_after_employee_bank_change()
	{
		using var factory = new PayslipPostgresTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var client = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		var initialSettings = (await client.GetFromJsonAsync<PayslipSettingsDto>("/api/payroll/payslip-settings", json))!;
		await EsiPayrollWorkflowScenario.RequireSuccess(await client.PutAsJsonAsync("/api/payroll/payslip-settings",
			new PayslipSettingsDto { SchoolName = "Synthetic School", SchoolAddress = "Test Road, Ahmedabad, Gujarat",
				ConcurrencyStamp = initialSettings.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid() }, json));
		var now = DateTime.UtcNow;
		var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(client, "PayslipDocument", dateOfJoining: month);
		await workflow.ConfigureAsync(true);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		var revision = await workflow.SaveCompensationAsync(employee.Id, month, "GROSS", 10000m);
		var payrollUrl = $"/api/employees/{employee.Id}/payroll";
		var payroll = new PayrollContract
		{
			BankDetails = new BankDetailsContract
			{
				BankName = "State Bank of India", BranchName = "Main Branch",
				AccountHolderName = "Synthetic Payslip Employee", AccountNumber = "1234567890", IfscCode = "SBIN0001234"
			},
			SalaryDetails = new SalaryDetailsContract
			{
				QuotedSalaryBasis = "GROSS", QuotedMonthlyAmount = revision.QuotedMonthlyAmount,
				SourceSalaryTemplateId = revision.SourceSalaryTemplateId,
				SourceSalaryTemplateVersionId = revision.SourceSalaryTemplateVersionId,
				SourceSalaryTemplateVersion = revision.SourceSalaryTemplateVersion,
				SalaryGeneratedAt = revision.SavedAt
			}
		};
		await EsiPayrollWorkflowScenario.RequireSuccess(await client.PutAsJsonAsync(payrollUrl, payroll, json));
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, month);
		var run = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(run.Id, employee.Id, month);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(run.Id, employee.Id, month));

		var query = $"employeeId={employee.Id}&monthYear={month:yyyy-MM}";
		var documentUrl = $"/api/payroll/salary-slips?{query}";
		var pdfUrl = $"/api/payroll/salary-slips/pdf?{query}";
		var before = (await client.GetFromJsonAsync<SalarySlipResponse>(documentUrl, json))!;
		before.Status.ShouldBe("Finalized");
		before.MaskedAccountNumber.ShouldEndWith("7890");
		var pdfBefore = await client.GetByteArrayAsync(pdfUrl);
		payroll.BankDetails.AccountNumber = "9876543210";
		await EsiPayrollWorkflowScenario.RequireSuccess(await client.PutAsJsonAsync(payrollUrl, payroll, json));
		var employeePayroll = (await client.GetFromJsonAsync<EmployeePayrollResponse>(payrollUrl, json))!;
		employeePayroll.BankDetails!.AccountNumber.ShouldBe("9876543210");
		var after = (await client.GetFromJsonAsync<SalarySlipResponse>(documentUrl, json))!;
		var pdfAfter = await client.GetByteArrayAsync(pdfUrl);
		var artifacts = Environment.GetEnvironmentVariable("PAYSLIP_VERIFY_ARTIFACTS");
		if (!string.IsNullOrWhiteSpace(artifacts))
		{
			Directory.CreateDirectory(artifacts);
			await File.WriteAllBytesAsync(Path.Combine(artifacts, "issued-before-bank-change.pdf"), pdfBefore);
			await File.WriteAllBytesAsync(Path.Combine(artifacts, "issued-after-bank-change.pdf"), pdfAfter);
		}
		after.ShouldSatisfyAllConditions(
			() => after.MaskedAccountNumber.ShouldBe(before.MaskedAccountNumber),
			() => after.SlipNumber.ShouldBe(before.SlipNumber),
			() => after.NetPay.ShouldBe(before.NetPay),
			() => Convert.ToHexString(SHA256.HashData(pdfAfter)).ShouldBe(Convert.ToHexString(SHA256.HashData(pdfBefore))));

		var versionsUrl = $"/api/payroll/payslip-documents?{query}";
		var versions = (await client.GetFromJsonAsync<List<PayslipDocumentDto>>(versionsUrl, json))!;
		versions.Count.ShouldBe(1);
		var issued = versions.Single();
		issued.Sha256.ShouldBe(Convert.ToHexString(SHA256.HashData(pdfBefore)));
		issued.IsCurrent.ShouldBeTrue();
		var settingsUrl = "/api/payroll/payslip-settings";
		var settings = (await client.GetFromJsonAsync<PayslipSettingsDto>(settingsUrl, json))!;
		settings.AutomaticEmail.ShouldBeFalse();
		settings.SchoolName = "Updated Synthetic School";
		settings.SchoolAddress = "Different synthetic address";
		settings.IdempotencyKey = Guid.NewGuid();
		await EsiPayrollWorkflowScenario.RequireSuccess(await client.PutAsJsonAsync(settingsUrl, settings, json));
		Convert.ToHexString(SHA256.HashData(await client.GetByteArrayAsync(pdfUrl))).ShouldBe(issued.Sha256);

		var send = new SendPayslipDto { DocumentId = issued.Id, IdempotencyKey = Guid.NewGuid() };
		var response = await client.PostAsJsonAsync("/api/payroll/payslip-deliveries", send, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var delivery = (await response.Content.ReadFromJsonAsync<PayslipDeliveryDto>(json))!;
		var duplicate = await client.PostAsJsonAsync("/api/payroll/payslip-deliveries", send, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(duplicate);
		(await duplicate.Content.ReadFromJsonAsync<PayslipDeliveryDto>(json))!.Id.ShouldBe(delivery.Id);
		async Task ExecuteDeliveryAsync(Guid id)
		{
			using var scope = factory.Services.CreateScope();
			await scope.ServiceProvider.GetRequiredService<PayslipDeliveryBackgroundJob>().ExecuteAsync(new PayslipDeliveryJobArgs
				{ TenantId = Guid.Parse(TestAuthHandler.TestTenantId), DeliveryId = id });
		}
		await Task.WhenAll(ExecuteDeliveryAsync(delivery.Id), ExecuteDeliveryAsync(delivery.Id));
		factory.Messages.Count.ShouldBe(1);
		factory.Messages.Single().Recipient.ShouldBe(issued.RecipientEmail);
		Convert.ToHexString(SHA256.HashData(factory.Messages.Single().Pdf)).ShouldBe(issued.Sha256);
		var historyUrl = $"/api/payroll/payslip-deliveries?salarySlipId={issued.SalarySlipId}";
		(await client.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, json))!.Single().Status.ShouldBe("PROVIDER_ACCEPTED");

		var payment = new RecordPayslipPaymentDto { DocumentId = issued.Id, IdempotencyKey = Guid.NewGuid(),
			PaidAt = DateTimeOffset.UtcNow.AddMinutes(-1), Reference = "Synthetic bank transfer" };
		await EsiPayrollWorkflowScenario.RequireSuccess(await client.PostAsJsonAsync("/api/payroll/payslip-documents/payment", payment, json));
		await EsiPayrollWorkflowScenario.RequireSuccess(await client.PostAsJsonAsync("/api/payroll/payslip-documents/payment", payment, json));
		Convert.ToHexString(SHA256.HashData(await client.GetByteArrayAsync(pdfUrl))).ShouldBe(issued.Sha256);
		(await client.GetFromJsonAsync<SalarySlipResponse>(documentUrl, json))!.Status.ShouldBe("Paid");

		var correction = new ReplacePayslipFactsDto { DocumentId = issued.Id, Reason = "Correct recorded bank details", IdempotencyKey = Guid.NewGuid() };
		var replacementResponse = await client.PostAsJsonAsync("/api/payroll/payslip-documents/replace-facts", correction, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(replacementResponse);
		var replacement = (await replacementResponse.Content.ReadFromJsonAsync<PayslipDocumentDto>(json))!;
		replacement.Revision.ShouldBe(2);
		replacement.ReplacesDocumentId.ShouldBe(issued.Id);
		var replacementRetry = await client.PostAsJsonAsync("/api/payroll/payslip-documents/replace-facts", correction, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(replacementRetry);
		(await replacementRetry.Content.ReadFromJsonAsync<PayslipDocumentDto>(json))!.Id.ShouldBe(replacement.Id);
		var corrected = (await client.GetFromJsonAsync<SalarySlipResponse>(documentUrl, json))!;
		corrected.MaskedAccountNumber.ShouldEndWith("3210");
		corrected.NetPay.ShouldBe(before.NetPay);
		corrected.Status.ShouldBe("Paid");
		Convert.ToHexString(SHA256.HashData(await client.GetByteArrayAsync($"/api/payroll/payslip-documents/{issued.Id}/pdf"))).ShouldBe(issued.Sha256);
		var oldSend = await client.PostAsJsonAsync("/api/payroll/payslip-deliveries", new SendPayslipDto { DocumentId = issued.Id, IdempotencyKey = Guid.NewGuid() }, json);
		oldSend.IsSuccessStatusCode.ShouldBeFalse();
		factory.FailEmail = true;
		var resendResponse = await client.PostAsJsonAsync("/api/payroll/payslip-deliveries", new SendPayslipDto { DocumentId = replacement.Id, IdempotencyKey = Guid.NewGuid() }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(resendResponse);
		var resend = (await resendResponse.Content.ReadFromJsonAsync<PayslipDeliveryDto>(json))!;
		await ExecuteDeliveryAsync(resend.Id);
		await ExecuteDeliveryAsync(resend.Id);
		factory.Messages.Count.ShouldBe(1);
		(await client.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, json))!.Single(x => x.Id == resend.Id).Status.ShouldBe("UNCERTAIN");

		using var denied = factory.CreateClient();
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
		foreach (var url in new[] { versionsUrl, historyUrl, settingsUrl, $"/api/payroll/payslip-documents/{issued.Id}/pdf" })
			(await denied.GetAsync(url)).StatusCode.ShouldBe(System.Net.HttpStatusCode.Forbidden);
		(await denied.PostAsJsonAsync("/api/payroll/payslip-deliveries", send, json)).StatusCode.ShouldBe(System.Net.HttpStatusCode.Forbidden);
		(await denied.PostAsJsonAsync("/api/payroll/payslip-documents/payment", payment, json)).StatusCode.ShouldBe(System.Net.HttpStatusCode.Forbidden);
		(await denied.PostAsJsonAsync("/api/payroll/payslip-documents/replace-facts", correction, json)).StatusCode.ShouldBe(System.Net.HttpStatusCode.Forbidden);
		(await denied.PutAsJsonAsync(settingsUrl, settings, json)).StatusCode.ShouldBe(System.Net.HttpStatusCode.Forbidden);
		using var otherTenant = factory.CreateClient();
		otherTenant.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host");
		(await otherTenant.GetAsync($"/api/payroll/payslip-documents/{issued.Id}/pdf")).IsSuccessStatusCode.ShouldBeFalse();
		(await otherTenant.GetAsync(historyUrl)).IsSuccessStatusCode.ShouldBeFalse();
		using var outsideScope = factory.CreateClient();
		outsideScope.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
		outsideScope.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Teacher");
		(await outsideScope.GetAsync($"/api/payroll/payslip-documents/{issued.Id}/pdf")).IsSuccessStatusCode.ShouldBeFalse();
		using var anonymous = factory.CreateClient();
		anonymous.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
		(await anonymous.GetAsync($"/api/payroll/payslip-documents/{issued.Id}/pdf")).IsSuccessStatusCode.ShouldBeFalse();
	}
}
