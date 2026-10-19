using System;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.HR.Reports;
using System.Net;
using Shouldly;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using ShikshaMitra.HR.Payroll.RunPayroll;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class IndependentEsiBasisPayroll_Tests
{
	[PayrollPostgresFact]
	public async Task Independent_sources_reach_payroll_and_preserve_locked_amounts()
	{
		using var factory = new PayrollPostgresTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		var now = DateTime.UtcNow;
		var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		await workflow.ConfigureAsync(false);
		var pf = (await api.GetFromJsonAsync<PfCoverageSettingsDto>("/api/payroll/pf-rules/coverage", json))!;
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync("/api/payroll/pf-rules/coverage", new SavePfCoverageDto
		{
			EffectiveMonth = month,
			IsCovered = false,
			EvidenceReference = "Disposable ESIC-only school fixture",
			ExpectedConfigurationId = pf.History.FirstOrDefault(x => !x.IsCancelled && x.EffectiveMonth <= month)?.Id,
			IdempotencyKey = Guid.NewGuid()
		}, json));
		var basic = await PayrollTestHelper.CreateEarningComponentAsync(api, "IndependentSchoolBasic");
		var hra = await PayrollTestHelper.CreateEarningComponentAsync(api, "IndependentHra");
		var templateInput = new SaveSalaryTemplateDto
		{
			Name = "Independent ESIC " + Guid.NewGuid().ToString("N"),
			DefaultSalaryBasis = "GROSS",
			SampleMonthlyAmount = 16000m,
			SampleEffectiveMonth = month,
			IdempotencyKey = Guid.NewGuid().ToString("N"),
			Rules = [
				new() { Code = "BASIC", ComponentId = basic.Id, SemanticType = "BASIC", FormulaType = "PERCENTAGE_OF_GROSS", Value = 60m, DisplayOrder = 1 },
				new() { Code = "HRA", ComponentId = hra.Id, SemanticType = "ORDINARY", FormulaType = "PERCENTAGE_OF_GROSS", Value = 40m, DisplayOrder = 2 }
			]
		};
		var previewResponse = await api.PostAsJsonAsync("/api/payroll/salary-templates/preview", new PreviewSalaryTemplateRequestDto
		{
			Rules = templateInput.Rules,
			SalaryBasis = "GROSS",
			MonthlyAmount = 16000m,
			EffectiveMonth = month
		}, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(previewResponse);
		templateInput.PreviewToken = (await previewResponse.Content.ReadFromJsonAsync<SalaryTemplatePreviewDto>(json))!.PreviewToken;
		var templateResponse = await api.PostAsJsonAsync("/api/payroll/salary-templates", templateInput, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(templateResponse);
		var template = (await templateResponse.Content.ReadFromJsonAsync<SalaryTemplateDto>(json))!;
		await ConfigureAsync("GROSS", "COMPONENT");
		var lockedEmployee = await CreateEmployeeAsync("LockedIndependentEsi");
		var openEmployee = await CreateEmployeeAsync("OpenIndependentEsi");
		var first = await workflow.CalculateAsync(month);
		var lockedBefore = await workflow.EmployeeAsync(first.Id, lockedEmployee);
		var openBefore = await workflow.EmployeeAsync(first.Id, openEmployee);
		lockedBefore.EmployeeStateInsuranceWages.ShouldBe(9600);
		openBefore.EmployeeStateInsuranceWages.ShouldBe(9600);
		lockedBefore.TotalDeduction.ShouldBe(72);
		lockedBefore.EmployerContributionTotal.ShouldBe(312);
		lockedBefore.GrossSalary.ShouldBe(16000);
		await workflow.ReviewAsync(first.Id, lockedEmployee, month);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(first.Id, lockedEmployee, month));
		var locked = await workflow.EmployeeAsync(first.Id, lockedEmployee);
		var frozen = JsonSerializer.Serialize(locked, json);
		await ConfigureAsync("COMPONENT", "GROSS");
		var second = await workflow.CalculateAsync(month);
		var changed = await workflow.EmployeeAsync(second.Id, openEmployee);
		changed.EmployeeStateInsuranceWages.ShouldBe(16000);
		changed.TotalDeduction.ShouldBe(120);
		changed.EmployerContributionTotal.ShouldBe(520);
		changed.GrossSalary.ShouldBe(16000);
		changed.NetPay.ShouldBe(15880);
		using var trace = JsonDocument.Parse(changed.EsiCalculationDetailsJson!);
		trace.RootElement.GetProperty("CalculationSettings").GetProperty("EligibilityBasis").GetString().ShouldBe("COMPONENT");
		trace.RootElement.GetProperty("CalculationSettings").GetProperty("CalculationBasis").GetString().ShouldBe("GROSS");
		JsonSerializer.Serialize(await workflow.EmployeeAsync(first.Id, lockedEmployee), json).ShouldBe(frozen);


		var profileUrl = $"/api/employees/{openEmployee}/payroll/statutory-profile";
		var profile = (await api.GetFromJsonAsync<EmployeeStatutoryProfileDto>(profileUrl, json))!;
		var change = new SaveEmployeeStatutoryProfileDto { Profile = profile.Profile with { ApplyEsic = false, EsicDisabledReason = "School registration dispute" },
			EffectiveMonth = month.ToString("yyyy-MM"), ConcurrencyStamp = profile.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid(), UpdatePf = false };
		var changedProfileResponse = await api.PutAsJsonAsync(profileUrl, change, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(changedProfileResponse);
		var changedProfile = (await changedProfileResponse.Content.ReadFromJsonAsync<EmployeeStatutoryProfileDto>(json))!;
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync(profileUrl, change, json));
		(await api.PutAsJsonAsync(profileUrl, withKey(Guid.NewGuid()), json)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var suppressedRun = await workflow.CalculateAsync(month);
		var suppressed = await workflow.EmployeeAsync(suppressedRun.Id, openEmployee);
		suppressed.EmployeeStateInsuranceWages.ShouldBe(16000);
		suppressed.TotalDeduction.ShouldBe(0); suppressed.EmployerContributionTotal.ShouldBe(0); suppressed.GrossSalary.ShouldBe(16000);
		suppressed.IssueCodes.ShouldContain("ESI_APPLY_DISABLED");
		var stillLocked = await workflow.EmployeeAsync(suppressedRun.Id, lockedEmployee);
		stillLocked.StatutoryApplicationJson.ShouldBe(locked.StatutoryApplicationJson);
		stillLocked.EsiCalculationDetailsJson.ShouldBe(locked.EsiCalculationDetailsJson);
		stillLocked.GrossSalary.ShouldBe(locked.GrossSalary); stillLocked.TotalDeduction.ShouldBe(locked.TotalDeduction);
		stillLocked.NetPay.ShouldBe(locked.NetPay); stillLocked.EmployerContributionTotal.ShouldBe(locked.EmployerContributionTotal);
		change = new SaveEmployeeStatutoryProfileDto { Profile = changedProfile.Profile with { ApplyEsic = true, EsicInsuranceNumber = "0", EsicDisabledReason = null },
			EffectiveMonth = month.ToString("yyyy-MM"), ConcurrencyStamp = changedProfile.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid(), UpdatePf = false };
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync(profileUrl, change, json));
		// The coverage endpoint cannot overwrite canonical identifiers using a stale legacy payload.
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync(
			$"/api/payroll/statutory-schemes/esi/employees/{openEmployee}/enrollment",
			new { Status = "PendingReview", WageCeilingCategory = "Standard", InsuranceNumber = "STALE123456" }, json));
		(await api.GetFromJsonAsync<EmployeeStatutoryProfileDto>(profileUrl, json))!.Profile.EsicInsuranceNumber.ShouldBeNull();
		var pendingRun = await workflow.CalculateAsync(month);
		var pending = await workflow.EmployeeAsync(pendingRun.Id, openEmployee);
		pending.IssueCodes.ShouldContain("ESI_NUMBER_MISSING"); pending.TotalDeduction.ShouldBe(120); pending.EmployerContributionTotal.ShouldBe(520);
		var previewRequest = new HrReportRequestDto { ReportType = "School payroll register", DataSource = HrReportDataSources.PayrollRegister,
			Filters = new() { Month = month.ToString("yyyy-MM"), IncludeDrafts = true }, PageSize = 100 };
		var registerResponse = await api.PostAsJsonAsync("/api/hr/reports/preview", previewRequest, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(registerResponse);
		var register = (await registerResponse.Content.ReadFromJsonAsync<HrReportPreviewDto>(json))!;
		register.Rows.Count.ShouldBeGreaterThanOrEqualTo(2);
		register.QueryWarnings.ShouldContain(x => x.Contains("Preview includes"));
		var statementResponse = await api.PostAsJsonAsync("/api/hr/reports/preview", new HrReportRequestDto { ReportType = "Employee salary statement",
			DataSource = HrReportDataSources.PayrollStatement, Filters = new() { EmployeeIds = [lockedEmployee],
				FromDate = DateOnly.FromDateTime(month), ToDate = DateOnly.FromDateTime(month.AddMonths(2)) }, PageSize = 100 }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(statementResponse);
		var statement = (await statementResponse.Content.ReadFromJsonAsync<HrReportPreviewDto>(json))!;
		statement.Rows.Count.ShouldBe(3); statement.Rows.Count(x => x["status"] == "Unavailable").ShouldBe(2);
		statement.Rows.Where(x => x["status"] == "Unavailable").ShouldAllBe(x => x["netPay"] == null);
		statement.Totals["netPay"].ShouldBe(15928);
		await workflow.ReviewAsync(pendingRun.Id, openEmployee, month);
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var review = await scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunReview, Guid>>().GetAsync(x => x.EmployeeId == openEmployee && x.MonthYear == month.ToString("yyyy-MM"));
			JsonSerializer.Serialize(review.ExtraProperties).ShouldContain("ESI_NUMBER_MISSING");
			await unit.CompleteAsync();
		}
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(pendingRun.Id, openEmployee, month));
		(await workflow.EmployeeAsync(pendingRun.Id, openEmployee)).IsLocked.ShouldBeTrue();

		var lockedProfileUrl = $"/api/employees/{lockedEmployee}/payroll/statutory-profile";
		var lockedProfile = (await api.GetFromJsonAsync<EmployeeStatutoryProfileDto>(lockedProfileUrl, json))!;
		var delayedResponse = await api.PutAsJsonAsync(lockedProfileUrl, new SaveEmployeeStatutoryProfileDto
		{ Profile = lockedProfile.Profile with { ApplyEsic = false, EsicDisabledReason = "Future registration change" }, EffectiveMonth = month.ToString("yyyy-MM"),
			ConcurrencyStamp = lockedProfile.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid(), UpdatePf = false }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(delayedResponse);
		(await delayedResponse.Content.ReadFromJsonAsync<EmployeeStatutoryProfileDto>(json))!.EffectiveMonth.ShouldBe(month.AddMonths(1).ToString("yyyy-MM"));

		SaveEmployeeStatutoryProfileDto withKey(Guid key) => new() { Profile = change.Profile, EffectiveMonth = change.EffectiveMonth,
			ConcurrencyStamp = change.ConcurrencyStamp, IdempotencyKey = key, UpdatePf = change.UpdatePf, UpdateEsic = change.UpdateEsic };

		async Task ConfigureAsync(string eligibilityBasis, string calculationBasis)
		{
			var settings = (await api.GetFromJsonAsync<EsiConfigurationSettingsDto>("/api/payroll/statutory-schemes/esi/configuration", json))!;
			await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync("/api/payroll/statutory-schemes/esi/configuration", new SaveEsiConfigurationDto
			{
				Enabled = true,
				EffectiveMonth = month.ToString("yyyy-MM"),
				ConcurrencyStamp = settings.Current!.ConcurrencyStamp,
				IdempotencyKey = Guid.NewGuid(),
				CalculationSettings = new() { EligibilityBasis = eligibilityBasis, CalculationBasis = calculationBasis, EligibilityComponentId = eligibilityBasis == "COMPONENT" ? basic.Id : null, CalculationComponentId = calculationBasis == "COMPONENT" ? basic.Id : null },
				Treatments = [new() { ComponentId = basic.Id, Treatment = "Included" }, new() { ComponentId = hra.Id, Treatment = "SpecifiedExclusion" }]
			}, json));
		}
		async Task<Guid> CreateEmployeeAsync(string name)
		{
			var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, name, dateOfJoining: month);
			await workflow.EnrollAsync(employee.Id, "1234567890");
			await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync($"/api/payroll/employees/{employee.Id}/compensation", new SaveEmployeeCompensationDto
			{
				TemplateVersionId = template.EditableVersion.Id,
				SalaryBasis = "GROSS",
				MonthlyAmount = 16000m,
				EffectiveMonth = month,
				PfApplicable = false,
				IdempotencyKey = Guid.NewGuid().ToString("N")
			}, json));
			await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
			await workflow.SeedAttendanceAsync(employee.Id, month);
			return employee.Id;
		}
	}
}