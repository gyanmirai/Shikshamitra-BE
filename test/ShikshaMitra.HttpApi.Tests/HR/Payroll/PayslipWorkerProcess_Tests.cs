using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using NSubstitute;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Emailing;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayslipWorkerProcessFactAttribute : FactAttribute
{
	public PayslipWorkerProcessFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PROCESS_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PROCESS_TEST_POSTGRES to a fresh disposable loopback payroll_tests_* database.";
	}
}
public class PayslipWorkerChildFactAttribute : FactAttribute
{
	public PayslipWorkerChildFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYSLIP_WORKER_CHILD_ID")))
			Skip = "Invoked only by the worker process restart regression.";
	}
}
public class PayslipWorkerProcessSeedFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PROCESS_TEST_POSTGRES";
}
// A worker restart must attach to persisted state without recreating tables or reseeding grants.
public class PayslipWorkerAttachFactory : PayslipPostgresTestFactory
{
	protected override bool InitializeTestData => false;
	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PAYROLL_PROCESS_TEST_POSTGRES"));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			connection.Database?.StartsWith("payroll_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("Worker tests require a disposable loopback payroll_tests_* database.");
		services.Configure<AbpDbContextOptions>(options => options.Configure(context =>
			context.DbContextOptions.UseNpgsql(connection.ConnectionString)));
	}
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		var sender = Substitute.For<IEmailSender>();
		sender.SendAsync(Arg.Any<MailMessage>(), Arg.Any<bool>()).Returns(async call => {
			var message = call.Arg<MailMessage>();
			if (message.To.Count != 1 || !message.To[0].Address.EndsWith("@test.shikshamitra.local", StringComparison.Ordinal) ||
				message.CC.Count != 0 || message.Bcc.Count != 0 || message.Attachments.Count != 1)
				throw new InvalidOperationException("Only one synthetic fixture recipient is allowed.");
			var directory = Environment.GetEnvironmentVariable("PAYSLIP_WORKER_CHILD_ARTIFACTS")!;
			await File.WriteAllTextAsync(Path.Combine(directory, "smtp-entered"), Environment.ProcessId.ToString());
			if (Environment.GetEnvironmentVariable("PAYSLIP_WORKER_CHILD_MODE") == "interrupt")
				await Task.Delay(TimeSpan.FromMinutes(5)); // Parent terminates this process after the persisted claim.
			using var bytes = new MemoryStream();
			await message.Attachments[0].ContentStream.CopyToAsync(bytes);
			await File.WriteAllBytesAsync(Path.Combine(directory, "captured.pdf"), bytes.ToArray());
			await File.WriteAllTextAsync(Path.Combine(directory, "recipient"), message.To[0].Address);
		});
		builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton(sender)));
	}
}

[Collection(ApiTestCollection.Name)]
public class PayslipWorkerProcess_Tests
{
	[PayslipWorkerChildFact]
	public async Task Delivery_worker_child()
	{
		using var factory = new PayslipWorkerAttachFactory();
		using var scope = factory.Services.CreateScope();
		var directory = Environment.GetEnvironmentVariable("PAYSLIP_WORKER_CHILD_ARTIFACTS")!;
		await File.WriteAllTextAsync(Path.Combine(directory, "worker-pid"), Environment.ProcessId.ToString());
		await scope.ServiceProvider.GetRequiredService<PayslipDeliveryBackgroundJob>().ExecuteAsync(new() {
			TenantId = Guid.Parse(TestAuthHandler.TestTenantId),
			DeliveryId = Guid.Parse(Environment.GetEnvironmentVariable("PAYSLIP_WORKER_CHILD_ID")!) });
	}

