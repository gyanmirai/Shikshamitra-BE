using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipSettlementMixedFactAttribute : FactAttribute
{
	public PayslipSettlementMixedFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_SETTLEMENT_MIXED_TEST_POSTGRES")))
			Skip = "Set PAYROLL_SETTLEMENT_MIXED_TEST_POSTGRES to a fresh disposable loopback payroll_tests_* database.";
	}
}

public sealed class PayslipSettlementMixedTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_SETTLEMENT_MIXED_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipSettlementMixedEmployee_Tests
{
	[PayslipSettlementMixedFact]
	public async Task Paid_settlement_invalidates_unlocked_work_and_preserves_another_employees_issued_target()
	{
		using var factory = new PayslipSettlementMixedTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		var now = DateTime.UtcNow;
		var sourceMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var targetMonth = sourceMonth.AddMonths(1);
		var correctedEmployee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipMixedCorrection", dateOfJoining: sourceMonth);
		var lockedEmployee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipMixedLocked", dateOfJoining: sourceMonth);
		await workflow.ConfigureAsync(true);
		await workflow.EnrollAsync(correctedEmployee.Id, "1234567890");
		await workflow.EnrollAsync(lockedEmployee.Id, "2345678901");
		await workflow.SaveCompensationAsync(correctedEmployee.Id, sourceMonth, "GROSS", 10000m);
		await workflow.SaveCompensationAsync(lockedEmployee.Id, sourceMonth, "GROSS", 8000m);
		foreach (var employee in new[] { correctedEmployee, lockedEmployee })
		{
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
			await workflow.SeedAttendanceAsync(employee.Id, sourceMonth);
			await workflow.SeedAttendanceAsync(employee.Id, targetMonth);
		}
		var source = await workflow.CalculateAsync(sourceMonth);
		await workflow.ReviewAsync(source.Id, correctedEmployee.Id, sourceMonth);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(source.Id, correctedEmployee.Id, sourceMonth));
		var sourceUrl = $"/api/payroll/payslip-documents?employeeId={correctedEmployee.Id}&monthYear={sourceMonth:yyyy-MM}";
		var original = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(sourceUrl, json))!.Single();
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PostAsJsonAsync("/api/payroll/payslip-documents/payment",
			new RecordPayslipPaymentDto { DocumentId = original.Id, IdempotencyKey = Guid.NewGuid(),
				PaidAt = DateTimeOffset.UtcNow.AddMinutes(-1), Reference = "Capture-only mixed-employee payment evidence" }, json));

		var target = await workflow.CalculateAsync(targetMonth);
		var before = await workflow.EmployeeAsync(target.Id, correctedEmployee.Id);
		await workflow.ReviewAsync(target.Id, correctedEmployee.Id, targetMonth);
		await workflow.ReviewAsync(target.Id, lockedEmployee.Id, targetMonth);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(target.Id, lockedEmployee.Id, targetMonth));
		var lockedUrl = $"/api/payroll/payslip-documents?employeeId={lockedEmployee.Id}&monthYear={targetMonth:yyyy-MM}";
		var lockedDocument = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(lockedUrl, json))!.Single();
		var lockedBefore = await workflow.EmployeeAsync(target.Id, lockedEmployee.Id);
		lockedBefore.IsLocked.ShouldBeTrue();

		var response = await api.PostAsJsonAsync("/api/payroll/payslip-corrections", new StartPayslipCorrectionDto {
			DocumentId = original.Id, IdempotencyKey = Guid.NewGuid(), Reason = "Synthetic mixed locked/unlocked target correction" }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var draft = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		draft.Attendance.First(x => x.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Status = EmployeeAttendanceStatusType.FullDayLeave;
		response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/calculate",
			new CalculatePayslipCorrectionDto { ConcurrencyStamp = draft.ConcurrencyStamp, Attendance = draft.Attendance }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var calculated = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/review",
			new PayslipCorrectionActionDto { ConcurrencyStamp = calculated.ConcurrencyStamp }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var reviewed = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		var request = new SettlePayslipCorrectionDto { ConcurrencyStamp = reviewed.ConcurrencyStamp,
			TargetMonth = targetMonth.ToString("yyyy-MM"), PreviewRunId = target.Id };
		var settleUrl = $"/api/payroll/payslip-corrections/{draft.Id}/settle";
		response = await api.PostAsJsonAsync(settleUrl, request, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(response);
		var applied = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		applied.Status.ShouldBe("APPLIED");
		applied.SettlementMonth.ShouldBe(request.TargetMonth);
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PostAsJsonAsync(settleUrl, request, json));
		(await workflow.StatusAsync(target.Id)).Status.ShouldBe("SUPERSEDED");
		(await workflow.StatusAsync(target.Id)).IsCurrent.ShouldBeFalse();
		(await api.PostAsJsonAsync("/api/payroll/run/reviews", new {
			month = request.TargetMonth, previewRunId = target.Id, employeeIds = new[] { correctedEmployee.Id } }, json)).IsSuccessStatusCode.ShouldBeFalse();
		(await workflow.LockAsync(target.Id, correctedEmployee.Id, targetMonth)).IsSuccessStatusCode.ShouldBeFalse();

		var regenerated = await workflow.CalculateAsync(targetMonth);
		var after = await workflow.EmployeeAsync(regenerated.Id, correctedEmployee.Id);
		Math.Round(after.NetPay - before.NetPay, 2).ShouldBe(Math.Round(calculated.Candidate!.NetPay - (double)draft.OriginalNetPay, 2));
		var lockedAfter = await workflow.EmployeeAsync(regenerated.Id, lockedEmployee.Id);
		lockedAfter.IsLocked.ShouldBeTrue();
		lockedAfter.NetPay.ShouldBe(lockedBefore.NetPay);
		lockedAfter.EmployerContributionTotal.ShouldBe(lockedBefore.EmployerContributionTotal);
		lockedAfter.EmployeeStateInsuranceCoverageDecision.ShouldBe(lockedBefore.EmployeeStateInsuranceCoverageDecision);
		await workflow.ReviewAsync(regenerated.Id, correctedEmployee.Id, targetMonth);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(regenerated.Id, correctedEmployee.Id, targetMonth));
		var correctedDocuments = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
			$"/api/payroll/payslip-documents?employeeId={correctedEmployee.Id}&monthYear={targetMonth:yyyy-MM}", json))!;
		correctedDocuments.Count.ShouldBe(1);
		var issuedTarget = (await api.GetFromJsonAsync<SalarySlipResponse>(
			$"/api/payroll/salary-slips?employeeId={correctedEmployee.Id}&monthYear={targetMonth:yyyy-MM}", json))!;
		Math.Round(Convert.ToDecimal(issuedTarget.NetSalary), 2).ShouldBe(Math.Round(Convert.ToDecimal(after.NetPay), 2));
		Math.Round(Convert.ToDecimal(issuedTarget.TotalEarning) - Convert.ToDecimal(issuedTarget.TotalDeduction), 2)
			.ShouldBe(Math.Round(Convert.ToDecimal(issuedTarget.NetSalary), 2));
		(await api.GetFromJsonAsync<List<PayslipDocumentDto>>(lockedUrl, json))!.Single().Id.ShouldBe(lockedDocument.Id);
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{lockedDocument.Id}/pdf"))).ShouldBe(lockedDocument.Sha256);
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf"))).ShouldBe(original.Sha256);
		(await api.GetFromJsonAsync<List<PayslipDocumentDto>>(sourceUrl, json))!.Single().PaymentStatus.ShouldBe("Paid");
		(await api.GetFromJsonAsync<List<PayslipCorrectionDto>>($"/api/payroll/payslip-corrections?documentId={original.Id}", json))!.Single().Id.ShouldBe(applied.Id);
		await AssertDirectCorrectionGuardsAsync(factory, original.Id, applied, request);
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf"))).ShouldBe(original.Sha256);
	}

	private static async Task AssertDirectCorrectionGuardsAsync(PayslipSettlementMixedTestFactory factory,
		Guid documentId, PayslipCorrectionDto correction, SettlePayslipCorrectionDto settlement)
	{
		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var direct = ActivatorUtilities.CreateInstance<PayslipCorrectionAppService>(services);
		direct.LazyServiceProvider = services.GetRequiredService<IAbpLazyServiceProvider>();
		direct.GetType().ShouldBe(typeof(PayslipCorrectionAppService), "these calls must bypass ABP method interceptors");
		var accessor = services.GetRequiredService<IHttpContextAccessor>();
		var previousContext = accessor.HttpContext;
		var context = new DefaultHttpContext();
		accessor.HttpContext = context;
		try
		{
			var calls = new Dictionary<string, Func<Task>> {
				["history"] = async () => { await direct.GetAsync(documentId); },
				["start"] = async () => { await direct.StartAsync(new() {
					DocumentId = documentId, IdempotencyKey = Guid.NewGuid(), Reason = "Synthetic direct-guard probe" }); },
				["calculate"] = async () => { await direct.CalculateAsync(correction.Id, new() {
					ConcurrencyStamp = correction.ConcurrencyStamp, Attendance = correction.Attendance }); },
				["review"] = async () => { await direct.ReviewAsync(correction.Id, new() { ConcurrencyStamp = correction.ConcurrencyStamp }); },
				["cancel"] = async () => { await direct.CancelAsync(correction.Id, new() { ConcurrencyStamp = correction.ConcurrencyStamp }); },
				["reissue"] = async () => { await direct.ApplyAsync(correction.Id, new() { ConcurrencyStamp = correction.ConcurrencyStamp }); },
				["settle retry"] = async () => { await direct.SettleAsync(correction.Id, settlement); },
			};
			var caller = new ClaimsPrincipal(new ClaimsIdentity(new[] {
				new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()), new Claim(AbpClaimTypes.Role, "Teacher") }, "direct-synthetic-fixture"));
			using var principal = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(caller);
			var tenant = services.GetRequiredService<ICurrentTenant>();
			using var owningTenant = tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId));
			context.Request.Headers[AlwaysGrantPermissionChecker.TestPermissionsHeader] = "__none__";
			foreach (var call in calls)
				await Should.ThrowAsync<AbpAuthorizationException>(call.Value, call.Key);
			var viewPermissions = string.Join(',',
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View,
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View);
			context.Request.Headers[AlwaysGrantPermissionChecker.TestPermissionsHeader] = viewPermissions;
			foreach (var call in calls.Where(x => x.Key != "history"))
				await Should.ThrowAsync<AbpAuthorizationException>(call.Value, "read-only " + call.Key);
			context.Request.Headers[AlwaysGrantPermissionChecker.TestPermissionsHeader] = viewPermissions + "," +
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add;
			foreach (var call in calls.Where(x => x.Key != "history"))
				await Should.ThrowAsync<AbpAuthorizationException>(call.Value, "missing adjustments " + call.Key);
			context.Request.Headers[AlwaysGrantPermissionChecker.TestPermissionsHeader] += "," +
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Adjustments;
			foreach (var call in calls.Where(x => x.Key is "reissue" or "settle retry"))
				await Should.ThrowAsync<AbpAuthorizationException>(call.Value, "missing payroll lock " + call.Key);
			context.Request.Headers.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
			foreach (var call in calls)
			{
				using var transaction = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
				var denied = await Should.ThrowAsync<BusinessException>(call.Value, call.Key);
				denied.Code.ShouldBe(HRErrorCodes.TakeAttendanceMediumStageOutOfScope, call.Key);
			}
			using var otherTenant = tenant.Change(Guid.NewGuid());
			foreach (var call in calls)
			{
				using var transaction = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
				await Should.ThrowAsync<EntityNotFoundException>(call.Value, call.Key);
			}
		}
		finally { accessor.HttpContext = previousContext; }
	}
}
