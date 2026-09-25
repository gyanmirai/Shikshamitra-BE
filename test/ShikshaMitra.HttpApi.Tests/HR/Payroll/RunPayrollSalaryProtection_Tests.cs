using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp.Application.Dtos;
using Shouldly;

namespace ShikshaMitra.HR.Payroll;

public partial class RunPayrollApi_Tests
{
	[PayrollWorkflowPostgresFact]
	public async Task Take_home_protection_should_restore_the_prorated_target_in_payroll()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		await EnsureActiveDefaultLeavePolicyAsync(new DateTime(2026, 6, 1));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "ProtectedTakeHome",
			dateOfJoining: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
		await AssignSalaryStructureAsync(employee.Id, "ProtectedTakeHome");
		var catalog = await Client.GetFromJsonAsync<SalaryTemplateComponentCatalogDto>(
			"/api/payroll/salary-templates/component-catalog", JsonOptions);
		var basic = catalog!.Items.First(x => x.LineKind == "EARNING" && x.Name.Contains("Basic"));
		var pt = catalog.Items.First(x => x.LineKind == "DEDUCTION" && x.Name.Contains("Professional"));
		var request = new SaveSalaryTemplateDto
		{
			Name = $"Protected take home {Guid.NewGuid():N}",
			DefaultSalaryBasis = "TAKE_HOME", DefaultProtectionPolicy = "PROTECT_TAKE_HOME",
			SampleMonthlyAmount = 50000m, SampleEffectiveMonth = new DateTime(2026, 6, 1),
			IdempotencyKey = Guid.NewGuid().ToString("N"),
			Rules =
			[
				new() { ComponentId = basic.ComponentId, LineKind = "EARNING", SemanticType = "BASIC",
					FormulaType = "PERCENTAGE_OF_GROSS", Value = 100m },
				new() { ComponentId = pt.ComponentId, LineKind = "DEDUCTION", SemanticType = "PROFESSIONAL_TAX",
					FormulaType = "PROFESSIONAL_TAX", DisplayOrder = 1 }
			]
		};
		var sample = await Client.PostAsJsonAsync("/api/payroll/salary-templates/preview", new PreviewSalaryTemplateRequestDto
		{
			Rules = request.Rules, SalaryBasis = request.DefaultSalaryBasis, ProtectionPolicy = request.DefaultProtectionPolicy,
			MonthlyAmount = request.SampleMonthlyAmount, EffectiveMonth = request.SampleEffectiveMonth
		}, JsonOptions);
		sample.StatusCode.ShouldBe(HttpStatusCode.OK, await sample.Content.ReadAsStringAsync());
		request.PreviewToken = (await sample.Content.ReadFromJsonAsync<SalaryTemplatePreviewDto>(JsonOptions))!.PreviewToken;
		var created = await Client.PostAsJsonAsync("/api/payroll/salary-templates", request, JsonOptions);
		created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
		var template = (await created.Content.ReadFromJsonAsync<SalaryTemplateDto>(JsonOptions))!;
		var saved = await Client.PutAsJsonAsync($"/api/payroll/employees/{employee.Id}/compensation", new SaveEmployeeCompensationDto
		{
			TemplateVersionId = template.EditableVersion.Id, SalaryBasis = "TAKE_HOME", ProtectionPolicy = "PROTECT_TAKE_HOME",
			MonthlyAmount = 50000m, EffectiveMonth = new DateTime(2026, 6, 1), IdempotencyKey = Guid.NewGuid().ToString("N")
		}, JsonOptions);
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		await SetEmployeeStatusAsync(employee.Id, 0, new DateOnly(2026, 6, 1));
		await SeedAttendanceOnDateAsync(employee.Id, new DateOnly(2026, 6, 4));
		var preview = await GetPreviewFromQueryAsync($"{BaseUrl}?month=2026-06&page=0&pageSize=100&forceRefresh=true");
		var item = preview.Items.Single(x => x.EmployeeId == employee.Id);
		item.ProrationFactor.ShouldBeGreaterThan(0);
		item.ProrationFactor.ShouldBeLessThan(1);
		((decimal)item.NetPay).ShouldBe(Math.Round(50000m * (decimal)item.ProrationFactor, 2, MidpointRounding.AwayFromZero));
		item.SalaryProtection!.PeriodTarget.ShouldBe((decimal)item.NetPay);
		item.SalaryProtection.Status.ShouldBe("ADJUSTED");
		var review = await Client.PostAsJsonAsync(ReviewUrl, new PayrollRunReviewRequest
		{
			Month = "2026-06", PreviewRunId = LastPreviewId("2026-06"), EmployeeIds = [employee.Id]
		}, JsonOptions);
		review.StatusCode.ShouldBe(HttpStatusCode.OK, await review.Content.ReadAsStringAsync());
		var taxes = await Client.GetFromJsonAsync<PagedResultDto<ProfessionalTaxDto>>("/api/payroll/professional-taxes?page=1&pageSize=100", JsonOptions);
		var slab = taxes!.Items.First(x => x.IsActive && x.FromSalary == 0m);
		var changed = await Client.PutAsJsonAsync($"/api/payroll/professional-taxes/{slab.Id}", new CreateUpdateProfessionalTaxDto
		{
			JurisdictionCode = slab.JurisdictionCode, EffectiveFrom = slab.EffectiveFrom, EffectiveTo = slab.EffectiveTo,
			FromSalary = slab.FromSalary, ToSalary = slab.ToSalary, TaxAmount = 100m, IsActive = true,
			DeductInCessationMonth = slab.DeductInCessationMonth
		}, JsonOptions);
		changed.StatusCode.ShouldBe(HttpStatusCode.OK, await changed.Content.ReadAsStringAsync());
		var refreshed = await GetPreviewFromQueryAsync($"{BaseUrl}?month=2026-06&page=0&pageSize=100&forceRefresh=false");
		var updated = refreshed.Items.Single(x => x.EmployeeId == employee.Id);
		updated.ReviewStatus.ShouldBe("PENDING");
		updated.NetPay.ShouldBe(item.NetPay);
		updated.ProfessionalTaxRuleVersion.ShouldBe(slab.Version + 1);
		updated.SalaryProtection!.Difference.ShouldBe(0m);
	}
}
