using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using NSubstitute;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Volo.Abp.Emailing;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipDeliveryCurrencyFactAttribute : FactAttribute
{
	public PayslipDeliveryCurrencyFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_CURRENCY_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_CURRENCY_TEST_POSTGRES to a fresh owned loopback payroll_tests_* database.";
	}
}

public sealed class PayslipDeliveryCurrencyTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_CURRENCY_TEST_POSTGRES";
	private readonly IEmailSender _sender = Substitute.For<IEmailSender>();
	private bool _blockProvider;
	private TaskCompletionSource _providerReached = Signal();
	private TaskCompletionSource _providerRelease = Signal();
	public Task ProviderReached => _providerReached.Task;
	public void BlockNextProviderCall()
	{
		_providerReached = Signal();
		_providerRelease = Signal();
		_blockProvider = true;
	}
	public void ReleaseProvider() => _providerRelease.TrySetResult();
	private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		_sender.SendAsync(Arg.Any<MailMessage>(), Arg.Any<bool>()).Returns(call => CaptureAsync(call.Arg<MailMessage>()));
		builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton(_sender)));
	}
	private async Task CaptureAsync(MailMessage message)
	{
		if (message.To.Count != 1 || message.CC.Count != 0 || message.Bcc.Count != 0 || message.Attachments.Count != 1)
			throw new InvalidOperationException("A payslip email must contain one recipient and one attachment.");
		if (_blockProvider)
		{
			_blockProvider = false;
			_providerReached.TrySetResult();
			await _providerRelease.Task;
		}
		using var output = new MemoryStream();
		await message.Attachments[0].ContentStream.CopyToAsync(output);
		Messages.Enqueue(new CapturedPayslipEmail(message.To[0].Address,
			message.Attachments[0].Name ?? throw new InvalidOperationException("Attachment filename is required."), output.ToArray()));
	}
}

