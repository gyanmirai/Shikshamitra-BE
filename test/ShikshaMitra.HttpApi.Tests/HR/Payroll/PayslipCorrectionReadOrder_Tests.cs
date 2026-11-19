using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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

public sealed class PayslipCorrectionReadFactAttribute : FactAttribute
{
	public PayslipCorrectionReadFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_CORRECTION_READ_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_CORRECTION_READ_TEST_POSTGRES to a fresh disposable loopback payroll_tests_* database.";
	}
}

public sealed class PayslipCorrectionReadTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_CORRECTION_READ_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipCorrectionReadOrder_Tests
{
	[PayslipCorrectionReadFact]
	public async Task Consecutive_calculations_keep_latest_review_authority_when_earlier_history_is_received_late()
	{
		using var factory = new PayslipCorrectionReadTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		await workflow.ConfigureAsync(true);
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipCorrectionRead", dateOfJoining: month);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, month, "GROSS", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, month);
		var run = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(run.Id, employee.Id, month);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(run.Id, employee.Id, month));
		var documentsUrl = $"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={month:yyyy-MM}";
		var original = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!.Single();
		var startResponse = await api.PostAsJsonAsync("/api/payroll/payslip-corrections", new StartPayslipCorrectionDto
		{
			DocumentId = original.Id,
			IdempotencyKey = Guid.NewGuid(),
			Reason = "Verify consecutive correction calculations"
		}, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(startResponse);
		var draft = (await startResponse.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		var calculateUrl = $"/api/payroll/payslip-corrections/{draft.Id}/calculate";
		var absent = draft.Attendance.Select(x => new PayslipCorrectionAttendanceDto
		{
			Date = x.Date,
			Status = EmployeeAttendanceStatusType.FullDayLeave,
			TimeIn = x.TimeIn,
			TimeOut = x.TimeOut
		}).ToList();
		var firstResponse = await api.PostAsJsonAsync(calculateUrl,
			new CalculatePayslipCorrectionDto { ConcurrencyStamp = draft.ConcurrencyStamp, Attendance = absent }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(firstResponse);
		var first = (await firstResponse.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		first.Candidate!.NetPay.ShouldBeLessThan((double) draft.OriginalNetPay);
		var historyUrl = $"/api/payroll/payslip-corrections?documentId={original.Id}";
		// The earlier GET is processed normally, but its body is consumed after the second write.
		// This is a supported transport ordering, without bypassing concurrency or mutating persisted evidence.
		using var earlierRead = await api.GetAsync(historyUrl, HttpCompletionOption.ResponseHeadersRead);
		await EsiPayrollWorkflowScenario.RequireSuccess(earlierRead);
		var secondResponse = await api.PostAsJsonAsync(calculateUrl,
			new CalculatePayslipCorrectionDto { ConcurrencyStamp = first.ConcurrencyStamp, Attendance = draft.Attendance }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(secondResponse);
		var second = (await secondResponse.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		second.ConcurrencyStamp.ShouldNotBe(first.ConcurrencyStamp);
		second.Candidate!.NetPay.ShouldBe((double) draft.OriginalNetPay);
		var earlierBody = await earlierRead.Content.ReadAsStringAsync();
		var earlier = JsonSerializer.Deserialize<List<PayslipCorrectionDto>>(earlierBody, json)!.Single();
		earlier.ConcurrencyStamp.ShouldBe(first.ConcurrencyStamp);
		earlier.Candidate!.NetPay.ShouldBe(first.Candidate.NetPay);
		var latest = (await api.GetFromJsonAsync<List<PayslipCorrectionDto>>(historyUrl, json))!.Single();
		latest.ConcurrencyStamp.ShouldBe(second.ConcurrencyStamp);
		latest.Candidate!.NetPay.ShouldBe(second.Candidate.NetPay);
		var reviewUrl = $"/api/payroll/payslip-corrections/{draft.Id}/review";
		var staleReview = await api.PostAsJsonAsync(reviewUrl, new PayslipCorrectionActionDto { ConcurrencyStamp = earlier.ConcurrencyStamp }, json);
		staleReview.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await staleReview.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.PayslipConflict);
		var review = await api.PostAsJsonAsync(reviewUrl, new PayslipCorrectionActionDto { ConcurrencyStamp = second.ConcurrencyStamp }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(review);
		var reviewed = (await review.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		reviewed.Status.ShouldBe("REVIEWED");
		var apply = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/apply",
			new PayslipCorrectionActionDto { ConcurrencyStamp = reviewed.ConcurrencyStamp }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(apply);
		var versions = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!;
		versions.Count.ShouldBe(2);
		versions.Single(x => x.IsCurrent).Revision.ShouldBe(2);
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf"))).ShouldBe(original.Sha256);
		factory.Messages.ShouldBeEmpty();
		var artifactRoot = Environment.GetEnvironmentVariable("PAYSLIP_VERIFY_ARTIFACTS");
		if (!string.IsNullOrWhiteSpace(artifactRoot))
		{
			Directory.CreateDirectory(artifactRoot);
			await File.WriteAllTextAsync(Path.Combine(artifactRoot, "api-read-order.json"), JsonSerializer.Serialize(new
			{
				sourceDocument = original,
				draft,
				first,
				earlier,
				second,
				latest,
				reviewed,
				staleReviewStatus = (int) staleReview.StatusCode,
				originalPdfUnchanged = true,
				issuedRevisions = versions.Count
			}, json));
		}
	}
}