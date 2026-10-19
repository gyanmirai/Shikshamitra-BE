using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Status;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Xunit;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class SalarySummaryApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData("GROSS")]
	[InlineData("EMPLOYER_COST")]
	public async Task Salary_preview_and_summary_apply_contributions_before_registration(string basis)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
		var employee = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalarySummary");
		var template = (await GetAndReadAsync<SalaryTemplateListDto>("/api/payroll/salary-templates/active")).Items.First(x => x.EditableVersion.Rules.Any(rule => rule.FormulaType == "EMPLOYEE_PROVIDENT_FUND"));
		var pf = await PayrollTestHelper.CreatePfRuleAsync(Client);
		var coverage = await GetAndReadAsync<PfCoverageSettingsDto>("/api/payroll/pf-rules/coverage");
		await EsiPayrollWorkflowScenario.RequireSuccess(await Client.PutAsJsonAsync("/api/payroll/pf-rules/coverage", new SavePfCoverageDto
		{
			EffectiveMonth = month, IsCovered = true, EvidenceReference = "Synthetic salary-summary coverage",
			ExpectedConfigurationId = coverage.History.FirstOrDefault(x => !x.IsCancelled && x.EffectiveMonth <= month)?.Id, IdempotencyKey = Guid.NewGuid()
		}, JsonOptions));
		var previewInput = new CalculateSalaryTemplateRequestDto
		{
			EmployeeId = employee, EffectiveMonth = month, SalaryBasis = basis, MonthlyAmount = 18000m,
			PfApplicable = true, PfRuleId = pf.Id, PfEnrollment = new() { Membership = "NOT_MEMBER" }
		};
		var previewResponse = await Client.PostAsJsonAsync($"/api/payroll/salary-templates/versions/{template.EditableVersion.Id}/calculate", previewInput, JsonOptions);
		await EsiPayrollWorkflowScenario.RequireSuccess(previewResponse);
		var preview = (await previewResponse.Content.ReadFromJsonAsync<SalaryTemplateCalculationDto>(JsonOptions))!;
		preview.StatutoryApplication!.ProvidentFund.NumberPresent.ShouldBeFalse();
		preview.StatutoryApplication.EmployeeStateInsurance.NumberPresent.ShouldBeFalse();
		preview.StatutoryApplication.ProvidentFund.AppliedEmployeeAmount.ShouldBeGreaterThan(0);
		preview.EmployeeStateInsurance.EmployeeContribution.ShouldBeGreaterThan(0);
		preview.TargetResult.IsExact.ShouldBeTrue();
		preview.StatutorySummary.Count.ShouldBe(3);
		var saveResponse = await Client.PutAsJsonAsync($"/api/payroll/employees/{employee}/compensation", new SaveEmployeeCompensationDto
		{
			TemplateVersionId = template.EditableVersion.Id, EffectiveMonth = month, SalaryBasis = basis, MonthlyAmount = 18000m,
			PfApplicable = true, PfRuleId = pf.Id, PfEnrollment = previewInput.PfEnrollment, IdempotencyKey = Guid.NewGuid().ToString("N")
		}, JsonOptions);
		await EsiPayrollWorkflowScenario.RequireSuccess(saveResponse);
		var url = $"/api/payroll/employees/{employee}/salary-summary?month={month:yyyy-MM}";
		var summary = await GetAndReadAsync<SalarySummaryDto>(url);
		summary.IsComplete.ShouldBeTrue(string.Join(", ", summary.BlockingIssues));
		summary.GrossSalary.ShouldBe(preview.GrossSalary);
		summary.TotalDeductions.ShouldBe(preview.TotalDeductions);
		summary.NetSalary.ShouldBe(preview.NetSalary);
		summary.EmployerCost.ShouldBe(preview.EmployerCost);
		summary.StatutorySummary.Where(x => x.Scheme is "PF" or "ESIC").ShouldAllBe(x => x.RegistrationPending && x.EmployeeAmount > 0m);
		var history = await GetAndReadAsync<CompensationRevisionListDto>($"/api/payroll/employees/{employee}/compensation");
		var frozenHistory = JsonSerializer.Serialize(history, JsonOptions);
		var profileUrl = $"/api/employees/{employee}/payroll/statutory-profile";
		var profile = await GetAndReadAsync<EmployeeStatutoryProfileDto>(profileUrl);
		await EsiPayrollWorkflowScenario.RequireSuccess(await Client.PutAsJsonAsync(profileUrl, new SaveEmployeeStatutoryProfileDto
		{
			Profile = profile.Profile with { PfNumber = "GJ/TEST/123", UanNumber = "123456789012", EsicInsuranceNumber = "1234567890" },
			ConcurrencyStamp = profile.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid(), EffectiveMonth = month.ToString("yyyy-MM")
		}, JsonOptions));
		var registered = await GetAndReadAsync<SalarySummaryDto>(url);
		registered.NetSalary.ShouldBe(summary.NetSalary);
		registered.EmployerCost.ShouldBe(summary.EmployerCost);
		registered.StatutorySummary.ShouldAllBe(x => !x.RegistrationPending);
		JsonSerializer.Serialize(await GetAndReadAsync<CompensationRevisionListDto>($"/api/payroll/employees/{employee}/compensation"), JsonOptions).ShouldBe(frozenHistory);
	}

	[Theory]
	[InlineData(false, 100)]
	[InlineData(true, 0)]
	public async Task Preview_and_saved_summary_follow_effective_professional_tax(bool cessationMonth, decimal expectedTax)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalaryCessation");
		if (cessationMonth)
			await EsiPayrollWorkflowScenario.RequireSuccess(await Client.PutAsJsonAsync($"/api/employees/{employee}/status",
				new EmployeeStatusUpdateRequest { EmployeeStatusType = 2, Remarks = "Synthetic cessation fixture", EffectiveDate = DateOnly.FromDateTime(month.AddMonths(1).AddDays(-1)) }, JsonOptions));
		var ruleId = Guid.NewGuid();
		var oldRuleId = Guid.NewGuid();
		var priorRuleIds = Array.Empty<Guid>();
		async Task SetRuleAsync(bool remove)
		{
			using var scope = Factory.Services.CreateScope();
			using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<ProfessionalTax, Guid>>();
			if (!remove) priorRuleIds = (await repository.GetListAsync(x => x.IsActive && x.JurisdictionCode == "IN-GJ")).Select(x => x.Id).ToArray();
			foreach (var id in priorRuleIds)
			{
				var rule = await repository.GetAsync(id);
				rule.Update(rule.JurisdictionCode, rule.EffectiveFrom, rule.EffectiveTo, rule.FromSalary, rule.ToSalary, rule.TaxAmount, remove, rule.DeductInCessationMonth);
				await repository.UpdateAsync(rule);
			}
			if (remove)
			{
				await repository.DeleteAsync(ruleId);
				await repository.DeleteAsync(oldRuleId);
			}
			else
			{
				await repository.InsertAsync(new ProfessionalTax(oldRuleId, Guid.Parse(TestAuthHandler.TestTenantId), "IN-GJ", month.AddMonths(-1), null, 15000m, null, 200m, deductInCessationMonth: false));
				await repository.InsertAsync(new ProfessionalTax(ruleId, Guid.Parse(TestAuthHandler.TestTenantId), "IN-GJ", month, null, 0m, null, 100m, deductInCessationMonth: false));
			}
			await unit.CompleteAsync();
		}
		await SetRuleAsync(false);
		try
		{
			var template = (await GetAndReadAsync<SalaryTemplateListDto>("/api/payroll/salary-templates/active")).Items.First(x => x.IsDefault);
			var pf = await PayrollTestHelper.CreatePfRuleAsync(Client);
			var input = new SaveEmployeeCompensationDto { TemplateVersionId = template.EditableVersion.Id,
				EffectiveMonth = month, SalaryBasis = "GROSS", MonthlyAmount = 18000m, PfApplicable = true, PfRuleId = pf.Id,
				PfEnrollment = new() { Membership = "EXISTING_MEMBER" }, IdempotencyKey = Guid.NewGuid().ToString("N") };
			var response = await Client.PostAsJsonAsync($"/api/payroll/salary-templates/versions/{template.EditableVersion.Id}/calculate", new CalculateSalaryTemplateRequestDto
			{
				EmployeeId = employee, EffectiveMonth = month, SalaryBasis = input.SalaryBasis, MonthlyAmount = input.MonthlyAmount,
				PfApplicable = input.PfApplicable, PfRuleId = pf.Id, PfEnrollment = input.PfEnrollment
			}, JsonOptions);
			await EsiPayrollWorkflowScenario.RequireSuccess(response);
			var preview = (await response.Content.ReadFromJsonAsync<SalaryTemplateCalculationDto>(JsonOptions))!;
			await EsiPayrollWorkflowScenario.RequireSuccess(await Client.PutAsJsonAsync($"/api/payroll/employees/{employee}/compensation", input, JsonOptions));
			var summary = await GetAndReadAsync<SalarySummaryDto>($"/api/payroll/employees/{employee}/salary-summary?month={month:yyyy-MM}");
			preview.Deductions.Where(x => x.SemanticType == "PROFESSIONAL_TAX").Sum(x => x.Amount).ShouldBe(expectedTax);
			preview.StatutorySummary.Single(x => x.Scheme == "PT").EmployeeAmount.ShouldBe(expectedTax);
			preview.StatutorySummary.Single(x => x.Scheme == "PT").Status.ShouldBe(cessationMonth ? "NOT_APPLICABLE" : "APPLIED");
			summary.NetSalary.ShouldBe(preview.NetSalary);
			summary.TotalDeductions.ShouldBe(preview.TotalDeductions);
		}
		finally { await SetRuleAsync(true); }
	}

	[Fact]
	public async Task Summary_requires_exact_permission_and_employee_scope_and_valid_month()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SalarySummaryAccess");
		var url = $"/api/payroll/employees/{employee}/salary-summary?month={DateTime.UtcNow:yyyy-MM}";
		(await GetAndReadAsync<SalarySummaryDto>(url)).RevisionId.ShouldBeNull();
		using var denied = Factory.CreateClient();
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
		(await denied.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var unscoped = Factory.CreateClient();
		unscoped.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
		(await unscoped.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var otherTenant = Factory.CreateClient();
		otherTenant.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		(await otherTenant.GetAsync(url)).IsSuccessStatusCode.ShouldBeFalse();
		(await Client.GetAsync($"/api/payroll/employees/{employee}/salary-summary?month=2026-13")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}
}