	[PayslipWorkerProcessFact]
	public async Task Interrupted_worker_process_recovers_persisted_attempt_without_automatic_resend()
	{
		using var factory = new PayslipWorkerProcessSeedFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		var now = DateTime.UtcNow;
		var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipRestart", dateOfJoining: month);
		await workflow.ConfigureAsync(true);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, month, "GROSS", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, month);
		var run = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(run.Id, employee.Id, month);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(run.Id, employee.Id, month));
		var document = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
			$"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={month:yyyy-MM}", json))!.Single();
		async Task<PayslipDeliveryDto> QueueAsync()
		{
			var response = await api.PostAsJsonAsync("/api/payroll/payslip-deliveries", new SendPayslipDto {
				DocumentId = document.Id, IdempotencyKey = Guid.NewGuid() }, json);
			await EsiPayrollWorkflowScenario.RequireSuccess(response);
			return (await response.Content.ReadFromJsonAsync<PayslipDeliveryDto>(json))!;
		}
		var delivery = await QueueAsync();
		var root = Path.Combine(Path.GetTempPath(), "ShikshaMitraPayslipWorkerTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		var interrupted = Path.Combine(root, "interrupted");
		using (var process = StartChild(delivery.Id, "interrupt", interrupted))
		{
			try {
				var deadline = DateTime.UtcNow.AddMinutes(2);
				while (!File.Exists(Path.Combine(interrupted, "smtp-entered")) && !process.HasExited && DateTime.UtcNow < deadline)
					await Task.Delay(100);
				File.Exists(Path.Combine(interrupted, "smtp-entered")).ShouldBeTrue("The real worker must reach the capture transport.");
				process.Kill(entireProcessTree: true);
				await process.WaitForExitAsync();
			} finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
		}
		File.Exists(Path.Combine(interrupted, "captured.pdf")).ShouldBeFalse();
		var historyUrl = $"/api/payroll/payslip-deliveries?salarySlipId={document.SalarySlipId}";
		(await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, json))!.Single().Status.ShouldBe("PROCESSING");
		// Simulate expiry of the 30-minute lease; this is not a substitute for process termination.
		await using (var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("PAYROLL_PROCESS_TEST_POSTGRES")))
		{
			await connection.OpenAsync();
			await using var command = new NpgsqlCommand("UPDATE \"HR_PayslipDeliveries\" SET \"StartedAt\" = @at WHERE \"Id\" = @id", connection);
			command.Parameters.AddWithValue("at", DateTime.UtcNow.AddMinutes(-31));
			command.Parameters.AddWithValue("id", delivery.Id);
			(await command.ExecuteNonQueryAsync()).ShouldBe(1);
		}
		var recovered = Path.Combine(root, "recovered");
		await FinishChildAsync(StartChild(delivery.Id, "recover", recovered));
		(await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, json))!.Single().Status.ShouldBe("UNCERTAIN");
		File.Exists(Path.Combine(recovered, "smtp-entered")).ShouldBeFalse();
		(await File.ReadAllTextAsync(Path.Combine(interrupted, "worker-pid"))).ShouldNotBe(
			await File.ReadAllTextAsync(Path.Combine(recovered, "worker-pid")));
		var retry = await QueueAsync(); // Deliberate new request after the uncertain result.
		var accepted = Path.Combine(root, "accepted");
		await FinishChildAsync(StartChild(retry.Id, "capture", accepted));
		Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(accepted, "captured.pdf")))).ShouldBe(document.Sha256);
		(await File.ReadAllTextAsync(Path.Combine(accepted, "recipient"))).ShouldBe(document.RecipientEmail);
		var repeated = Path.Combine(root, "repeated");
		await FinishChildAsync(StartChild(retry.Id, "capture", repeated));
		File.Exists(Path.Combine(repeated, "smtp-entered")).ShouldBeFalse();
		(await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, json))!.Single(x => x.Id == retry.Id).Status.ShouldBe("PROVIDER_ACCEPTED");
	}

	private static Process StartChild(Guid deliveryId, string mode, string directory)
	{
		Directory.CreateDirectory(directory);
		var dotnet = Environment.GetEnvironmentVariable("PAYSLIP_WORKER_DOTNET") ?? Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
		var info = new ProcessStartInfo(dotnet) { UseShellExecute = false, CreateNoWindow = true };
		foreach (var argument in new[] { "vstest", typeof(PayslipWorkerProcess_Tests).Assembly.Location,
			"--TestCaseFilter:FullyQualifiedName=ShikshaMitra.HR.Payroll.PayslipWorkerProcess_Tests.Delivery_worker_child",
			"--ResultsDirectory:" + directory, "--logger:trx;LogFileName=worker.trx" }) info.ArgumentList.Add(argument);
		info.Environment["PAYSLIP_WORKER_CHILD_ID"] = deliveryId.ToString();
		info.Environment["PAYSLIP_WORKER_CHILD_MODE"] = mode;
		info.Environment["PAYSLIP_WORKER_CHILD_ARTIFACTS"] = directory;
		return Process.Start(info) ?? throw new InvalidOperationException("Could not start the delivery worker test process.");
	}
	private static async Task FinishChildAsync(Process process)
	{
		using (process)
		{
			try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2)); process.ExitCode.ShouldBe(0); }
			finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
		}
	}
}
