using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll;

public partial class RunPayrollApi_Tests
{
	[PayrollWorkflowPostgresFact]
	public async Task Ctc_protection_rebalances_partial_month_after_pf_change_and_requires_fresh_review()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		var effectiveMonth = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
		await EnsureActiveDefaultLeavePolicyAsync(effectiveMonth);
		// Seed historical coverage in the disposable test database because the coverage API rejects backdating.
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var repo = scope.ServiceProvider.GetRequiredService<IRepository<PfCoverageConfiguration, Guid>>();
			await repo.InsertAsync(new PfCoverageConfiguration(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId),
				effectiveMonth, true, "Disposable CTC protection test", Guid.NewGuid().ToString("N")));
			await uow.CompleteAsync();
		}
		var pf = await PayrollTestHelper.CreatePfRuleAsync(Client, "ProtectionPf");
		var pfRule = (await Client.GetFromJsonAsync<PfRuleResponseDto>($"/api/payroll/pf-rules/{pf.Id}", JsonOptions))!;
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "ProtectedCtc", dateOfJoining: effectiveMonth);
		await AssignSalaryStructureAsync(employee.Id, "ProtectedCtc");
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var profiles = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeStatutoryProfileVersion, Guid>>();
			await profiles.InsertAsync(new EmployeeStatutoryProfileVersion(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId),
				employee.Id, effectiveMonth, new PayrollStatutoryProfile { ApplyPf = true, PfNumber = "GJ/CTC/123" },
				Guid.NewGuid(), "Synthetic CTC PF profile"));
			await uow.CompleteAsync();
		}
		var templates = (await Client.GetFromJsonAsync<SalaryTemplateListDto>("/api/payroll/salary-templates/active", JsonOptions))!;
		var template = templates.Items.First(x => x.EditableVersion.Rules.Any(rule => rule.FormulaType == "EMPLOYEE_PROVIDENT_FUND"));
		var compensationRequest = await PayrollTestHelper.BindEsiPolicyVersionAsync(Client, new SaveEmployeeCompensationDto
		{
			TemplateVersionId = template.EditableVersion.Id, SalaryBasis = "EMPLOYER_COST", MonthlyAmount = 50000m,
			EffectiveMonth = effectiveMonth, PfApplicable = true, PfRuleId = pf.Id,
			PfEnrollment = new() { Membership = "EXISTING_MEMBER" },
			EsiPolicyId = template.EditableVersion.DefaultEsiPolicyId, IdempotencyKey = Guid.NewGuid().ToString("N")
		});
		var saved = await Client.PutAsJsonAsync($"/api/payroll/employees/{employee.Id}/compensation", compensationRequest, JsonOptions);
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		var approved = (await saved.Content.ReadFromJsonAsync<CompensationRevisionDto>(JsonOptions))!;
		approved.GrossSalary.ShouldBeLessThan(approved.EmployerCost, "The approved quote must include employer PF.");
		await ActivateCalculationFixtureAsync(employee.Id);
		await SeedAttendanceOnDateAsync(employee.Id, new DateOnly(2026, 6, 10));

		var preview = await GetPreviewFromQueryAsync($"{BaseUrl}?month=2026-06&page=0&pageSize=100&forceRefresh=true");
		var item = preview.Items.Single(x => x.EmployeeId == employee.Id);
		item.ProrationFactor.ShouldBeGreaterThan(0d);
		item.ProrationFactor.ShouldBeLessThan(1d);
		var target = GrossEarningAllocation.Money(50000m * (decimal) item.PayableDays / (decimal) item.BasisDays);
		item.SalaryProtection.ShouldNotBeNull(string.Join(", ", item.IssueCodes));
		item.SalaryProtection.PeriodTarget.ShouldBe(target);
		Math.Abs(item.SalaryProtection.Achieved - target).ShouldBeLessThanOrEqualTo(0.01m);
		var firstPreviewId = LastPreviewId("2026-06");
		var review = new PayrollRunReviewRequest { Month = "2026-06", PreviewRunId = firstPreviewId, EmployeeIds = [employee.Id] };
		(await Client.PostAsJsonAsync(ReviewUrl, review, JsonOptions)).EnsureSuccessStatusCode();

		var changed = await Client.PutAsJsonAsync($"/api/payroll/pf-rules/{pf.Id}", new CreateUpdateProvidentFundDto
		{
			Name = pfRule.Name, Code = pfRule.Code, EmployeePercentage = 12m, EmployerPercentage = 15m
		}, JsonOptions);
		changed.StatusCode.ShouldBe(HttpStatusCode.OK, await changed.Content.ReadAsStringAsync());
		var staleReview = await Client.PostAsJsonAsync(ReviewUrl, review, JsonOptions);
		staleReview.IsSuccessStatusCode.ShouldBeFalse();

		var refreshed = await GetPreviewFromQueryAsync($"{BaseUrl}?month=2026-06&page=0&pageSize=100&forceRefresh=false");
		var revised = refreshed.Items.Single(x => x.EmployeeId == employee.Id);
		revised.ReviewStatus.ShouldBe("PENDING");
		revised.GrossSalary.ShouldBeLessThan(item.GrossSalary);
		revised.CompensationRevisionId.ShouldBe(approved.Id);
		revised.SalaryProtection!.PeriodTarget.ShouldBe(target);
		Math.Abs(revised.SalaryProtection.Achieved - target).ShouldBeLessThanOrEqualTo(0.01m);
		revised.IssueCodes.ShouldNotContain(IssueCodes.CompensationTargetVariance);
		var draft = await EnsureCalculationDraftAsync(employee.Id, "2026-06");
		SalaryProtectionSnapshot.Read(draft).ShouldBe(revised.SalaryProtection);
	}
}
