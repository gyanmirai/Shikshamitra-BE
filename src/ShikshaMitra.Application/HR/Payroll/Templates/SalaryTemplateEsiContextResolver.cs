using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Templates;

internal sealed record SalaryTemplateEsiResolution(
	SalaryTemplateEsiEvaluationContext? Context,
	IReadOnlyList<string> Warnings,
	bool RegistrationPending,
	Guid? ConfigurationId);

public class SalaryTemplateEsiContextResolver(
	IRepository<StatutoryRuleVersion, Guid> ruleRepository,
	EsiPayrollConfigurationResolver configurationResolver,
	EsiEnrollmentResolver enrollmentResolver,
	IRepository<PayrollComponentStatutoryTreatment, Guid> treatmentRepository,
	IRepository<StatutoryOverride, Guid> overrideRepository,
	IEarningComponentRepository earningComponentRepository,
	IDeductionComponentRepository deductionComponentRepository, EmployeeStatutoryProfileResolver profileResolver)
	: ITransientDependency
{
	private const string SchemeCode = StatutorySchemeCodes.EmployeeStateInsurance;
	private const string CanonicalEsiComponentName = "Employee State Insurance (ESI) - Auto";

	internal async Task<SalaryTemplateEsiResolution> ResolveAsync(
		DateTime effectiveMonth,
		string jurisdictionCode,
		Guid? employeeId)
	{
		var monthStart = new DateTime(effectiveMonth.Year, effectiveMonth.Month, 1);
		var monthEnd = monthStart.AddMonths(1).AddDays(-1);
		var configuration = await configurationResolver.ResolveAsync(monthStart);
		var establishment = configuration?.ToCalculationEstablishment();
		if (establishment == null)
		{
			return new SalaryTemplateEsiResolution(null, [], false, configuration?.Id);
		}

		var rule = (await ruleRepository.GetListAsync(x =>
			x.SchemeCode == SchemeCode &&
			x.JurisdictionCode == jurisdictionCode &&
			x.IsActive &&
			x.EffectiveFrom <= monthEnd &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= monthStart)))
			.OrderByDescending(x => x.EffectiveFrom)
			.ThenByDescending(x => x.Version)
			.FirstOrDefault();
		if (rule == null)
		{
			return new SalaryTemplateEsiResolution(null, ["ESI_RULE_NOT_FOUND"], false, configuration?.Id);
		}

		var treatments = (await treatmentRepository.GetListAsync(x =>
			x.SchemeCode == SchemeCode &&
			x.EffectiveFrom <= monthEnd &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= monthStart)))
			.GroupBy(x => x.ComponentId)
			.ToDictionary(
				x => x.Key,
				x => x.OrderByDescending(item => item.EffectiveFrom).First().Treatment);
		EmployeeStatutoryEnrollment? enrollment = null;
		var activeEarnings = await earningComponentRepository.GetListAsync(x => x.IsActive);
		foreach (var earning in activeEarnings)
			treatments.TryAdd(earning.Id, EsiWageDefaults.ByName.GetValueOrDefault(earning.Name, StatutoryWageTreatment.Unclassified));
		foreach (var treatment in configuration!.GetTreatments()) treatments[treatment.Key] = treatment.Value;
		IReadOnlyList<StatutoryOverride> overrides = [];
		if (employeeId.HasValue)
		{
			enrollment = (await enrollmentResolver.ResolveAsync(monthStart, [employeeId.Value])).GetValueOrDefault(employeeId.Value);
			overrides = await overrideRepository.GetListAsync(x =>
				x.SchemeCode == SchemeCode &&
				x.EmployeeId == employeeId.Value &&
				x.EffectiveFrom <= monthEnd &&
				x.EffectiveTo >= monthStart);
		}

		var canonicalDeduction = await deductionComponentRepository.FindByNameAsync(CanonicalEsiComponentName);
		var calculationSettings = configuration.GetCalculationSettings();
		calculationSettings = calculationSettings with
		{
			EligibilityComponentIsActive = calculationSettings.EligibilityComponentId == null ||
			activeEarnings.Any(x => x.Id == calculationSettings.EligibilityComponentId),
			CalculationComponentIsActive = calculationSettings.CalculationComponentId == null ||
			activeEarnings.Any(x => x.Id == calculationSettings.CalculationComponentId)
		};
		var context = new SalaryTemplateEsiEvaluationContext(
			monthStart,
			rule,
			establishment,
			enrollment,
			treatments,
			overrides,
			canonicalDeduction?.Id ?? Guid.Empty,
			canonicalDeduction?.Name ?? CanonicalEsiComponentName)
		{ CalculationSettings = calculationSettings, PayrollProfile = employeeId.HasValue ? await profileResolver.ResolveAsync(employeeId.Value, monthStart) : null };
		return new SalaryTemplateEsiResolution(
			context,
			enrollment?.Status == EmployeeStatutoryEnrollmentStatus.RegistrationPending
				? ["ESI_REGISTRATION_PENDING"]
				: [],
			enrollment?.Status == EmployeeStatutoryEnrollmentStatus.RegistrationPending, configuration?.Id);
	}
}