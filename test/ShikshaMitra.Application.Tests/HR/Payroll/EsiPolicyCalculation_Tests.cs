using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class EsiPolicyCalculation_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Monthly_payroll_uses_each_assignment_and_effective_version_and_omits_external_management()
	{
		var tenant = Guid.NewGuid();
		using var tenantScope = GetRequiredService<ICurrentTenant>().Change(tenant);
		var first = new EsiPolicy(Guid.NewGuid(), tenant, "First", "FIRST", true);
		var second = new EsiPolicy(Guid.NewGuid(), tenant, "Second", "SECOND");
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<EsiPolicy, Guid>>().InsertManyAsync([first, second]);
			await GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>().InsertManyAsync([
				Rule(first.Id, 1, new(2045, 1, 1), .75m, 3.25m, tenant),
				Rule(first.Id, 2, new(2046, 5, 1), 2m, 5m, tenant),
				Rule(second.Id, 1, new(2045, 1, 1), 1m, 4m, tenant)]);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var one = Revision(first.Id, tenant);
			var two = Revision(second.Id, tenant);
			var external = Revision(null, tenant);
			var assignments = new[] { one, two, external }.ToDictionary(x => x.EmployeeId,
				x => new PayrollCompensationRevisionResolution(x, [Earning(x.EmployeeId)]));
			var components = assignments.Values.SelectMany(x => x.Components).ToLookup(x => x.EmployeeId);
			var resolver = GetRequiredService<PayrollEsiContextResolver>();
			var april = await resolver.ResolveAsync(2046, 4, assignments.Keys.ToArray(), components, assignments);
			april.ContainsKey(external.EmployeeId).ShouldBeFalse();
			var firstResult = Compute(one.EmployeeId, components, april);
			firstResult.EsiCalculation!.EmployeeContribution.ShouldBe(75m);
			firstResult.EsiCalculation.EmployerContribution.ShouldBe(325m);
			var secondResult = Compute(two.EmployeeId, components, april);
			secondResult.EsiCalculation!.EmployeeContribution.ShouldBe(100m);
			secondResult.EsiCalculation.EmployerContribution.ShouldBe(400m);
			var may = await resolver.ResolveAsync(2046, 5, assignments.Keys.ToArray(), components, assignments);
			Compute(one.EmployeeId, components, may).EsiCalculation!.EmployeeContribution.ShouldBe(200m);
			Compute(one.EmployeeId, components, may).EsiCalculation!.EmployerContribution.ShouldBe(500m);
			var trackedSecond = await GetRequiredService<IRepository<EsiPolicy, Guid>>().GetAsync(second.Id);
			trackedSecond.Archive();
			await GetRequiredService<IRepository<EsiPolicy, Guid>>().UpdateAsync(trackedSecond, true);
			(await GetRequiredService<EsiPolicyResolver>().ResolveAsync(second.Id, new(2046, 5, 1))).Rule.Version.ShouldBe(1);
			(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<EsiPolicyResolver>().ResolveAsync(second.Id, new(2046, 5, 1), false))).Code.ShouldBe(HRErrorCodes.EsiPolicyArchived);
			(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<EsiPolicyResolver>().ResolveAsync(second.Id, new(2044, 5, 1)))).Code.ShouldBe(HRErrorCodes.EsiPolicyNotEffective);
		});
		using var otherTenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<EsiPolicyResolver>().ResolveAllAsync(new(2046, 5, 1))).ShouldBeEmpty();
			(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<EsiPolicyResolver>().ResolveAsync(first.Id, new(2046, 5, 1)))).Code.ShouldBe(HRErrorCodes.EsiPolicyNotEffective);
		});
	}
	private static PayrollComputationResult Compute(Guid employeeId, ILookup<Guid, EmployeePayrollComponent> components,
		IReadOnlyDictionary<Guid, PayrollEsiStatutoryContext> contexts) => PayrollComputation.Calculate(0, 0, 1m, 1m,
			components[employeeId].ToList(), new PayrollStatutoryRuleSet(null, [], EmployeeStateInsurance: contexts.GetValueOrDefault(employeeId)));
	private static EmployeePayrollComponent Earning(Guid employeeId) => new(Guid.NewGuid(), employeeId, "Basic Salary", Guid.NewGuid(), null,
		10000m, true, false, null, SalaryComponentType.Fixed, SalaryComponentDependency.None, true, false, PayrollComponentSemanticType.Basic);
	private static EmployeeCompensationRevision Revision(Guid? policyId, Guid tenant)
	{
		var revision = new EmployeeCompensationRevision(Guid.NewGuid(), tenant, Guid.NewGuid(), 1, new(2046, 1, 1));
		revision.SetEsiAssignment(policyId.HasValue ? EsiManagement.InPlatform : EsiManagement.External, policyId, policyId.HasValue ? null : "External provider");
		return revision;
	}
	private static StatutoryRuleVersion Rule(Guid policyId, int version, DateTime start, decimal employee, decimal employer, Guid tenant) =>
		new(Guid.NewGuid(), tenant, "ESI", "IN-GJ", version, start, null, 10, 21000m, 25000m, employee, employer, 50m, 15m,
			StatutoryRoundingPolicy.NextHigherRupee, "https://example.invalid/test", start,
			esiPolicyId: policyId, calculationSettings: new() { EligibilityBasis = "GROSS", CalculationBasis = "GROSS" });
}
