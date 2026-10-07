using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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

public sealed class PayslipSchoolIssuanceFactAttribute : FactAttribute
{
	public PayslipSchoolIssuanceFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_SCHOOL_ISSUANCE_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_SCHOOL_ISSUANCE_TEST_POSTGRES to a fresh disposable loopback payroll_tests_* database.";
	}
}

public sealed class PayslipSchoolIssuanceTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_SCHOOL_ISSUANCE_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipSchoolIssuance_Tests
{
	[PayslipSchoolIssuanceFact]
	public async Task Missing_school_details_block_issuance_atomically_and_valid_configuration_recovers()
	{
		using var factory = new PayslipSchoolIssuanceTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		// This factory owns a fresh empty database; omit only its synthetic school's configured fixture row.
		// No issued evidence, existing school data or permission grants are changed.
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var settings = scope.ServiceProvider.GetRequiredService<IRepository<PayslipSettings, Guid>>();
			await settings.HardDeleteAsync(await settings.GetAsync(Guid.Parse(TestAuthHandler.TestTenantId)));
			await unit.CompleteAsync();
		}
		var defaults = (await api.GetFromJsonAsync<PayslipSettingsDto>("/api/payroll/payslip-settings", json))!;
		defaults.SchoolAddress.ShouldBeEmpty();
		defaults.ConcurrencyStamp.ShouldBeNull();
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipSchoolFacts", dateOfJoining: month);
		await workflow.ConfigureAsync(true);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, month, "EMPLOYER_COST", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, month);
		var preview = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(preview.Id, employee.Id, month);
		var failedLock = await workflow.LockAsync(preview.Id, employee.Id, month);
		failedLock.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await failedLock.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.PayslipSchoolDetailsRequired);
		var documentsUrl = $"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={month:yyyy-MM}";
		(await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!.ShouldBeEmpty();
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false))
		{
			(await scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>()
				.GetAsync(x => x.EmployeeId == employee.Id && x.MonthYear == month.ToString("yyyy-MM"))).IsLocked.ShouldBeFalse();
			(await scope.ServiceProvider.GetRequiredService<ISalarySlipRepository>().AnyAsync(x => x.EmployeeId == employee.Id)).ShouldBeFalse();
			(await scope.ServiceProvider.GetRequiredService<IRepository<PayslipDelivery, Guid>>().AnyAsync()).ShouldBeFalse();
		}
		factory.Messages.ShouldBeEmpty();
		var invalidSave = await api.PutAsJsonAsync("/api/payroll/payslip-settings", new PayslipSettingsDto
		{
			SchoolName = "Synthetic School",
			SchoolAddress = "",
			IdempotencyKey = Guid.NewGuid()
		}, json);
		invalidSave.IsSuccessStatusCode.ShouldBeFalse();
		(await api.GetFromJsonAsync<PayslipSettingsDto>("/api/payroll/payslip-settings", json))!.ConcurrencyStamp.ShouldBeNull();
		var save = await api.PutAsJsonAsync("/api/payroll/payslip-settings", new PayslipSettingsDto
		{
			SchoolName = "Synthetic Recovered School",
			SchoolAddress = "Synthetic Recovery Road, Ahmedabad, Gujarat",
			AutomaticEmail = false,
			IdempotencyKey = Guid.NewGuid()
		}, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(save);
		var fresh = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(fresh.Id, employee.Id, month);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(fresh.Id, employee.Id, month));
		var document = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!.Single();
		document.Revision.ShouldBe(1);
		document.ReplacesDocumentId.ShouldBeNull();
		var pdf = await api.GetByteArrayAsync($"/api/payroll/payslip-documents/{document.Id}/pdf");
		Encoding.ASCII.GetString(pdf.AsSpan(0, 5)).ShouldBe("%PDF-");
		Convert.ToHexString(SHA256.HashData(pdf)).ShouldBe(document.Sha256);
		var slip = (await api.GetFromJsonAsync<SalarySlipResponse>(
			$"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={month:yyyy-MM}", json))!;
		slip.Status.ShouldBe("Finalized");
		factory.Messages.ShouldBeEmpty();
	}
}