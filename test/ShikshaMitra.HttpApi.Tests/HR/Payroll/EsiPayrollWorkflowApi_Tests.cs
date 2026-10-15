using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class EsiPayrollWorkflowApi_Tests
{
	[PayrollRegistrationPostgresFact]
	public async Task Pending_registration_to_locked_payroll_for_both_salary_bases()
	{
		using var factory = new PayrollRegistrationPostgresTestFactory();
		var scenario = new EsiPayrollWorkflowScenario(factory);
		foreach (var activeBeforeEsi in new[] { false, true })
			foreach (var basis in new[] { "GROSS", "EMPLOYER_COST" })
				await scenario.RegistrationJourneyAsync(basis, activeBeforeEsi);
	}
}

internal class EsiPayrollWorkflowScenario(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	internal static readonly Guid Tenant = Guid.Parse(TestAuthHandler.TestTenantId);
	internal const string Previews = "/api/payroll/run/previews";
	private const string Configuration = "/api/payroll/statutory-schemes/esi/configuration";
	internal HttpClient Api => Client;

	internal async Task RegistrationJourneyAsync(string basis, bool activeBeforeEsi)
	{
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		var now = DateTime.UtcNow;
		var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "EsiJourney", dateOfJoining: month);
		var id = employee.Id;
		await ConfigureAsync(!activeBeforeEsi);
		await EnrollAsync(id, null);
		var compensation = await SaveCompensationAsync(id, month, basis, 10000m);
		if (activeBeforeEsi)
		{
			await RequireSuccess(await ActivateAsync(id));
			await ConfigureAsync(true);
			// Resolve changed employer cost through compensation without shrinking agreed wages.
			compensation = await SaveCompensationAsync(id, month, basis, basis == "EMPLOYER_COST" ? 10325m : 10000m, compensation.RevisionNumber);
		}
		else await RequireSuccess(await ActivateAsync(id));
		await SeedAttendanceAsync(id, month);
		var pending = await CalculateAsync(month);
		var pendingRow = await EmployeeAsync(pending.Id, id);
		pendingRow.IssueCodes.ShouldContain("ESI_NUMBER_MISSING");
		pendingRow.TotalDeduction.ShouldBe(0);
		pendingRow.EmployerContributionTotal.ShouldBe(0);
		PayrollLockPreconditionGuard.HasBlockingLockIssue(System.Text.Json.JsonSerializer.Serialize(new[] { "ESI_NUMBER_MISSING" })).ShouldBeFalse();
		await ReviewAsync(pending.Id, id, month);
		var readiness = (await GetAndReadAsync<RunPayrollResponse>($"{Previews}/{pending.Id}/employees?pageSize=100")).Readiness;
		readiness!.BlockingIssues.ShouldNotContain("ESI_NUMBER_MISSING");
		await EnrollAsync(id, "1234567890");
		var stored = await GetAndReadAsync<EmployeeStatutoryEnrollmentDto>($"/api/payroll/statutory-schemes/esi/employees/{id}/enrollment");
		stored.InsuranceNumber.ShouldBe("1234567890");

		if (activeBeforeEsi && basis == "GROSS") await ClassifyCustomEarningAsync(id, month);
		var ready = await CalculateAsync(month);
		var calculated = await EmployeeAsync(ready.Id, id);
		calculated.IssueCodes.ShouldNotContain("ESI_NUMBER_MISSING");
		calculated.GrossSalary.ShouldBe(pendingRow.GrossSalary);
		calculated.IssueCodes.ShouldNotContain("ESI_WAGE_CLASSIFICATION_REQUIRED");
		calculated.EmployeeStateInsuranceCoverageDecision.ShouldBe("WITHIN_WAGE_CEILING");
		calculated.EmployeeStateInsuranceNumber.ShouldBe("1234567890");
		calculated.EmployerCost.ShouldBe(Math.Round(calculated.TotalEarnings + calculated.EmployerContributionTotal, 2));
		calculated.NetPay.ShouldBe(Math.Round(calculated.TotalEarnings - calculated.TotalDeduction, 2));
		await ReviewAsync(ready.Id, id, month);
		await RequireSuccess(await LockAsync(ready.Id, id, month));
		(await EmployeeAsync(ready.Id, id)).IsLocked.ShouldBeTrue();
	}

	private async Task ClassifyCustomEarningAsync(Guid id, DateTime month)
	{
		var current = await CalculateAsync(month);
		await ReviewAsync(current.Id, id, month);
		var adjustment = await PostAndReadAsync<PayrollAdjustmentDto>("/api/payroll/run/adjustments", new
		{
			month = month.ToString("yyyy-MM"),
			employeeId = id,
			name = "Synthetic custom bonus",
			amount = 500,
			isEarning = true,
			reason = "ESIC classification regression"
		});
		var unclassified = await CalculateAsync(month);
		(await EmployeeAsync(unclassified.Id, id)).IssueCodes.ShouldContain("ESI_WAGE_CLASSIFICATION_REQUIRED");
		var hidden = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Tenant))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollAdjustment, Guid>>();
			await repository.InsertAsync(new(hidden[0], Guid.NewGuid(), month.ToString("yyyy-MM"), id, "Other tenant", 1m, true, "Isolation fixture"));
			await repository.InsertAsync(new(hidden[1], Tenant, month.AddMonths(-1).ToString("yyyy-MM"), id, "Other month", 1m, true, "Month fixture"));
			await repository.InsertAsync(new(hidden[2], Tenant, month.ToString("yyyy-MM"), id, "Deduction", 1m, false, "Deduction fixture"));
			await uow.CompleteAsync();
		}
		var catalog = await GetAndReadAsync<List<PayrollComponentStatutoryTreatmentDto>>(
			$"/api/payroll/statutory-schemes/esi/component-treatments?effectiveOn={month:yyyy-MM-dd}");
		catalog.ShouldContain(x => x.ComponentId == adjustment.Id && x.Treatment == "Unclassified");
		catalog.ShouldNotContain(x => hidden.Contains(x.ComponentId));
		var settings = await GetAndReadAsync<EsiConfigurationSettingsDto>(Configuration);
		await RequireSuccess(await Client.PutAsJsonAsync(Configuration, new SaveEsiConfigurationDto
		{
			Enabled = true,
			ConcurrencyStamp = settings.Current!.ConcurrencyStamp,
			IdempotencyKey = Guid.NewGuid(),
			Treatments = [new() { ComponentId = adjustment.Id, Treatment = "Included" }]
		}, JsonOptions));
	}

	internal async Task ConfigureAsync(bool enabled)
	{
		var settings = await GetAndReadAsync<EsiConfigurationSettingsDto>(Configuration);
		await RequireSuccess(await Client.PutAsJsonAsync(Configuration, new SaveEsiConfigurationDto
		{ Enabled = enabled, ConcurrencyStamp = settings.Current!.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid() }, JsonOptions));
	}

	internal Task<HttpResponseMessage> ActivateAsync(Guid id) => Client.PutAsJsonAsync($"/api/employees/{id}/status",
		new { employeeStatusType = 0, remarks = "Synthetic ESIC workflow verification" }, JsonOptions);

	internal async Task EnrollAsync(Guid id, string? number)
	{
		var profileUrl = $"/api/employees/{id}/payroll/statutory-profile";
		var profile = (await Client.GetFromJsonAsync<EmployeeStatutoryProfileDto>(profileUrl, JsonOptions))!;
		await RequireSuccess(await Client.PutAsJsonAsync(profileUrl, new SaveEmployeeStatutoryProfileDto
		{
			Profile = profile.Profile with { EsicInsuranceNumber = number }, UpdatePf = false,
			ConcurrencyStamp = profile.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid()
		}, JsonOptions));
		await RequireSuccess(await Client.PutAsJsonAsync(
			$"/api/payroll/statutory-schemes/esi/employees/{id}/enrollment", new UpdateEmployeeStatutoryEnrollmentDto
			{ Status = number == null ? "RegistrationPending" : "PendingReview", StatusReason = "Synthetic test registration" }, JsonOptions));
	}

	internal async Task<CompensationRevisionDto> SaveCompensationAsync(Guid id, DateTime month, string basis, decimal amount, int? expected = null)
	{
		// This fixture isolates ESIC and intentionally disables PF with an audited reason.
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Tenant))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var resolver = scope.ServiceProvider.GetRequiredService<EmployeeStatutoryProfileResolver>();
			var profile = await resolver.ResolveAsync(id, month);
			if (profile.ApplyPf)
			{
				var repository = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeStatutoryProfileVersion, Guid>>();
				if (profile.VersionId.HasValue && profile.EffectiveMonth == DateTime.SpecifyKind(month, DateTimeKind.Utc))
				{ var existing = await repository.GetAsync(profile.VersionId.Value); existing.Supersede(); await repository.UpdateAsync(existing, autoSave: true); }
				await repository.InsertAsync(new EmployeeStatutoryProfileVersion(Guid.NewGuid(), Tenant, id, month,
					profile with { ApplyPf = false, PfDisabledReason = "ESIC-only workflow fixture" }, Guid.NewGuid(), "FIXTURE"), autoSave: true);
			}
			await unit.CompleteAsync();
		}
		var templates = await GetAndReadAsync<SalaryTemplateListDto>("/api/payroll/salary-templates/active");
		var template = templates.Items.First(x => x.IsDefault);
		var response = await Client.PutAsJsonAsync($"/api/payroll/employees/{id}/compensation", new SaveEmployeeCompensationDto
		{
			TemplateVersionId = template.EditableVersion.Id,
			SalaryBasis = basis,
			MonthlyAmount = amount,
			EffectiveMonth = month,
			PfApplicable = false,
			IdempotencyKey = Guid.NewGuid().ToString("N"),
			ExpectedRevision = expected
		}, JsonOptions);
		await RequireSuccess(response);
		return (await response.Content.ReadFromJsonAsync<CompensationRevisionDto>(JsonOptions))!;
	}

	internal async Task<PayrollPreviewStatusDto> StartAsync(DateTime month) => await PostAndReadAsync<PayrollPreviewStatusDto>(Previews,
		new { month = month.ToString("yyyy-MM"), refresh = true });
	internal async Task ExecuteAsync(Guid runId)
	{
		using var scope = Factory.Services.CreateScope();
		await scope.ServiceProvider.GetRequiredService<PayrollPreviewBackgroundJob>().ExecuteAsync(new PayrollPreviewJobArgs { TenantId = Tenant, RunId = runId });
	}
	internal async Task<PayrollPreviewStatusDto> CalculateAsync(DateTime month)
	{
		var run = await StartAsync(month);
		await ExecuteAsync(run.Id);
		var result = await StatusAsync(run.Id);
		result.Status.ShouldBe("SUCCEEDED", result.ErrorMessage);
		return result;
	}
	internal Task<PayrollPreviewStatusDto> StatusAsync(Guid runId) => GetAndReadAsync<PayrollPreviewStatusDto>($"{Previews}/{runId}");
	internal async Task<RunPayrollItem> EmployeeAsync(Guid runId, Guid id) =>
		(await GetAndReadAsync<RunPayrollResponse>($"{Previews}/{runId}/employees?pageSize=100")).Items.Single(x => x.EmployeeId == id);
	internal async Task ReviewAsync(Guid runId, Guid id, DateTime month) => await RequireSuccess(await Client.PostAsJsonAsync("/api/payroll/run/reviews",
		new { month = month.ToString("yyyy-MM"), previewRunId = runId, employeeIds = new[] { id } }));
	internal Task<HttpResponseMessage> LockAsync(Guid runId, Guid id, DateTime month) => Client.PostAsJsonAsync("/api/payroll/run",
		new { month = month.ToString("yyyy-MM"), previewRunId = runId, employeeIds = new[] { id } });
	internal static async Task RequireSuccess(HttpResponseMessage response) =>
		response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());

	internal async Task SeedAttendanceAsync(Guid id, DateTime month)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Tenant);
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		for (var date = month; date < month.AddMonths(1); date = date.AddDays(1))
			await repository.InsertAsync(new EmployeeAttendance
			{
				TenantId = Tenant,
				EmployeeId = id,
				EducationMediumStageId = EmployeeTestHelper.DefaultMediumStageId,
				AttendanceDate = DateOnly.FromDateTime(date),
				StatusType = EmployeeAttendanceStatusType.Present,
				TimeIn = new DateTimeOffset(DateTime.SpecifyKind(date.AddHours(9), DateTimeKind.Utc)),
				TimeOut = new DateTimeOffset(DateTime.SpecifyKind(date.AddHours(17), DateTimeKind.Utc)),
				Remarks = "Synthetic ESIC attendance"
			});
		await uow.CompleteAsync();
	}
}