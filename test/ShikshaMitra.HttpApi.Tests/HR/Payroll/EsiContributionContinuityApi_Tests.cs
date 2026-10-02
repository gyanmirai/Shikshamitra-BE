using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
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
public class EsiContributionContinuityApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData(SalarySlipStatus.Finalized)]
	[InlineData(SalarySlipStatus.Paid)]
	public async Task Above_ceiling_salary_keeps_contributions_after_finalized_unlocked_payroll(SalarySlipStatus status)
	{
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		// Use the next complete period, after all current development-fixture configuration changes.
		var (_, currentPeriodEnd) = EsiStatutoryCalculator.GetContributionPeriod(DateTime.UtcNow);
		var periodStart = currentPeriodEnd.AddDays(1);
		var priorMonth = periodStart.ToString("yyyy-MM", CultureInfo.InvariantCulture);
		var employeeId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "EsiContinuity");
		var snapshotId = Guid.NewGuid();
		var slipId = Guid.NewGuid();
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
			var ruleId = Guid.NewGuid();
			var snapshot = new PayrollRunItemSnapshot(snapshotId, tenantId, priorMonth, employeeId)
			{
				EmployeeStateInsuranceRuleId = ruleId,
				EmployeeStateInsuranceCoverageDecision = "WITHIN_WAGE_CEILING",
				EmployeeStateInsuranceNumber = "1234567890"
			};
			await scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(snapshot);
			var constructor = typeof(SalarySlip).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
				.Single(x => x.GetParameters().Length == 8);
			var slip = (SalarySlip) constructor.Invoke([slipId, tenantId, employeeId, priorMonth,
				new List<SalarySlipLineItem>(), new List<SalarySlipLineItem>(), DateTime.UtcNow, null]);
			typeof(SalarySlip).GetProperty(nameof(SalarySlip.EmployeeStateInsuranceRuleId))!.SetValue(slip, ruleId);
			typeof(SalarySlip).GetProperty(nameof(SalarySlip.EmployeeStateInsuranceWages))!.SetValue(slip, 20000m);
			typeof(SalarySlip).GetProperty(nameof(SalarySlip.EmployeeStateInsuranceCoverageDecision))!.SetValue(slip, "WITHIN_WAGE_CEILING");
			typeof(SalarySlip).GetProperty(nameof(SalarySlip.EmployeeStateInsuranceNumber))!.SetValue(slip, "1234567890");
			slip.FinalizeSlip();
			if (status == SalarySlipStatus.Paid)
				typeof(SalarySlip).GetProperty(nameof(SalarySlip.Status))!.SetValue(slip, status);
			await scope.ServiceProvider.GetRequiredService<IRepository<SalarySlip, Guid>>().InsertAsync(slip);
			await uow.CompleteAsync();
		}
		var templates = (await Client.GetFromJsonAsync<SalaryTemplateListDto>("/api/payroll/salary-templates/active", JsonOptions))!;
		var template = templates.Items.First(x => x.IsDefault);
		var input = new CalculateSalaryTemplateRequestDto
		{
			EmployeeId = employeeId,
			SalaryBasis = "GROSS",
			MonthlyAmount = 30000m,
			EffectiveMonth = periodStart.AddMonths(1),
			PfApplicable = false
		};
		var url = $"/api/payroll/salary-templates/versions/{template.EditableVersion.Id}/calculate";
		var response = await Client.PostAsJsonAsync(url, input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var calculation = (await response.Content.ReadFromJsonAsync<SalaryTemplateCalculationDto>(JsonOptions))!;
		calculation.EmployeeStateInsurance.IsConfigured.ShouldBeTrue(await response.Content.ReadAsStringAsync());
		calculation.EmployeeStateInsurance.CoverageDecision.ShouldBe("CONTRIBUTION_PERIOD_CONTINUATION");
		calculation.EmployeeStateInsurance.EmployeeContribution.ShouldBe(225m);
		calculation.EmployeeStateInsurance.EmployerContribution.ShouldBe(975m);
		input.EffectiveMonth = periodStart.AddMonths(6);
		var next = await Client.PostAsJsonAsync(url, input, JsonOptions);
		next.StatusCode.ShouldBe(HttpStatusCode.OK, await next.Content.ReadAsStringAsync());
		var nextCalculation = (await next.Content.ReadFromJsonAsync<SalaryTemplateCalculationDto>(JsonOptions))!;
		nextCalculation.EmployeeStateInsurance.CoverageDecision.ShouldBe("ABOVE_WAGE_CEILING");
		nextCalculation.EmployeeStateInsurance.EmployeeContribution.ShouldBe(0m);
		nextCalculation.EmployeeStateInsurance.EmployerContribution.ShouldBe(0m);
		using var readScope = Factory.Services.CreateScope();
		using var tenant = readScope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var readUow = readScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		(await readScope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().GetAsync(snapshotId)).IsLocked.ShouldBeFalse();
		var original = await readScope.ServiceProvider.GetRequiredService<IRepository<SalarySlip, Guid>>().GetAsync(slipId);
		original.Status.ShouldBe(status);
		original.EmployeeStateInsuranceWages.ShouldBe(20000m);
		await readUow.CompleteAsync();
	}
}