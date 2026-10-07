using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipAutomaticDeliveryPermissionFactAttribute : FactAttribute
{
	public PayslipAutomaticDeliveryPermissionFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_LOCK_PERMISSION_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_LOCK_PERMISSION_TEST_POSTGRES to a fresh disposable loopback payroll_tests_* database.";
	}
}

public sealed class PayslipAutomaticDeliveryPermissionTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_LOCK_PERMISSION_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipAutomaticDeliveryPermission_Tests
{
	[PayslipAutomaticDeliveryPermissionFact]
	public async Task Payroll_lock_automatic_delivery_rechecks_lock_permission_without_requiring_calculation()
	{
		using var factory = new PayslipAutomaticDeliveryPermissionTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		var settings = (await api.GetFromJsonAsync<PayslipSettingsDto>("/api/payroll/payslip-settings", json))!;
		settings.AutomaticEmail = true;
		settings.IdempotencyKey = Guid.NewGuid();
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync("/api/payroll/payslip-settings", settings, json));
		var now = DateTime.UtcNow;
		var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var first = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipLockPermission", dateOfJoining: month);
		var revoked = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipRevokedLock", dateOfJoining: month);
		await workflow.ConfigureAsync(true);
		foreach (var employee in new[] { first, revoked })
		{
			await workflow.EnrollAsync(employee.Id, "1234567890");
			await workflow.SaveCompensationAsync(employee.Id, month, "GROSS", 10000m);
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
			await workflow.SeedAttendanceAsync(employee.Id, month);
		}
		var run = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(run.Id, first.Id, month);
		await workflow.ReviewAsync(run.Id, revoked.Id, month);
		var lockPermissions = new[] {
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View,
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Lock };
		using var approver = factory.CreateClient();
		approver.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(',', lockPermissions));
		await EsiPayrollWorkflowScenario.RequireSuccess(await approver.PostAsJsonAsync("/api/payroll/run", new
		{
			month = month.ToString("yyyy-MM"),
			previewRunId = run.Id,
			academicYearId = run.AcademicYearId,
			employeeIds = new[] { first.Id, revoked.Id }
		}, json));

		var firstDocument = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
			$"/api/payroll/payslip-documents?employeeId={first.Id}&monthYear={month:yyyy-MM}", json))!.Single();
		var revokedDocument = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
			$"/api/payroll/payslip-documents?employeeId={revoked.Id}&monthYear={month:yyyy-MM}", json))!.Single();
		var firstHistory = $"/api/payroll/payslip-deliveries?salarySlipId={firstDocument.SalarySlipId}";
		var revokedHistory = $"/api/payroll/payslip-deliveries?salarySlipId={revokedDocument.SalarySlipId}";
		var firstAttempt = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(firstHistory, json))!.Single();
		var revokedAttempt = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(revokedHistory, json))!.Single();
		firstAttempt.Automatic.ShouldBeTrue();
		revokedAttempt.Automatic.ShouldBeTrue();
		factory.Messages.ShouldBeEmpty();

		await ExecuteWithPermissionsAsync(factory, firstAttempt.Id, lockPermissions);
		await ExecuteWithPermissionsAsync(factory, revokedAttempt.Id, [
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View,
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Add ]);
		var accepted = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(firstHistory, json))!.Single();
		var denied = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(revokedHistory, json))!.Single();
		accepted.ShouldSatisfyAllConditions(
			() => accepted.Status.ShouldBe("PROVIDER_ACCEPTED"),
			() => denied.Status.ShouldBe("FAILED"),
			() => denied.ErrorCategory.ShouldBe("ACCESS_REVOKED"),
			() => factory.Messages.Single().Recipient.ShouldBe(firstDocument.RecipientEmail),
			() => Convert.ToHexString(SHA256.HashData(factory.Messages.Single().Pdf)).ShouldBe(firstDocument.Sha256));
		factory.Messages.Count.ShouldBe(1);
		await ExecuteWithPermissionsAsync(factory, firstAttempt.Id, lockPermissions);
		factory.Messages.Count.ShouldBe(1);
		var manualResponse = await api.PostAsJsonAsync("/api/payroll/payslip-deliveries",
			new SendPayslipDto { DocumentId = firstDocument.Id, IdempotencyKey = Guid.NewGuid() }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(manualResponse);
		var manual = (await manualResponse.Content.ReadFromJsonAsync<PayslipDeliveryDto>(json))!;
		manual.Automatic.ShouldBeFalse();
		await ExecuteWithPermissionsAsync(factory, manual.Id, [
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add ]);
		factory.Messages.Count.ShouldBe(2);
		factory.Messages.Last().Recipient.ShouldBe(firstDocument.RecipientEmail);
		Convert.ToHexString(SHA256.HashData(factory.Messages.Last().Pdf)).ShouldBe(firstDocument.Sha256);
	}

	private static async Task ExecuteWithPermissionsAsync(PayslipPostgresTestFactory factory, Guid deliveryId,
		string[] permissions)
	{
		using var scope = factory.Services.CreateScope();
		var contexts = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
		var previous = contexts.HttpContext;
		var context = new DefaultHttpContext();
		// The established test checker models the actor's current grants through this header.
		// Production IsAuthorizedAsync still reloads the persisted actor and employee scope.
		context.Request.Headers[AlwaysGrantPermissionChecker.TestPermissionsHeader] = string.Join(',', permissions);
		contexts.HttpContext = context;
		try
		{
			await scope.ServiceProvider.GetRequiredService<PayslipDeliveryBackgroundJob>().ExecuteAsync(
				new PayslipDeliveryJobArgs { TenantId = EsiPayrollWorkflowScenario.Tenant, DeliveryId = deliveryId });
		}
		finally { contexts.HttpContext = previous; }
	}
}