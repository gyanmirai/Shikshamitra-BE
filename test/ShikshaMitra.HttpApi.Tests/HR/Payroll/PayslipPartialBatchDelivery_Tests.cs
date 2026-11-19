using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipPartialBatchDeliveryFactAttribute : FactAttribute
{
	public PayslipPartialBatchDeliveryFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_PARTIAL_DELIVERY_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_PARTIAL_DELIVERY_TEST_POSTGRES to a fresh disposable loopback payroll_tests_* database.";
	}
}

public sealed class PayslipPartialBatchDeliveryTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_PARTIAL_DELIVERY_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipPartialBatchDelivery_Tests
{
	[PayslipPartialBatchDeliveryFact]
	public async Task Partial_lock_queues_only_issued_employees_and_disabling_auto_email_affects_subsequent_issuance()
	{
		using var factory = new PayslipPartialBatchDeliveryTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		var settingsUrl = "/api/payroll/payslip-settings";
		var defaults = (await api.GetFromJsonAsync<PayslipSettingsDto>(settingsUrl, json))!;
		defaults.AutomaticEmail.ShouldBeFalse();
		var repeatedRead = (await api.GetFromJsonAsync<PayslipSettingsDto>(settingsUrl, json))!;
		repeatedRead.AutomaticEmail.ShouldBeFalse();
		repeatedRead.ConcurrencyStamp.ShouldBe(defaults.ConcurrencyStamp);
		defaults.AutomaticEmail = true;
		defaults.IdempotencyKey = Guid.NewGuid();
		var savedResponse = await api.PutAsJsonAsync(settingsUrl, defaults, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(savedResponse);
		var saved = (await savedResponse.Content.ReadFromJsonAsync<PayslipSettingsDto>(json))!;
		var duplicateSave = await api.PutAsJsonAsync(settingsUrl, defaults, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(duplicateSave);
		(await duplicateSave.Content.ReadFromJsonAsync<PayslipSettingsDto>(json))!.ConcurrencyStamp.ShouldBe(saved.ConcurrencyStamp);
		(await api.GetFromJsonAsync<PayslipSettingsDto>(settingsUrl, json))!.AutomaticEmail.ShouldBeTrue();

		var now = DateTime.UtcNow;
		var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var first = await EmployeeTestHelper.CreateEmployeeAsync(api, "PartialDeliveryFirst", dateOfJoining: month);
		var failed = await EmployeeTestHelper.CreateEmployeeAsync(api, "PartialDeliveryInvalid", dateOfJoining: month);
		var last = await EmployeeTestHelper.CreateEmployeeAsync(api, "PartialDeliveryLast", dateOfJoining: month);
		await workflow.ConfigureAsync(true);
		var employees = new[] { first, failed, last };
		foreach (var employee in employees)
		{
			await workflow.EnrollAsync(employee.Id, "1234567890");
			await workflow.SaveCompensationAsync(employee.Id, month, "GROSS", 10000m);
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
			await workflow.SeedAttendanceAsync(employee.Id, month);
		}
		var run = await workflow.CalculateAsync(month);
		foreach (var employee in employees) await workflow.ReviewAsync(run.Id, employee.Id, month);
		// Inject a pre-write component-integrity fault in the disposable draft, never in an issued artifact.
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			await scope.ServiceProvider.GetRequiredService<IPayrollInputRevisionStore>().LockAsync();
			var snapshots = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
			var snapshot = await snapshots.GetAsync(x => x.EmployeeId == failed.Id && x.MonthYear == month.ToString("yyyy-MM"));
			snapshot.IsLocked.ShouldBeFalse();
			snapshot.ReviewStatus.ShouldBe("REVIEWED");
			snapshot.EarningComponentsJson = "[]";
			await snapshots.UpdateAsync(snapshot);
			await unit.CompleteAsync();
		}
		var lockResponse = await api.PostAsJsonAsync("/api/payroll/run", new
		{
			month = month.ToString("yyyy-MM"),
			previewRunId = run.Id,
			academicYearId = run.AcademicYearId,
			employeeIds = employees.Select(x => x.Id).ToArray()
		}, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(lockResponse);
		var result = (await lockResponse.Content.ReadFromJsonAsync<PayrollLockResponse>(json))!;
		result.EmployeesProcessed.ShouldBe(2);
		result.EmployeesFailed.ShouldBe(1);
		result.EmployeesSkipped.ShouldBe(0);
		factory.Messages.ShouldBeEmpty();
		var issued = new List<PayslipDocumentDto>();
		var attempts = new List<PayslipDeliveryDto>();
		foreach (var employee in new[] { first, last })
		{
			var document = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
				$"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={month:yyyy-MM}", json))!.Single();
			document.IsCurrent.ShouldBeTrue();
			document.PaymentStatus.ShouldBe("Finalized");
			var attempt = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(
				$"/api/payroll/payslip-deliveries?salarySlipId={document.SalarySlipId}", json))!.Single();
			attempt.DocumentId.ShouldBe(document.Id);
			attempt.Automatic.ShouldBeTrue();
			attempt.Status.ShouldBe("QUEUED");
			issued.Add(document);
			attempts.Add(attempt);
		}
		(await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
			$"/api/payroll/payslip-documents?employeeId={failed.Id}&monthYear={month:yyyy-MM}", json))!.ShouldBeEmpty();
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			(await scope.ServiceProvider.GetRequiredService<IRepository<PayslipDelivery, Guid>>()
				.CountAsync(x => x.EmployeeId == failed.Id)).ShouldBe(0);
			(await scope.ServiceProvider.GetRequiredService<ISalarySlipRepository>()
				.CountAsync(x => x.EmployeeId == failed.Id)).ShouldBe(0);
			var snapshots = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
			foreach (var employee in employees)
				(await snapshots.GetAsync(x => x.EmployeeId == employee.Id && x.MonthYear == month.ToString("yyyy-MM")))
					.IsLocked.ShouldBe(employee.Id != failed.Id);
			await unit.CompleteAsync();
		}

		var off = (await api.GetFromJsonAsync<PayslipSettingsDto>(settingsUrl, json))!;
		off.AutomaticEmail = false;
		off.IdempotencyKey = Guid.NewGuid();
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync(settingsUrl, off, json));
		async Task DeliverAsync(Guid id)
		{
			using var scope = factory.Services.CreateScope();
			await scope.ServiceProvider.GetRequiredService<PayslipDeliveryBackgroundJob>().ExecuteAsync(new()
			{
				TenantId = Guid.Parse(TestAuthHandler.TestTenantId),
				DeliveryId = id
			});
		}
		await Task.WhenAll(attempts.Select(x => DeliverAsync(x.Id)));
		foreach (var attempt in attempts) await DeliverAsync(attempt.Id);
		factory.Messages.Count.ShouldBe(2);
		foreach (var document in issued)
		{
			var captured = factory.Messages.Single(x => x.Recipient == document.RecipientEmail);
			Convert.ToHexString(SHA256.HashData(captured.Pdf)).ShouldBe(document.Sha256);
			Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{document.Id}/pdf")))
				.ShouldBe(document.Sha256);
		}
		var repeatedLock = await api.PostAsJsonAsync("/api/payroll/run", new
		{
			month = month.ToString("yyyy-MM"),
			previewRunId = run.Id,
			academicYearId = run.AcademicYearId,
			employeeIds = employees.Select(x => x.Id).ToArray()
		}, json);
		repeatedLock.IsSuccessStatusCode.ShouldBeFalse();

		// Regeneration restores the failed draft through the pipeline; new issuance respects auto-off.
		var recalculated = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(recalculated.Id, failed.Id, month);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(recalculated.Id, failed.Id, month));
		var recovered = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
			$"/api/payroll/payslip-documents?employeeId={failed.Id}&monthYear={month:yyyy-MM}", json))!.Single();
		recovered.IsCurrent.ShouldBeTrue();
		recovered.PaymentStatus.ShouldBe("Finalized");
		(await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(
			$"/api/payroll/payslip-deliveries?salarySlipId={recovered.SalarySlipId}", json))!.ShouldBeEmpty();
		factory.Messages.Count.ShouldBe(2);
		foreach (var document in issued)
		{
			var history = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(
				$"/api/payroll/payslip-deliveries?salarySlipId={document.SalarySlipId}", json))!;
			history.Count.ShouldBe(1);
			history.Single().Status.ShouldBe("PROVIDER_ACCEPTED");
			Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{document.Id}/pdf")))
				.ShouldBe(document.Sha256);
		}
	}
}