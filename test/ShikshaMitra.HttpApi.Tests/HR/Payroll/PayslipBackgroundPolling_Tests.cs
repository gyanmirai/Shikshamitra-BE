using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipBackgroundPollingFactAttribute : FactAttribute
{
	public PayslipBackgroundPollingFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_POLLER_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_POLLER_TEST_POSTGRES to a fresh owned loopback payroll_tests_* database.";
	}
}

public sealed class PayslipBackgroundPollingTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_POLLER_TEST_POSTGRES";

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		// Keep unrelated periodic workers disabled by the shared fixture.
		// Start only the real framework job poller explicitly after commit assertions.
		builder.ConfigureTestServices(services => services.PostConfigure<AbpBackgroundJobOptions>(
			options => options.IsJobExecutionEnabled = true));
	}
}

[Collection(ApiTestCollection.Name)]
public class PayslipBackgroundPolling_Tests
{
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	[PayslipBackgroundPollingFact]
	public async Task Framework_poller_sends_committed_intent_and_never_sends_rolled_back_lock()
	{
		using var factory = new PayslipBackgroundPollingTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		await workflow.ConfigureAsync(true);
		var settings = (await api.GetFromJsonAsync<PayslipSettingsDto>("/api/payroll/payslip-settings", Json))!;
		settings.AutomaticEmail = true;
		settings.IdempotencyKey = Guid.NewGuid();
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync("/api/payroll/payslip-settings", settings, Json));
		var now = DateTime.UtcNow;
		var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipPoller", dateOfJoining: month);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, month, "GROSS", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, month);
		var first = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(first.Id, employee.Id, month);
		var documentsUrl = $"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={month:yyyy-MM}";
		using var scope = factory.Services.CreateScope();
		var store = scope.ServiceProvider.GetRequiredService<IBackgroundJobStore>();
		var precedingJobIds = (await store.GetWaitingJobsAsync(1000)).Select(job => job.Id).OrderBy(id => id).ToArray();
		factory.Barrier.FailNextCommand("INSERT INTO \"AbpBackgroundJobs\"");
		(await workflow.LockAsync(first.Id, employee.Id, month)).IsSuccessStatusCode.ShouldBeFalse();
		(await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, Json))!.ShouldBeEmpty();
		var afterRollback = await store.GetWaitingJobsAsync(1000);
		afterRollback.Select(job => job.Id).OrderBy(id => id).ToArray().ShouldBe(precedingJobIds,
			"Rolled-back locking must not add any background job to the existing preview queue.");
		afterRollback.ShouldNotContain(job => job.JobName == typeof(PayslipDeliveryJobArgs).FullName,
			"Rolled-back delivery intent must not escape through the framework job store.");
		factory.Messages.ShouldBeEmpty();

		var fresh = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(fresh.Id, employee.Id, month);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(fresh.Id, employee.Id, month));
		var document = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, Json))!.Single();
		var historyUrl = $"/api/payroll/payslip-deliveries?salarySlipId={document.SalarySlipId}";
		var attempt = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, Json))!.Single();
		attempt.Status.ShouldBe("QUEUED");
		factory.Messages.ShouldBeEmpty("Queuing intent must not send synchronously inside the payroll transaction.");
		var queuedJob = (await store.GetWaitingJobsAsync(1000)).Single(job =>
			job.JobArgs.Contains(attempt.Id.ToString(), StringComparison.OrdinalIgnoreCase));
		var persistedArgs = JsonSerializer.Deserialize<PayslipDeliveryJobArgs>(queuedJob.JobArgs, Json)!;
		persistedArgs.DeliveryId.ShouldBe(attempt.Id);
		persistedArgs.TenantId.ShouldBe(EsiPayrollWorkflowScenario.Tenant);

		var poller = scope.ServiceProvider.GetRequiredService<IBackgroundJobWorker>();
		await poller.StartAsync();
		try
		{
			var deadline = DateTime.UtcNow.AddSeconds(45);
			PayslipDeliveryDto current;
			do
			{
				await Task.Delay(100);
				current = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, Json))!.Single();
			} while ((current.Status is "QUEUED" or "PROCESSING") && DateTime.UtcNow < deadline);
			current.Status.ShouldBe("PROVIDER_ACCEPTED");
			var captured = factory.Messages.Single();
			captured.Recipient.ShouldBe(document.RecipientEmail);
			Convert.ToHexString(SHA256.HashData(captured.Pdf)).ShouldBe(document.Sha256);
			Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{document.Id}/pdf")))
				.ShouldBe(document.Sha256);
			deadline = DateTime.UtcNow.AddSeconds(10);
			while ((await store.GetWaitingJobsAsync(1000)).Any(job => job.Id == queuedJob.Id) && DateTime.UtcNow < deadline)
				await Task.Delay(100);
			(await store.GetWaitingJobsAsync(1000)).ShouldNotContain(job => job.Id == queuedJob.Id);
			factory.Messages.Count.ShouldBe(1);
		}
		finally
		{
			await poller.StopAsync();
		}
	}
}