[Collection(ApiTestCollection.Name)]
public class PayslipDeliveryCurrency_Tests
{
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	[PayslipDeliveryCurrencyFact]
	public async Task Unpaid_reissue_supersedes_queued_and_claimed_documents_and_serializes_with_provider_acceptance()
	{
		using var factory = new PayslipDeliveryCurrencyTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		await workflow.ConfigureAsync(true);
		var settings = (await api.GetFromJsonAsync<PayslipSettingsDto>("/api/payroll/payslip-settings", Json))!;
		settings.AutomaticEmail = true;
		settings.IdempotencyKey = Guid.NewGuid();
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync("/api/payroll/payslip-settings", settings, Json));
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		foreach (var ordering in new[] { "queued", "claimed", "provider-first" })
		{
			var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipCurrency_" + ordering, dateOfJoining: month);
			await workflow.EnrollAsync(employee.Id, "1234567890");
			await workflow.SaveCompensationAsync(employee.Id, month, "GROSS", 10000m);
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
			await workflow.SeedAttendanceAsync(employee.Id, month);
			var run = await workflow.CalculateAsync(month);
			await workflow.ReviewAsync(run.Id, employee.Id, month);
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(run.Id, employee.Id, month));
			var documentsUrl = $"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={month:yyyy-MM}";
			var original = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, Json))!.Single();
			var historyUrl = $"/api/payroll/payslip-deliveries?salarySlipId={original.SalarySlipId}";
			var oldAttempt = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, Json))!.Single();
			oldAttempt.DocumentId.ShouldBe(original.Id);
			oldAttempt.Status.ShouldBe("QUEUED");
			oldAttempt.Automatic.ShouldBeTrue();
			var before = factory.Messages.Count;
			var response = await api.PostAsJsonAsync("/api/payroll/payslip-corrections", new StartPayslipCorrectionDto
			{
				DocumentId = original.Id,
				IdempotencyKey = Guid.NewGuid(),
				Reason = "Correct synthetic absence before delivery: " + ordering
			}, Json);
			await EsiPayrollWorkflowScenario.RequireSuccess(response);
			var draft = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(Json))!;
			draft.Attendance.First(day => day.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Status = EmployeeAttendanceStatusType.FullDayLeave;
			response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/calculate",
				new CalculatePayslipCorrectionDto { ConcurrencyStamp = draft.ConcurrencyStamp, Attendance = draft.Attendance }, Json);
			await EsiPayrollWorkflowScenario.RequireSuccess(response);
			var calculated = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(Json))!;
			calculated.Candidate!.NetPay.ShouldBeLessThan((double) draft.OriginalNetPay);
			response = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/review",
				new PayslipCorrectionActionDto { ConcurrencyStamp = calculated.ConcurrencyStamp }, Json);
			await EsiPayrollWorkflowScenario.RequireSuccess(response);
			var reviewed = (await response.Content.ReadFromJsonAsync<PayslipCorrectionDto>(Json))!;
			async Task<PayslipCorrectionDto> ReissueAsync()
			{
				var applied = await api.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/apply",
					new PayslipCorrectionActionDto { ConcurrencyStamp = reviewed.ConcurrencyStamp }, Json);
				await EsiPayrollWorkflowScenario.RequireSuccess(applied);
				return (await applied.Content.ReadFromJsonAsync<PayslipCorrectionDto>(Json))!;
			}
			PayslipCorrectionDto completed;
			if (ordering == "queued")
			{
				completed = await ReissueAsync();
				await ExecuteAsync(factory, oldAttempt.Id);
			}
			else if (ordering == "claimed")
			{
				PayslipCorrectionDto? afterClaim = null;
				await ExecuteAsync(factory, oldAttempt.Id, async () =>
				{
					(await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, Json))!.Single(x => x.Id == oldAttempt.Id).Status.ShouldBe("PROCESSING");
					factory.Messages.Count.ShouldBe(before);
					afterClaim = await ReissueAsync();
				});
				completed = afterClaim.ShouldNotBeNull();
			}
			else
			{
				factory.BlockNextProviderCall();
				var sending = ExecuteAsync(factory, oldAttempt.Id);
				Task<PayslipCorrectionDto>? reissue = null;
				try
				{
					await factory.ProviderReached.WaitAsync(TimeSpan.FromSeconds(25));
					factory.Messages.Count.ShouldBe(before);
					factory.Barrier.Observe("INSERT INTO \"HR_PayrollInputRevisions\"");
					reissue = ReissueAsync();
					await factory.Barrier.Observed.WaitAsync(TimeSpan.FromSeconds(25));
					await WaitForBlockedMutationAsync();
					reissue.IsCompleted.ShouldBeFalse();
					(await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, Json))!.Single(x => x.IsCurrent).Id.ShouldBe(original.Id);
				}
				finally { factory.ReleaseProvider(); }
				await sending;
				completed = await reissue.ShouldNotBeNull();
				var acceptedOriginal = factory.Messages.Skip(before).Single();
				acceptedOriginal.Recipient.ShouldBe(original.RecipientEmail);
				Convert.ToHexString(SHA256.HashData(acceptedOriginal.Pdf)).ShouldBe(original.Sha256);
			}
			completed.Status.ShouldBe("APPLIED");
			var versions = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, Json))!;
			versions.Count.ShouldBe(2);
			var replacement = versions.Single(x => x.IsCurrent);
			replacement.Id.ShouldBe(completed.ResultDocumentId!.Value);
			replacement.Revision.ShouldBe(2);
			replacement.ReplacesDocumentId.ShouldBe(original.Id);
			replacement.Sha256.ShouldNotBe(original.Sha256);
			var history = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, Json))!;
			history.Count.ShouldBe(2);
			var endedOriginal = history.Single(x => x.Id == oldAttempt.Id);
			endedOriginal.Status.ShouldBe(ordering == "provider-first" ? "PROVIDER_ACCEPTED" : "SUPERSEDED");
			if (ordering != "provider-first")
			{
				endedOriginal.ErrorCategory.ShouldBe("DOCUMENT_REPLACED");
				factory.Messages.Count.ShouldBe(before);
			}
			var current = history.Single(x => x.DocumentId == replacement.Id);
			current.Automatic.ShouldBeTrue();
			current.Status.ShouldBe("QUEUED");
			await Task.WhenAll(ExecuteAsync(factory, current.Id), ExecuteAsync(factory, current.Id));
			await ExecuteAsync(factory, current.Id);
			await ExecuteAsync(factory, oldAttempt.Id);
			var expected = ordering == "provider-first" ? 2 : 1;
			factory.Messages.Count.ShouldBe(before + expected);
			var captured = factory.Messages.Last();
			captured.Recipient.ShouldBe(original.RecipientEmail);
			captured.FileName.ShouldBe($"Payslip_{month:yyyy-MM}_R2.pdf");
			Convert.ToHexString(SHA256.HashData(captured.Pdf)).ShouldBe(replacement.Sha256);
			foreach (var document in versions)
				Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{document.Id}/pdf"))).ShouldBe(document.Sha256);
			(await api.GetFromJsonAsync<SalarySlipResponse>($"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={month:yyyy-MM}", Json))!
				.NetSalary.ShouldBe(calculated.Candidate.NetPay);
		}
		factory.Messages.Count.ShouldBe(4);
		await VerifyControlledAccountEmailDeliveryAsync(factory, workflow, month);
	}

	private static async Task VerifyControlledAccountEmailDeliveryAsync(PayslipDeliveryCurrencyTestFactory factory,
		EsiPayrollWorkflowScenario workflow, DateTime month)
	{
		var api = workflow.Api;
		var initialMessages = factory.Messages.Count;
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "AccountEmailDelivery", dateOfJoining: month);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, month, "GROSS", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, month);
		var run = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(run.Id, employee.Id, month);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(run.Id, employee.Id, month));
		var documentsUrl = $"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={month:yyyy-MM}";
		var original = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, Json))!.Single();
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PostAsJsonAsync("/api/payroll/payslip-documents/payment",
			new RecordPayslipPaymentDto
			{
				DocumentId = original.Id,
				IdempotencyKey = Guid.NewGuid(),
				PaidAt = DateTimeOffset.UtcNow,
				Reference = "Synthetic recipient-race payment"
			}, Json));
		var accountUrl = $"/api/employees/{employee.Id}/account-email";
		var historyUrl = $"/api/payroll/payslip-deliveries?salarySlipId={original.SalarySlipId}";
		var salaryUrl = $"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={month:yyyy-MM}";
		var originalSalary = (await api.GetFromJsonAsync<SalarySlipResponse>(salaryUrl, Json))!;
		foreach (var ordering in new[] { "queued", "claimed", "provider-first" })
		{
			var before = factory.Messages.Count;
			var account = (await api.GetFromJsonAsync<EmployeeAccountEmailDto>(accountUrl, Json))!;
			var input = new ChangeEmployeeAccountEmailDto
			{
				Email = "recipient-" + ordering + "-" + Guid.NewGuid().ToString("N") + "@test.shikshamitra.local",
				Reason = "HR verified the synthetic recipient before " + ordering,
				ConfirmEmployeeVerified = true,
				ConcurrencyToken = account.ConcurrencyToken,
				IdempotencyKey = Guid.NewGuid()
			};
			var request = new SendPayslipDto { DocumentId = original.Id, IdempotencyKey = Guid.NewGuid() };
			PayslipDeliveryDto attempt;
			if (ordering == "queued")
				attempt = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, Json))!.Single();
			else
			{
				var queued = await api.PostAsJsonAsync("/api/payroll/payslip-deliveries", request, Json);
				await EsiPayrollWorkflowScenario.RequireSuccess(queued);
				attempt = (await queued.Content.ReadFromJsonAsync<PayslipDeliveryDto>(Json))!;
			}
			attempt.Status.ShouldBe("QUEUED");
			async Task ChangeAsync() => await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync(accountUrl, input, Json));
			if (ordering == "queued")
			{
				await ChangeAsync();
				await ExecuteAsync(factory, attempt.Id);
			}
			else if (ordering == "claimed")
			{
				await ExecuteAsync(factory, attempt.Id, async () =>
				{
					var claimed = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, Json))!.Single(x => x.Id == attempt.Id);
					claimed.Status.ShouldBe("PROCESSING");
					claimed.Recipient.ShouldBe(account.AccountEmail);
					await ChangeAsync();
				});
				factory.Messages.Count.ShouldBe(before);
				var failed = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, Json))!.Single(x => x.Id == attempt.Id);
				failed.Status.ShouldBe("FAILED");
				failed.ErrorCategory.ShouldBe("RECIPIENT_CHANGED");
				var replay = await api.PostAsJsonAsync("/api/payroll/payslip-deliveries", request, Json);
				await EsiPayrollWorkflowScenario.RequireSuccess(replay);
				(await replay.Content.ReadFromJsonAsync<PayslipDeliveryDto>(Json))!.Id.ShouldBe(attempt.Id);
				await ExecuteAsync(factory, attempt.Id);
				factory.Messages.Count.ShouldBe(before);
			}
			else
			{
				factory.BlockNextProviderCall();
				var sending = ExecuteAsync(factory, attempt.Id);
				Task? changing = null;
				try
				{
					await factory.ProviderReached.WaitAsync(TimeSpan.FromSeconds(25));
					factory.Barrier.Observe("INSERT INTO \"HR_PayrollInputRevisions\"");
					changing = ChangeAsync();
					await factory.Barrier.Observed.WaitAsync(TimeSpan.FromSeconds(25));
					await WaitForBlockedMutationAsync();
					changing.IsCompleted.ShouldBeFalse();
					(await api.GetFromJsonAsync<EmployeeAccountEmailDto>(accountUrl, Json))!.AccountEmail.ShouldBe(account.AccountEmail);
				}
				finally { factory.ReleaseProvider(); }
				await sending;
				await changing.ShouldNotBeNull();
				var accepted = factory.Messages.Skip(before).Single();
				accepted.Recipient.ShouldBe(account.AccountEmail);
				Convert.ToHexString(SHA256.HashData(accepted.Pdf)).ShouldBe(original.Sha256);
			}
			if (ordering != "claimed")
			{
				var accepted = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, Json))!.Single(x => x.Id == attempt.Id);
				accepted.Status.ShouldBe("PROVIDER_ACCEPTED");
				accepted.Recipient.ShouldBe(ordering == "queued" ? input.Email : account.AccountEmail);
				await ExecuteAsync(factory, attempt.Id);
				factory.Messages.Count.ShouldBe(before + 1);
			}
			if (ordering != "queued")
			{
				var retry = await api.PostAsJsonAsync("/api/payroll/payslip-deliveries",
					new SendPayslipDto { DocumentId = original.Id, IdempotencyKey = Guid.NewGuid() }, Json);
				await EsiPayrollWorkflowScenario.RequireSuccess(retry);
				var current = (await retry.Content.ReadFromJsonAsync<PayslipDeliveryDto>(Json))!;
				await Task.WhenAll(ExecuteAsync(factory, current.Id), ExecuteAsync(factory, current.Id));
				await ExecuteAsync(factory, current.Id);
			}
			factory.Messages.Count.ShouldBe(before + (ordering == "provider-first" ? 2 : 1));
			factory.Messages.Last().Recipient.ShouldBe(input.Email);
			Convert.ToHexString(SHA256.HashData(factory.Messages.Last().Pdf)).ShouldBe(original.Sha256);
			var updated = (await api.GetFromJsonAsync<EmployeeAccountEmailDto>(accountUrl, Json))!;
			updated.EmployeeEmail.ShouldBe(input.Email);
			updated.AccountEmail.ShouldBe(input.Email);
			var preserved = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, Json))!.Single();
			preserved.Id.ShouldBe(original.Id);
			preserved.Revision.ShouldBe(1);
			preserved.PaymentStatus.ShouldBe("Paid");
			preserved.Sha256.ShouldBe(original.Sha256);
			Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{original.Id}/pdf"))).ShouldBe(original.Sha256);
			(await api.GetFromJsonAsync<SalarySlipResponse>(salaryUrl, Json))!.NetSalary.ShouldBe(originalSalary.NetSalary);
		}
		factory.Messages.Count.ShouldBe(initialMessages + 4);
	}

	private static async Task ExecuteAsync(PayslipPostgresTestFactory factory, Guid delivery, Func<Task>? beforeSend = null)
	{
		using var scope = factory.Services.CreateScope();
		var fence = scope.ServiceProvider.GetRequiredService<IPayrollInputRevisionStore>();
		if (beforeSend != null) fence = new SendPhaseFence(fence, beforeSend);
		var worker = ActivatorUtilities.CreateInstance<PayslipDeliveryBackgroundJob>(scope.ServiceProvider, fence);
		await worker.ExecuteAsync(new() { TenantId = EsiPayrollWorkflowScenario.Tenant, DeliveryId = delivery });
	}

	private sealed class SendPhaseFence(IPayrollInputRevisionStore inner, Func<Task> beforeSend) : IPayrollInputRevisionStore
	{
		private int _locks;
		public Task<Guid> ReadAsync() => inner.ReadAsync();
		public async Task<Guid> LockAsync()
		{
			if (++_locks == 2) await beforeSend();
			return await inner.LockAsync();
		}
	}

	private static async Task WaitForBlockedMutationAsync()
	{
		var options = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_CURRENCY_TEST_POSTGRES"));
		if (options.Host is not ("127.0.0.1" or "localhost" or "::1") || options.Database?.StartsWith("payroll_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("Only inspect the owned loopback fixture.");
		await using var connection = new NpgsqlConnection(options.ConnectionString);
		await connection.OpenAsync();
		await using var command = new NpgsqlCommand("""
			SELECT COUNT(*)::int FROM pg_stat_activity
			WHERE datname = current_database() AND wait_event_type = 'Lock'
			AND query LIKE '%HR_PayrollInputRevisions%'
			AND cardinality(pg_blocking_pids(pid)) > 0
			""", connection);
		var watch = Stopwatch.StartNew();
		while (watch.Elapsed < TimeSpan.FromSeconds(10))
		{
			if ((int) (await command.ExecuteScalarAsync())! > 0) return;
			await Task.Delay(25);
		}
		throw new InvalidOperationException("The mutation did not become blocked on the provider's real PostgreSQL fence.");
	}
}