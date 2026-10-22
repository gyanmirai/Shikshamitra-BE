using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollEsiContextResolver(
	EsiPolicyResolver policyResolver,
	EsiPayrollConfigurationResolver configurationResolver,
	EsiEnrollmentResolver enrollmentResolver,
	IRepository<PayrollComponentStatutoryTreatment, Guid> treatmentRepository,
	IRepository<StatutoryOverride, Guid> overrideRepository,
	IEarningComponentRepository earningComponentRepository,
	IDeductionComponentRepository deductionComponentRepository)
	: ITransientDependency
{
	private const string JurisdictionCode = "IN-GJ";
	private const string CanonicalEsiComponentName = "Employee State Insurance (ESI) - Auto";

	public async Task<IReadOnlyDictionary<Guid, PayrollEsiStatutoryContext>> ResolveAsync(
		int year,
		int month,
		IReadOnlyCollection<Guid> employeeIds,
		ILookup<Guid, EmployeePayrollComponent> componentsByEmployee,
		IReadOnlyDictionary<Guid, PayrollCompensationRevisionResolution>? compensation = null)
	{
		var payrollMonth = new DateTime(year, month, 1);
		var monthEnd = payrollMonth.AddMonths(1).AddDays(-1);
		var configuration = await configurationResolver.ResolveAsync(payrollMonth);
		var establishment = configuration?.ToCalculationEstablishment();
		if (establishment is null)
		{
			return new Dictionary<Guid, PayrollEsiStatutoryContext>();
		}

		var effectivePolicies = await policyResolver.ResolveAllAsync(payrollMonth);

		var treatments = (await treatmentRepository.GetListAsync(x =>
			x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance &&
			x.EffectiveFrom <= monthEnd &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= payrollMonth)))
			.GroupBy(x => x.ComponentId)
			.ToDictionary(
				x => x.Key,
				x => x.OrderByDescending(item => item.EffectiveFrom).First().Treatment);
		var requestedEmployeeIds = employeeIds.Distinct().ToHashSet();
		var activeEarnings = await earningComponentRepository.GetListAsync(x => x.IsActive);
		foreach (var earning in activeEarnings)
			treatments.TryAdd(earning.Id, EsiWageDefaults.ByName.GetValueOrDefault(earning.Name, StatutoryWageTreatment.Unclassified));
		foreach (var treatment in configuration!.GetTreatments()) treatments[treatment.Key] = treatment.Value;
		var enrollments = await enrollmentResolver.ResolveAsync(payrollMonth, requestedEmployeeIds);
		var overrides = (await overrideRepository.GetListAsync(x =>
			x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance &&
			requestedEmployeeIds.Contains(x.EmployeeId) &&
			x.EffectiveFrom <= monthEnd &&
			x.EffectiveTo >= payrollMonth))
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(x => x.Key, x => (IReadOnlyList<StatutoryOverride>) x.ToList());
		var canonicalDeduction = await deductionComponentRepository.FindByNameAsync(CanonicalEsiComponentName);
		var configurationSettings = configuration.GetCalculationSettings();
		configurationSettings = configurationSettings with
		{
			EligibilityComponentIsActive = configurationSettings.EligibilityComponentId == null ||
			activeEarnings.Any(x => x.Id == configurationSettings.EligibilityComponentId),
			CalculationComponentIsActive = configurationSettings.CalculationComponentId == null ||
			activeEarnings.Any(x => x.Id == configurationSettings.CalculationComponentId)
		};

		var result = new Dictionary<Guid, PayrollEsiStatutoryContext>();
		foreach (var employeeId in requestedEmployeeIds)
		{
			var revision = compensation?.GetValueOrDefault(employeeId)?.Revision;
			if (compensation != null && revision == null) continue;
			if (revision is { EsiPolicyId: null, EsiManagementMode: EsiManagement.InPlatform }) throw EsiPolicyResolver.NotEffective(null, payrollMonth);
			if (revision?.EsiManagementMode == EsiManagement.External) continue;
			var policy = revision?.EsiPolicyId is { } policyId ? effectivePolicies.GetValueOrDefault(policyId)
				: effectivePolicies.Values.SingleOrDefault(x => x.Policy.IsDefault);
			if (policy == null) throw EsiPolicyResolver.NotEffective(revision?.EsiPolicyId, payrollMonth);
			var rule = policy.Rule;
			var calculationSettings = rule.GetPolicySettings() ?? configurationSettings;
			calculationSettings = calculationSettings with {
				EligibilityComponentIsActive = calculationSettings.EligibilityComponentId == null || activeEarnings.Any(x => x.Id == calculationSettings.EligibilityComponentId),
				CalculationComponentIsActive = calculationSettings.CalculationComponentId == null || activeEarnings.Any(x => x.Id == calculationSettings.CalculationComponentId) };
			var source = componentsByEmployee[employeeId]
				.FirstOrDefault(x => !x.IsEarning && x.SemanticType == PayrollComponentSemanticType.EmployeeStateInsurance)
				?? BuildTransientDeduction(employeeId, canonicalDeduction);
			result[employeeId] = new PayrollEsiStatutoryContext(
				payrollMonth,
				rule,
				establishment,
				enrollments.GetValueOrDefault(employeeId),
				treatments,
				overrides.GetValueOrDefault(employeeId) ?? [],
				source)
			{ CalculationSettings = calculationSettings };
		}

		return result;
	}

	private static EmployeePayrollComponent BuildTransientDeduction(
		Guid employeeId,
		DeductionComponent? component) =>
		new(
			component?.Id ?? Guid.Empty,
			employeeId,
			component?.Name ?? CanonicalEsiComponentName,
			component?.Id,
			component?.Description ?? "System-calculated staff ESI contribution",
			0m,
			isFixed: true,
			isTemp: false,
			applicableMonth: null,
			SalaryComponentType.Fixed,
			SalaryComponentDependency.None,
			isEarning: false,
			usesMasterValue: true,
			PayrollComponentSemanticType.EmployeeStateInsurance,
			statutoryRuleId: null);
}
