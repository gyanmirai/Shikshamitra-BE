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
	IRepository<StatutoryRuleVersion, Guid> ruleRepository,
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
		ILookup<Guid, EmployeePayrollComponent> componentsByEmployee)
	{
		var payrollMonth = new DateTime(year, month, 1);
		var monthEnd = payrollMonth.AddMonths(1).AddDays(-1);
		var configuration = await configurationResolver.ResolveAsync(payrollMonth);
		var establishment = configuration?.ToCalculationEstablishment();
		if (establishment is null)
		{
			return new Dictionary<Guid, PayrollEsiStatutoryContext>();
		}

		var rule = (await ruleRepository.GetListAsync(x =>
			x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance &&
			x.JurisdictionCode == JurisdictionCode &&
			x.IsActive &&
			x.EffectiveFrom <= monthEnd &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= payrollMonth)))
			.OrderByDescending(x => x.EffectiveFrom)
			.ThenByDescending(x => x.Version)
			.FirstOrDefault();
		if (rule is null)
		{
			throw new BusinessException(HRErrorCodes.StatutoryRuleNotFound);
		}

		var treatments = (await treatmentRepository.GetListAsync(x =>
			x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance &&
			x.EffectiveFrom <= monthEnd &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= payrollMonth)))
			.GroupBy(x => x.ComponentId)
			.ToDictionary(
				x => x.Key,
				x => x.OrderByDescending(item => item.EffectiveFrom).First().Treatment);
		var requestedEmployeeIds = employeeIds.Distinct().ToHashSet();
		foreach (var earning in await earningComponentRepository.GetListAsync(x => x.IsActive))
			treatments.TryAdd(earning.Id, EsiWageDefaults.ByName.GetValueOrDefault(earning.Name, StatutoryWageTreatment.Unclassified));
		foreach (var treatment in configuration!.GetTreatments()) treatments[treatment.Key] = treatment.Value;
		var enrollments = await enrollmentResolver.ResolveAsync(payrollMonth, requestedEmployeeIds);
		var overrides = (await overrideRepository.GetListAsync(x =>
			x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance &&
			requestedEmployeeIds.Contains(x.EmployeeId) &&
			x.EffectiveFrom <= monthEnd &&
			x.EffectiveTo >= payrollMonth))
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(x => x.Key, x => (IReadOnlyList<StatutoryOverride>)x.ToList());
		var canonicalDeduction = await deductionComponentRepository.FindByNameAsync(CanonicalEsiComponentName);

		var result = new Dictionary<Guid, PayrollEsiStatutoryContext>();
		foreach (var employeeId in requestedEmployeeIds)
		{
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
				source);
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
			component?.Description ?? "System-calculated employee ESI contribution",
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
