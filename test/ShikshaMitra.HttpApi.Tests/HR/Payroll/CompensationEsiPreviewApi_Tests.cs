using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using ShikshaMitra;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class CompensationEsiPreviewApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Fact]
	public async Task Save_rejects_changed_or_missing_policy_versions_without_creating_a_revision_and_allows_refresh_and_retry()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var coverage = scope.ServiceProvider.GetRequiredService<IRepository<PfCoverageConfiguration, Guid>>();
			await coverage.InsertAsync(new PfCoverageConfiguration(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId),
				new(2045, 1, 1), false, "Synthetic ESIC review school", Guid.NewGuid().ToString("N")));
			await uow.CompleteAsync();
		}
		var employee = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "EsicPreviewRaceReview");
		const string url = "/api/payroll/statutory-schemes/esi/policies";
		var create = new CreateEsiPolicyDto
		{
			Name = "Review " + Guid.NewGuid().ToString("N"),
			Code = "REVIEW_" + Guid.NewGuid().ToString("N").ToUpperInvariant(),
			EffectiveFrom = new(2045, 1, 1),
			StandardWageCeiling = 100000m,
			DisabilityWageCeiling = 125000m,
			SourceUrl = "https://example.invalid/review",
			SourceVerifiedOn = new(2026, 10, 1),
			IdempotencyKey = Guid.NewGuid(),
			CalculationSettings = new() { EligibilityBasis = "GROSS", CalculationBasis = "GROSS" }
		};
		var policy = await PostAndReadAsync<EsiPolicyDto>(url, create);
		var catalog = await GetAndReadAsync<SalaryTemplateComponentCatalogDto>("/api/payroll/salary-templates/component-catalog");
		var earning = catalog.Items.First(x => x.LineKind == "EARNING");
		var previewInput = new PreviewSalaryTemplateRequestDto
		{
			SalaryBasis = "GROSS",
			MonthlyAmount = 50000m,
			EffectiveMonth = new(2046, 7, 1),
			EsiPolicyId = policy.Id,
			Rules = [new() { ComponentId = earning.ComponentId, SemanticType = "BASIC", LineKind = "EARNING", FormulaType = "PERCENTAGE_OF_GROSS", Value = 100m }]
		};
		var preview = await PostAndReadAsync<SalaryTemplatePreviewDto>("/api/payroll/salary-templates/preview", previewInput);
		var template = await PostAndReadAsync<SalaryTemplateDto>("/api/payroll/salary-templates", new SaveSalaryTemplateDto
		{
			Name = "Review template " + Guid.NewGuid().ToString("N"),
			DefaultSalaryBasis = "GROSS",
			DefaultEsiPolicyId = policy.Id,
			Rules = previewInput.Rules,
			SampleMonthlyAmount = 50000m,
			SampleEffectiveMonth = previewInput.EffectiveMonth,
			PreviewToken = preview.PreviewToken,
			IdempotencyKey = Guid.NewGuid().ToString("N")
		});
		var employeePreview = await PostAndReadAsync<SalaryTemplateCalculationDto>($"/api/payroll/salary-templates/versions/{template.EditableVersion.Id}/calculate", new CalculateSalaryTemplateRequestDto
		{
			EmployeeId = employee,
			SalaryBasis = "GROSS",
			MonthlyAmount = 50000m,
			EffectiveMonth = previewInput.EffectiveMonth,
			EsiPolicyId = policy.Id,
			PfApplicable = false,
			PfEnrollment = new() { Membership = "NOT_MEMBER" }
		});
		policy = (await GetAndReadAsync<List<EsiPolicyDto>>(url)).Single(x => x.Id == policy.Id);
		await PostAndReadAsync<EsiPolicyDto>($"{url}/{policy.Id}/versions", new EsiPolicyVersionInput
		{
			EffectiveFrom = new(2046, 5, 1),
			EmployeePercentage = 1m,
			EmployerPercentage = 4m,
			StandardWageCeiling = 100000m,
			DisabilityWageCeiling = 125000m,
			SourceUrl = create.SourceUrl,
			SourceVerifiedOn = create.SourceVerifiedOn,
			IdempotencyKey = Guid.NewGuid(),
			ConcurrencyStamp = policy.ConcurrencyStamp,
			CalculationSettings = create.CalculationSettings
		});
		var input = new SaveEmployeeCompensationDto
		{
			TemplateVersionId = template.EditableVersion.Id,
			SalaryBasis = "GROSS",
			MonthlyAmount = 50000m,
			EffectiveMonth = previewInput.EffectiveMonth,
			EsiPolicyId = policy.Id,
			ExpectedEsiPolicyVersionId = employeePreview.EmployeeStateInsurance.RuleId,
			PfApplicable = false,
			PfEnrollment = new() { Membership = "NOT_MEMBER" },
			IdempotencyKey = Guid.NewGuid().ToString("N")
		};
		var employeeUrl = $"/api/payroll/employees/{employee}/compensation";
		var missingVersion = input.ExpectedEsiPolicyVersionId;
		input.ExpectedEsiPolicyVersionId = null;
		Assert.Equal(HttpStatusCode.BadRequest, (await Client.PutAsJsonAsync(employeeUrl, input)).StatusCode);
		input.ExpectedEsiPolicyVersionId = missingVersion;
		var stale = await Client.PutAsJsonAsync(employeeUrl, input);
		Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
		Assert.Contains("HR:Payroll:CompensationPreviewStale", await stale.Content.ReadAsStringAsync());
		Assert.Empty((await GetAndReadAsync<CompensationRevisionListDto>(employeeUrl)).Items);
		var refreshed = await PostAndReadAsync<SalaryTemplateCalculationDto>($"/api/payroll/salary-templates/versions/{template.EditableVersion.Id}/calculate", new CalculateSalaryTemplateRequestDto
		{
			EmployeeId = employee,
			SalaryBasis = input.SalaryBasis,
			MonthlyAmount = input.MonthlyAmount,
			EffectiveMonth = input.EffectiveMonth,
			EsiPolicyId = policy.Id,
			PfApplicable = false,
			PfEnrollment = input.PfEnrollment
		});
		input.ExpectedEsiPolicyVersionId = refreshed.EmployeeStateInsurance.RuleId;
		var fresh = await Client.PutAsJsonAsync(employeeUrl, input);
		Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
		var result = (await fresh.Content.ReadFromJsonAsync<CompensationRevisionDto>(JsonOptions))!;
		Assert.Equal(49500m, result.NetSalary);
		var retry = await Client.PutAsJsonAsync(employeeUrl, input);
		Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
		Assert.Equal(result.Id, (await retry.Content.ReadFromJsonAsync<CompensationRevisionDto>(JsonOptions))!.Id);
		input.ExpectedEsiPolicyVersionId = employeePreview.EmployeeStateInsurance.RuleId;
		Assert.Equal(HttpStatusCode.Conflict, (await Client.PutAsJsonAsync(employeeUrl, input)).StatusCode);
		input.EsiManagementMode = EsiManagement.External;
		input.EsiPolicyId = null;
		input.ExpectedEsiPolicyVersionId = null;
		input.EsiExternalReason = "Managed by an external payroll provider";
		input.ExpectedRevision = result.RevisionNumber;
		input.IdempotencyKey = Guid.NewGuid().ToString("N");
		var external = await Client.PutAsJsonAsync(employeeUrl, input);
		Assert.Equal(HttpStatusCode.OK, external.StatusCode);
		Assert.Equal(50000m, (await external.Content.ReadFromJsonAsync<CompensationRevisionDto>(JsonOptions))!.NetSalary);
	}
}