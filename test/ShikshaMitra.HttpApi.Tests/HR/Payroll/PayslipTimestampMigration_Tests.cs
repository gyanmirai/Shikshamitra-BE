using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipTimestampMigrationFactAttribute : FactAttribute
{
	public PayslipTimestampMigrationFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_TIMESTAMP_MIGRATION_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_TIMESTAMP_MIGRATION_TEST_POSTGRES to a fresh owned loopback payroll_tests_* database.";
	}
}

public sealed class PayslipTimestampMigrationFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_TIMESTAMP_MIGRATION_TEST_POSTGRES";
	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			connection.Database?.StartsWith("payroll_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("Migration tests require a disposable loopback payroll_tests_* database.");
		// Ensure the session and process offsets differ even on a UTC test host.
		connection.Timezone = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now) == TimeSpan.Zero ? "America/New_York" : "UTC";
		services.Configure<AbpDbContextOptions>(options => options.Configure(context =>
			context.DbContextOptions.UseNpgsql(connection.ConnectionString).AddInterceptors(Barrier)));
		using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql(connection.ConnectionString).Options);
		if (db.GetService<IRelationalDatabaseCreator>().HasTables())
			throw new InvalidOperationException("Never recreate or migrate an existing fixture database.");
		var migrations = db.GetService<IMigrationsAssembly>();
		migrations.Migrations.Keys.Where(id => id.Contains("Payslip", StringComparison.Ordinal)).ShouldBe(new[]
		{
			"20261003081008_AddPayslipDocumentsAndDelivery",
			"20261003093802_AddPayslipCorrectionDrafts"
		});
		var operations = migrations.Migrations.Values
			.SelectMany(type => migrations.CreateMigration(type, db.Database.ProviderName!).UpOperations).ToArray();
		foreach (var table in new[] { "HR_PayslipDocuments", "HR_PayslipDeliveries", "HR_PayslipSettings", "HR_PayslipCorrections" })
			operations.OfType<CreateTableOperation>().Count(operation => operation.Name == table).ShouldBe(1,
				$"The full compiled migration chain must create {table} exactly once.");
		foreach (var column in new[] { "PaidAt", "PaymentRecordedBy", "PaymentReference", "PaymentRequestId" })
			operations.OfType<AddColumnOperation>().Count(operation => operation.Table == "HR_SalarySlips" && operation.Name == column)
				.ShouldBe(1, $"The full compiled migration chain must add {column} exactly once.");
		// Apply the real tooling-generated migrations, with the unchanged runtime timestamp switch.
		// Do not replace this with CreateTables or suppress the pending-model warning.
		db.Database.Migrate();
		db.Database.GetPendingMigrations().ShouldBeEmpty();
		db.Database.GetAppliedMigrations().ShouldBe(db.Database.GetMigrations());
	}
}

