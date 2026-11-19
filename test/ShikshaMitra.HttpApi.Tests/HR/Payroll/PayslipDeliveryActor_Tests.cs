using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipDeliveryActorFactAttribute : FactAttribute
{
	public PayslipDeliveryActorFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_ACTOR_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_ACTOR_TEST_POSTGRES to a fresh owned loopback payroll_tests_* database.";
	}
}

public sealed class PayslipDeliveryActorTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_ACTOR_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipDeliveryActor_Tests
{
	[PayslipDeliveryActorFact]
	public async Task Delivery_responses_show_same_tenant_requesters_without_exposing_foreign_or_missing_accounts()
	{
		using var factory = new PayslipDeliveryActorTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		await workflow.ConfigureAsync(true);
		var settings = (await api.GetFromJsonAsync<PayslipSettingsDto>("/api/payroll/payslip-settings", json))!;
		settings.AutomaticEmail = true;
		settings.IdempotencyKey = Guid.NewGuid();
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync("/api/payroll/payslip-settings", settings, json));
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipActor", dateOfJoining: month);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, month, "GROSS", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, month);
		var run = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(run.Id, employee.Id, month);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(run.Id, employee.Id, month));
		var document = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
			$"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={month:yyyy-MM}", json))!.Single();
		var historyUrl = $"/api/payroll/payslip-deliveries?salarySlipId={document.SalarySlipId}";
		using var initial = JsonDocument.Parse(await api.GetStringAsync(historyUrl));
		var automatic = initial.RootElement.EnumerateArray().Single();
		automatic.GetProperty("automatic").GetBoolean().ShouldBeTrue();
		var actor = automatic.GetProperty("requestedBy").GetGuid();
		string expectedName;
		var namedActor = Guid.NewGuid();
		var namedAttempt = Guid.NewGuid();
		var foreignActor = Guid.NewGuid();
		var missingActor = Guid.NewGuid();
		var foreignAttempt = Guid.NewGuid();
		var missingAttempt = Guid.NewGuid();
		using (var scope = factory.Services.CreateScope())
		{
			var services = scope.ServiceProvider;
			var tenant = services.GetRequiredService<ICurrentTenant>();
			using (tenant.Change(EsiPayrollWorkflowScenario.Tenant))
			using (var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
			{
				var user = await services.GetRequiredService<IRepository<IdentityUser, Guid>>().GetAsync(actor);
				var fullName = string.Join(" ", new[] { user.Name, user.Surname }.Where(x => !string.IsNullOrWhiteSpace(x)));
				expectedName = string.IsNullOrWhiteSpace(fullName) ? user.UserName : fullName;
				var named = new IdentityUser(namedActor, "payslip-display-actor", "named-actor@example.test", tenant.Id)
				{ Name = "Synthetic HR", Surname = "Reviewer" };
				(await services.GetRequiredService<IdentityUserManager>().CreateAsync(named)).Succeeded.ShouldBeTrue();
				await unit.CompleteAsync();
			}
			using (tenant.Change(Guid.NewGuid()))
			using (var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
			{
				// New foreign fixture identity with no grants; never modify an existing actor.
				var user = new IdentityUser(foreignActor, "foreign-payslip-actor", "foreign-actor@example.test", tenant.Id)
				{ Name = "Foreign confidential", Surname = "Requester" };
				(await services.GetRequiredService<IdentityUserManager>().CreateAsync(user)).Succeeded.ShouldBeTrue();
				await unit.CompleteAsync();
			}
			using (tenant.Change(EsiPayrollWorkflowScenario.Tenant))
			using (var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
			{
				// Deliberately inconsistent audit IDs exercise fail-closed name resolution only.
				var attempts = services.GetRequiredService<IRepository<PayslipDelivery, Guid>>();
				foreach (var (id, requester) in new[] { (namedAttempt, namedActor), (foreignAttempt, foreignActor), (missingAttempt, missingActor) })
					await attempts.InsertAsync(new PayslipDelivery(id, tenant.Id, document.Id, employee.Id,
						Guid.NewGuid(), false, requester, ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add));
				await unit.CompleteAsync();
			}
		}

		// Assert the real HTTP response before any production change, not a DTO with a manufactured name.
		automatic.TryGetProperty("requestedByDisplayName", out var initialName).ShouldBeTrue("Delivery history must expose a readable same-tenant requester.");
		initialName.GetString().ShouldBe(expectedName);
		var request = new SendPayslipDto { DocumentId = document.Id, IdempotencyKey = Guid.NewGuid() };
		var manualResponse = await api.PostAsJsonAsync("/api/payroll/payslip-deliveries", request, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(manualResponse);
		using var manual = JsonDocument.Parse(await manualResponse.Content.ReadAsStringAsync());
		manual.RootElement.GetProperty("requestedBy").GetGuid().ShouldBe(actor);
		manual.RootElement.GetProperty("requestedByDisplayName").GetString().ShouldBe(expectedName);
		var repeated = await api.PostAsJsonAsync("/api/payroll/payslip-deliveries", request, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(repeated);
		using var duplicate = JsonDocument.Parse(await repeated.Content.ReadAsStringAsync());
		duplicate.RootElement.GetProperty("id").GetGuid().ShouldBe(manual.RootElement.GetProperty("id").GetGuid());
		using var history = JsonDocument.Parse(await api.GetStringAsync(historyUrl));
		var rows = history.RootElement.EnumerateArray().ToList();
		rows.Count.ShouldBe(5);
		rows.Single(x => x.GetProperty("id").GetGuid() == namedAttempt).GetProperty("requestedByDisplayName")
			.GetString().ShouldBe("Synthetic HR Reviewer");
		foreach (var row in rows.Where(x => x.GetProperty("requestedBy").GetGuid() == actor))
			row.GetProperty("requestedByDisplayName").GetString().ShouldBe(expectedName);
		foreach (var id in new[] { foreignAttempt, missingAttempt })
			rows.Single(x => x.GetProperty("id").GetGuid() == id).GetProperty("requestedByDisplayName").ValueKind.ShouldBe(JsonValueKind.Null);
		history.RootElement.GetRawText().ShouldNotContain("Foreign confidential");
		factory.Messages.ShouldBeEmpty("Name projection must not send or resend a payslip.");
	}
}