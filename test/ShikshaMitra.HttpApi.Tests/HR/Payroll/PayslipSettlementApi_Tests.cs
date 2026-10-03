using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayslipSettlementPostgresFactAttribute : FactAttribute
{
	public PayslipSettlementPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_SETTLEMENT_TEST_POSTGRES")))
			Skip = "Set PAYROLL_SETTLEMENT_TEST_POSTGRES to a fresh disposable loopback payroll_tests_* database.";
	}
}
public class PayslipSettlementPostgresTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_SETTLEMENT_TEST_POSTGRES";
}
[Collection(ApiTestCollection.Name)]
public class PayslipSettlementApi_Tests
{
	[PayslipSettlementPostgresFact]
	public async Task Paid_correction_settles_once_in_later_pipeline_and_preserves_paid_original()
	{
		using var factory = new PayslipSettlementPostgresTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		var now = DateTime.UtcNow;
		var sourceMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var targetMonth = sourceMonth.AddMonths(1);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipSettlement", dateOfJoining: sourceMonth);
		await workflow.ConfigureAsync(true);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, sourceMonth, "GROSS", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, sourceMonth);
		var source = await workflow.CalculateAsync(sourceMonth);
		await workflow.ReviewAsync(source.Id, employee.Id, sourceMonth);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(source.Id, employee.Id, sourceMonth));
		var url = $"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={sourceMonth:yyyy-MM}";
		var original = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(url, json))!.Single();
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PostAsJsonAsync("/api/payroll/payslip-documents/payment", new RecordPayslipPaymentDto {
			DocumentId = original.Id, IdempotencyKey = Guid.NewGuid(), PaidAt = DateTimeOffset.UtcNow.AddMinutes(-1), Reference = "Capture-only synthetic payment evidence" }, json));
		await workflow.SeedAttendanceAsync(employee.Id, targetMonth);
		var target = await workflow.CalculateAsync(targetMonth);
		var before = await workflow.EmployeeAsync(target.Id, employee.Id);
		var started = await api.PostAsJsonAsync("/api/payroll/payslip-corrections", new StartPayslipCorrectionDto {
			DocumentId = original.Id, IdempotencyKey = Guid.NewGuid(), Reason = "Synthetic paid attendance correction" }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(started);
		var draft = (await started.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		draft.Attendance.First(x => x.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Status = EmployeeAttendanceStatusType.FullDayLeave;
		var response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/calculate", new CalculatePayslipCorrectionDto {
			ConcurrencyStamp = draft.ConcurrencyStamp, Attendance = draft.Attendance }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var calculated = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/review", new PayslipCorrectionActionDto {
			ConcurrencyStamp = calculated.ConcurrencyStamp }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var reviewed = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		var request = new SettlePayslipCorrectionDto { ConcurrencyStamp = reviewed.ConcurrencyStamp,
			TargetMonth = targetMonth.ToString("yyyy-MM"), PreviewRunId = target.Id };
		var settleUrl = $"/api/payroll/payslip-corrections/{draft.Id}/settle";
		using var denied = factory.CreateClient();
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
		(await denied.PostAsJsonAsync(settleUrl, request, json)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var otherTenant = factory.CreateClient();
		otherTenant.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host");
		(await otherTenant.PostAsJsonAsync(settleUrl, request, json)).IsSuccessStatusCode.ShouldBeFalse();
		using var outsideScope = factory.CreateClient();
		outsideScope.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
		outsideScope.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Teacher");
		(await outsideScope.PostAsJsonAsync(settleUrl, request, json)).IsSuccessStatusCode.ShouldBeFalse();
		(await api.PostAsJsonAsync(settleUrl, new SettlePayslipCorrectionDto { ConcurrencyStamp = "stale",
			TargetMonth = request.TargetMonth, PreviewRunId = target.Id }, json)).IsSuccessStatusCode.ShouldBeFalse();
		(await api.PostAsJsonAsync(settleUrl, new SettlePayslipCorrectionDto { ConcurrencyStamp = request.ConcurrencyStamp,
			TargetMonth = sourceMonth.ToString("yyyy-MM"), PreviewRunId = target.Id }, json)).IsSuccessStatusCode.ShouldBeFalse();
		var concurrent = await Task.WhenAll(api.PostAsJsonAsync(settleUrl, request, json), api.PostAsJsonAsync(settleUrl, request, json));
		foreach (var result in concurrent) await EsiPayrollWorkflowScenario.RequireSuccess(result);
		(await workflow.StatusAsync(target.Id)).Status.ShouldBe("SUPERSEDED");
		(await workflow.LockAsync(target.Id, employee.Id, targetMonth)).IsSuccessStatusCode.ShouldBeFalse();
		var recalculated = await workflow.CalculateAsync(targetMonth);
		var after = await workflow.EmployeeAsync(recalculated.Id, employee.Id);
		Math.Round(after.NetPay - before.NetPay, 2).ShouldBe(Math.Round(calculated.Candidate!.NetPay - (double)draft.OriginalNetPay, 2));
		Math.Round(after.EmployerCost - before.EmployerCost, 2).ShouldBe(Math.Round(calculated.Candidate.EmployerCost - (double)draft.OriginalEmployerCost, 2));
		await workflow.ReviewAsync(recalculated.Id, employee.Id, targetMonth);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(recalculated.Id, employee.Id, targetMonth));
		(await api.GetFromJsonAsync<List<PayslipDocumentDto>>(url, json))!.Single().PaymentStatus.ShouldBe("Paid");
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf"))).ShouldBe(original.Sha256);
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PostAsJsonAsync(settleUrl, request, json));
		// A second correction starts from the logically corrected source, never charging the same difference twice.
		var nextMonth = targetMonth.AddMonths(1);
		await workflow.SeedAttendanceAsync(employee.Id, nextMonth);
		var nextTarget = await workflow.CalculateAsync(nextMonth);
		var nextBefore = await workflow.EmployeeAsync(nextTarget.Id, employee.Id);
		for (var iteration = 0; iteration < 2; iteration++)
		{
			response = await api.PostAsJsonAsync("/api/payroll/payslip-corrections", new StartPayslipCorrectionDto {
				DocumentId = original.Id, IdempotencyKey = Guid.NewGuid(), Reason = iteration == 0 ? "Restore verified paid attendance" : "Verify zero monetary difference" }, json);
			await EsiPayrollWorkflowScenario.RequireSuccess(response);
			var followup = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
			if (iteration == 0) followup.OriginalNetPay.ShouldBe((decimal)calculated.Candidate.NetPay);
			foreach (var attendance in followup.Attendance) attendance.Status = EmployeeAttendanceStatusType.Present;
			response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{followup.Id}/calculate", new CalculatePayslipCorrectionDto {
				ConcurrencyStamp = followup.ConcurrencyStamp, Attendance = followup.Attendance }, json);
			await EsiPayrollWorkflowScenario.RequireSuccess(response);
			var revised = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
			response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{followup.Id}/review", new PayslipCorrectionActionDto {
				ConcurrencyStamp = revised.ConcurrencyStamp }, json);
			await EsiPayrollWorkflowScenario.RequireSuccess(response);
			var accepted = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
			var followupUrl = $"/api/payroll/payslip-corrections/{followup.Id}/settle";
			if (iteration == 0)
			{
				var lockedPreview = await workflow.CalculateAsync(targetMonth);
				(await api.PostAsJsonAsync(followupUrl, new SettlePayslipCorrectionDto {
					ConcurrencyStamp = accepted.ConcurrencyStamp, TargetMonth = targetMonth.ToString("yyyy-MM"), PreviewRunId = lockedPreview.Id }, json)).IsSuccessStatusCode.ShouldBeFalse();
			}
			var followupRequest = new SettlePayslipCorrectionDto { ConcurrencyStamp = accepted.ConcurrencyStamp,
				TargetMonth = nextMonth.ToString("yyyy-MM"), PreviewRunId = nextTarget.Id };
			await EsiPayrollWorkflowScenario.RequireSuccess(await api.PostAsJsonAsync(followupUrl, followupRequest, json));
			nextTarget = await workflow.CalculateAsync(nextMonth);
			var nextAfter = await workflow.EmployeeAsync(nextTarget.Id, employee.Id);
			Math.Round(nextAfter.NetPay - nextBefore.NetPay, 2).ShouldBe(Math.Round(revised.Candidate!.NetPay - (double)followup.OriginalNetPay, 2));
			if (iteration == 1) revised.Candidate.NetPay.ShouldBe((double)followup.OriginalNetPay);
			nextBefore = nextAfter;
		}
		await workflow.ReviewAsync(nextTarget.Id, employee.Id, nextMonth);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(nextTarget.Id, employee.Id, nextMonth));
		(await api.GetFromJsonAsync<List<PayslipDocumentDto>>(url, json))!.Count.ShouldBe(1);
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf"))).ShouldBe(original.Sha256);
	}
}