[Collection(ApiTestCollection.Name)]
public class PayslipTimestampMigration_Tests
{
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	[PayslipTimestampMigrationFact]
	public async Task Migrated_payslip_instants_and_delivery_lease_expiry_preserve_actual_elapsed_time()
	{
		using var factory = new PayslipTimestampMigrationFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		await workflow.ConfigureAsync(true);
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipTimestamp", dateOfJoining: month);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, month, "GROSS", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, month);
		var run = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(run.Id, employee.Id, month);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(run.Id, employee.Id, month));
		var document = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
			$"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={month:yyyy-MM}", Json))!.Single();
		var historyUrl = $"/api/payroll/payslip-deliveries?salarySlipId={document.SalarySlipId}";
		factory.Messages.ShouldBeEmpty();

		using (var scope = factory.Services.CreateScope())
		{
			using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant);
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
			var saved = await scope.ServiceProvider.GetRequiredService<IRepository<PayslipDocument, Guid>>().GetAsync(document.Id);
			var frozen = JsonSerializer.Deserialize<SalarySlipDto>(saved.FactsJson)!;
			// PostgreSQL stores microseconds; JSON retains the original 100-nanosecond ticks.
			(Utc(saved.IssuedAt).Ticks / 10).ShouldBe(Utc(frozen.ProcessedDate).Ticks / 10);
			var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			(await db.Database.GetAppliedMigrationsAsync()).Count().ShouldBeGreaterThan(100);
			(await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
			// The unrelated global mismatch must remain visible, not be suppressed by this scoped fix.
			db.Database.HasPendingModelChanges().ShouldBeTrue();
		}

		foreach (var recoveryKind in new[] { DateTimeKind.Local, DateTimeKind.Utc })
		{
			var queued = await api.PostAsJsonAsync("/api/payroll/payslip-deliveries", new SendPayslipDto
			{
				DocumentId = document.Id,
				IdempotencyKey = Guid.NewGuid()
			}, Json);
			await EsiPayrollWorkflowScenario.RequireSuccess(queued);
			var attempt = (await queued.Content.ReadFromJsonAsync<PayslipDeliveryDto>(Json))!;
			var started = DateTime.Now;
			await Should.ThrowAsync<InvalidOperationException>(() => ExecuteAsync(factory, attempt.Id, started, interrupt: true));
			async Task<PayslipDeliveryDto> ResultAsync() =>
				(await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, Json))!.Single(x => x.Id == attempt.Id);
			(await ResultAsync()).Status.ShouldBe("PROCESSING");
			var current = recoveryKind == DateTimeKind.Utc ? started.ToUniversalTime() : started;
			await ExecuteAsync(factory, attempt.Id, current);
			(await ResultAsync()).Status.ShouldBe("PROCESSING", "A live claim must not be stolen because the clock/session offsets differ.");
			await ExecuteAsync(factory, attempt.Id, current.AddMinutes(31));
			var expired = await ResultAsync();
			expired.Status.ShouldBe("UNCERTAIN", "31 actual elapsed minutes must expire the persisted 30-minute lease.");
			expired.ErrorCategory.ShouldBe("SEND_OUTCOME_UNKNOWN");
			await ExecuteAsync(factory, attempt.Id, current.AddMinutes(32));
			factory.Messages.ShouldBeEmpty();
		}

		var retryResponse = await api.PostAsJsonAsync("/api/payroll/payslip-deliveries", new SendPayslipDto
		{
			DocumentId = document.Id,
			IdempotencyKey = Guid.NewGuid()
		}, Json);
		await EsiPayrollWorkflowScenario.RequireSuccess(retryResponse);
		var retry = (await retryResponse.Content.ReadFromJsonAsync<PayslipDeliveryDto>(Json))!;
		await ExecuteAsync(factory, retry.Id, DateTime.Now);
		await ExecuteAsync(factory, retry.Id, DateTime.Now);
		var captured = factory.Messages.Single();
		captured.Recipient.ShouldBe(document.RecipientEmail);
		Convert.ToHexString(SHA256.HashData(captured.Pdf)).ShouldBe(document.Sha256);
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{document.Id}/pdf")))
			.ShouldBe(document.Sha256);
		var history = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, Json))!;
		history.Count.ShouldBe(3);
		history.Count(x => x.Status == "UNCERTAIN").ShouldBe(2);
		history.Single(x => x.Id == retry.Id).Status.ShouldBe("PROVIDER_ACCEPTED");

		var replacementRequest = new ReplacePayslipFactsDto
		{
			DocumentId = document.Id,
			IdempotencyKey = Guid.NewGuid(),
			Reason = "Synthetic issuance instant consistency check"
		};
		var replacementResponse = await api.PostAsJsonAsync("/api/payroll/payslip-documents/replace-facts", replacementRequest, Json);
		await EsiPayrollWorkflowScenario.RequireSuccess(replacementResponse);
		var replacement = (await replacementResponse.Content.ReadFromJsonAsync<PayslipDocumentDto>(Json))!;
		var repeatedResponse = await api.PostAsJsonAsync("/api/payroll/payslip-documents/replace-facts", replacementRequest, Json);
		await EsiPayrollWorkflowScenario.RequireSuccess(repeatedResponse);
		var repeated = (await repeatedResponse.Content.ReadFromJsonAsync<PayslipDocumentDto>(Json))!;
		repeated.Id.ShouldBe(replacement.Id);
		// Compare at the same PostgreSQL microsecond precision used for frozen facts below.
		(Utc(repeated.IssuedAt).Ticks / 10).ShouldBe(Utc(replacement.IssuedAt).Ticks / 10);
		using (var scope = factory.Services.CreateScope())
		{
			using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant);
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
			var saved = await scope.ServiceProvider.GetRequiredService<IRepository<PayslipDocument, Guid>>().GetAsync(replacement.Id);
			var frozen = JsonSerializer.Deserialize<SalarySlipDto>(saved.FactsJson)!;
			(Utc(saved.IssuedAt).Ticks / 10).ShouldBe(Utc(frozen.ProcessedDate).Ticks / 10,
				"A replacement must use the same issuance instant for its canonical facts and history metadata.");
		}
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{document.Id}/pdf")))
			.ShouldBe(document.Sha256);
	}

	private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Unspecified
		? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

	private static async Task ExecuteAsync(PayslipPostgresTestFactory factory, Guid delivery, DateTime at, bool interrupt = false)
	{
		using var scope = factory.Services.CreateScope();
		var fence = scope.ServiceProvider.GetRequiredService<IPayrollInputRevisionStore>();
		if (interrupt) fence = new InterruptBeforeSendFence(fence);
		var worker = ActivatorUtilities.CreateInstance<PayslipDeliveryBackgroundJob>(scope.ServiceProvider, fence, new FixedClock(at));
		await worker.ExecuteAsync(new() { TenantId = EsiPayrollWorkflowScenario.Tenant, DeliveryId = delivery });
	}

	private sealed class InterruptBeforeSendFence(IPayrollInputRevisionStore inner) : IPayrollInputRevisionStore
	{
		private int _locks;
		public Task<Guid> ReadAsync() => inner.ReadAsync();
		public Task<Guid> LockAsync() => ++_locks == 2
			? throw new InvalidOperationException("Synthetic interruption after persisted claim and before send") : inner.LockAsync();
	}

	private sealed class FixedClock(DateTime at) : IClock
	{
		public DateTime Now => at;
		public DateTimeKind Kind => at.Kind;
		public bool SupportsMultipleTimezone => Kind == DateTimeKind.Utc;
		public DateTime Normalize(DateTime value) => Kind == DateTimeKind.Utc ? value.ToUniversalTime() : value.ToLocalTime();
	}
}