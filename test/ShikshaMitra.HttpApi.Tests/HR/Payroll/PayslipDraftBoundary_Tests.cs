using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipDraftBoundaryFactAttribute : FactAttribute
{
	public PayslipDraftBoundaryFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_DRAFT_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_DRAFT_TEST_POSTGRES to a fresh owned loopback payroll_tests_* database.";
	}
}

public sealed class PayslipDraftBoundaryTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_DRAFT_TEST_POSTGRES";
	public PayslipDraftAuditLogger Audit { get; } = new();
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<ILogger<SalarySlipAppService>>(Audit)));
	}
}

public sealed class PayslipDraftAuditLogger : ILogger<SalarySlipAppService>
{
	public ConcurrentQueue<string[]> FieldNames { get; } = new();
	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
	public bool IsEnabled(LogLevel level) => true;
	public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
		if (state is IEnumerable<KeyValuePair<string, object?>> fields)
			FieldNames.Enqueue(fields.Where(x => x.Key != "{OriginalFormat}").Select(x => x.Key).ToArray());
	}
}

[Collection(ApiTestCollection.Name)]
public class PayslipDraftBoundary_Tests
{
	[PayslipDraftBoundaryFact]
	public async Task Drafts_require_real_school_facts_cannot_be_distributed_and_keep_salary_amounts_out_of_logs()
	{
		using var factory = new PayslipDraftBoundaryTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		await workflow.ConfigureAsync(true);
		const string schoolName = "Synthetic Draft Boundary School";
		const string schoolAddress = "Owned Draft Road, Ahmedabad, Gujarat";
		const string settingsUrl = "/api/payroll/payslip-settings";
		async Task SaveSchoolAsync()
		{
			var settings = (await api.GetFromJsonAsync<PayslipSettingsDto>(settingsUrl, json))!;
			settings.SchoolName = schoolName;
			settings.SchoolAddress = schoolAddress;
			settings.AutomaticEmail = true;
			settings.IdempotencyKey = Guid.NewGuid();
			await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync(settingsUrl, settings, json));
		}
		await SaveSchoolAsync();
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipDraftBoundary", dateOfJoining: month);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, month, "GROSS", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, month);
		await workflow.CalculateAsync(month);
		var query = $"employeeId={employee.Id}&monthYear={month:yyyy-MM}";
		var slipUrl = $"/api/payroll/salary-slips?{query}";
		var previewUrl = $"/api/payroll/salary-slips/pdf-preview?{query}";
		var documentsUrl = $"/api/payroll/payslip-documents?{query}";
		var draft = (await api.GetFromJsonAsync<SalarySlipResponse>(slipUrl, json))!;
		draft.Status.ShouldBe("Draft");
		draft.DocumentId.ShouldBeNull();
		draft.DocumentRevision.ShouldBeNull();
		draft.NetPay.ShouldBe(9925);
		var preview = await api.GetByteArrayAsync(previewUrl);
		Guid draftId;
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant))
		using (scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false))
			draftId = (await scope.ServiceProvider.GetRequiredService<ISalarySlipRepository>()
				.GetByEmployeeAndMonthAsync(employee.Id, month.ToString("yyyy-MM", CultureInfo.InvariantCulture)))
				.ShouldNotBeNull("The draft read must persist its authoritative source slip.").Id;
		(await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!.ShouldBeEmpty();
		var officialBeforeLock = await api.GetAsync($"/api/payroll/salary-slips/pdf?{query}");
		officialBeforeLock.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await officialBeforeLock.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.SalarySlipPdfRequiresFinalized);
		var attemptedSend = await api.PostAsJsonAsync("/api/payroll/payslip-deliveries",
			new SendPayslipDto { DocumentId = draftId, IdempotencyKey = Guid.NewGuid() }, json);
		attemptedSend.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await api.GetFromJsonAsync<List<PayslipDeliveryDto>>($"/api/payroll/payslip-deliveries?salarySlipId={draftId}", json))!.ShouldBeEmpty();
		factory.Messages.ShouldBeEmpty();
		// Omit only this newly created fixture's school row before any official document exists.
		// Payroll preparation remains available, but a PDF must not invent school identity facts.
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var settings = scope.ServiceProvider.GetRequiredService<IRepository<PayslipSettings, Guid>>();
			await settings.HardDeleteAsync(await settings.GetAsync(EsiPayrollWorkflowScenario.Tenant));
			await unit.CompleteAsync();
		}
		var missingSchool = await api.GetAsync(previewUrl);
		var missingSchoolCode = missingSchool.IsSuccessStatusCode ? null : await missingSchool.Content.ReadAsStringAsync();
		(await api.GetFromJsonAsync<SalarySlipResponse>(slipUrl, json))!.Status.ShouldBe("Draft");
		await SaveSchoolAsync();
		var run = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(run.Id, employee.Id, month);
		(await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!.ShouldBeEmpty();
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(run.Id, employee.Id, month));
		var issued = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!.Single();
		issued.Revision.ShouldBe(1);
		issued.SalarySlipId.ShouldBe(draftId);
		var official = await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{issued.Id}/pdf");
		var officialHash = Convert.ToHexString(SHA256.HashData(official));
		officialHash.ShouldBe(issued.Sha256);
		Convert.ToHexString(SHA256.HashData(preview)).ShouldNotBe(officialHash);
		Convert.ToHexString(SHA256.HashData(await api.GetByteArrayAsync(previewUrl))).ShouldBe(officialHash);
		var delivery = (await api.GetFromJsonAsync<List<PayslipDeliveryDto>>($"/api/payroll/payslip-deliveries?salarySlipId={draftId}", json))!.Single();
		delivery.DocumentId.ShouldBe(issued.Id);
		delivery.Automatic.ShouldBeTrue();
		using (var scope = factory.Services.CreateScope())
			await scope.ServiceProvider.GetRequiredService<PayslipDeliveryBackgroundJob>().ExecuteAsync(
				new() { TenantId = EsiPayrollWorkflowScenario.Tenant, DeliveryId = delivery.Id });
		factory.Messages.Count.ShouldBe(1);
		Convert.ToHexString(SHA256.HashData(factory.Messages.Single().Pdf)).ShouldBe(officialHash);
		var artifactRoot = Environment.GetEnvironmentVariable("PAYSLIP_VERIFY_ARTIFACTS");
		if (!string.IsNullOrWhiteSpace(artifactRoot))
		{
			Directory.CreateDirectory(artifactRoot);
			await File.WriteAllBytesAsync(Path.Combine(artifactRoot, "draft.pdf"), preview);
			await File.WriteAllBytesAsync(Path.Combine(artifactRoot, "issued.pdf"), official);
			await File.WriteAllTextAsync(Path.Combine(artifactRoot, "evidence.json"), JsonSerializer.Serialize(new
			{
				schoolName,
				schoolAddress,
				draft.EmployeeName,
				draft.EmployeeCode,
				draft.NetPay,
				missingSchoolStatus = (int) missingSchool.StatusCode,
				missingSchoolCode,
				logFieldNames = factory.Audit.FieldNames.ToArray(),
				draftSha256 = Convert.ToHexString(SHA256.HashData(preview)),
				issuedSha256 = officialHash
			}, json));
		}
		factory.Audit.FieldNames.ShouldContain(fields => new[] { "EmployeeId", "MonthYear", "Status", "Duration" }.All(fields.Contains));
		factory.ShouldSatisfyAllConditions(
			() => missingSchool.StatusCode.ShouldBe(HttpStatusCode.BadRequest),
			() => missingSchoolCode.ShouldNotBeNull().ShouldContain(HRErrorCodes.PayslipSchoolDetailsRequired),
			() => factory.Audit.FieldNames.SelectMany(fields => fields).ShouldNotContain("TotalEarning"),
			() => factory.Audit.FieldNames.SelectMany(fields => fields).ShouldNotContain("TotalDeduction"),
			() => factory.Audit.FieldNames.SelectMany(fields => fields).ShouldNotContain("NetSalary"));
	}
}