using System;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Xunit;

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
		await ConfigureAsync("GROSS", "BASIC_COMPONENT");
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
		await ConfigureAsync("BASIC_COMPONENT", "GROSS");
		var second = await workflow.CalculateAsync(month);
		var changed = await workflow.EmployeeAsync(second.Id, openEmployee);
		changed.EmployeeStateInsuranceWages.ShouldBe(16000);
		changed.TotalDeduction.ShouldBe(120);
		changed.EmployerContributionTotal.ShouldBe(520);
		changed.GrossSalary.ShouldBe(16000);
		changed.NetPay.ShouldBe(15880);
		using var trace = JsonDocument.Parse(changed.EsiCalculationDetailsJson!);
		trace.RootElement.GetProperty("CalculationSettings").GetProperty("EligibilityBasis").GetString().ShouldBe("BASIC_COMPONENT");
		trace.RootElement.GetProperty("CalculationSettings").GetProperty("CalculationBasis").GetString().ShouldBe("GROSS");
		JsonSerializer.Serialize(await workflow.EmployeeAsync(first.Id, lockedEmployee), json).ShouldBe(frozen);

		async Task ConfigureAsync(string eligibilityBasis, string calculationBasis)
		{
			var settings = (await api.GetFromJsonAsync<EsiConfigurationSettingsDto>("/api/payroll/statutory-schemes/esi/configuration", json))!;
			await EsiPayrollWorkflowScenario.RequireSuccess(await api.PutAsJsonAsync("/api/payroll/statutory-schemes/esi/configuration", new SaveEsiConfigurationDto
			{
				Enabled = true,
				EffectiveMonth = month.ToString("yyyy-MM"),
				ConcurrencyStamp = settings.Current!.ConcurrencyStamp,
				IdempotencyKey = Guid.NewGuid(),
				CalculationSettings = new() { EligibilityBasis = eligibilityBasis, CalculationBasis = calculationBasis, BasicComponentId = basic.Id },
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