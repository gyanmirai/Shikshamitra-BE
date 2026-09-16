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
	bool RegistrationPending);

public class SalaryTemplateEsiContextResolver(
	IRepository<StatutoryRuleVersion, Guid> ruleRepository,
	IRepository<StatutoryEstablishment, Guid> establishmentRepository,
	IRepository<EmployeeStatutoryEnrollment, Guid> enrollmentRepository,
	IRepository<PayrollComponentStatutoryTreatment, Guid> treatmentRepository,
	IRepository<StatutoryOverride, Guid> overrideRepository,
	IDeductionComponentRepository deductionComponentRepository)
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
		var establishment = (await establishmentRepository.GetListAsync(x =>
			x.SchemeCode == SchemeCode &&
			x.JurisdictionCode == jurisdictionCode &&
			x.Status == StatutoryEstablishmentStatus.Active &&
			x.BenefitsActiveFrom.HasValue &&
			x.BenefitsActiveFrom.Value <= monthEnd))
			.OrderByDescending(x => x.BenefitsActiveFrom)
			.FirstOrDefault();
		if (establishment == null)
		{
			return new SalaryTemplateEsiResolution(null, ["ESI_ESTABLISHMENT_INACTIVE"], false);
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
			return new SalaryTemplateEsiResolution(null, ["ESI_RULE_NOT_FOUND"], false);
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
		IReadOnlyList<StatutoryOverride> overrides = [];
		if (employeeId.HasValue)
		{
			enrollment = (await enrollmentRepository.GetListAsync(x =>
				x.SchemeCode == SchemeCode && x.EmployeeId == employeeId.Value))
				.OrderByDescending(x => x.CreationTime)
				.FirstOrDefault();
			overrides = await overrideRepository.GetListAsync(x =>
				x.SchemeCode == SchemeCode &&
				x.EmployeeId == employeeId.Value &&
				x.EffectiveFrom <= monthEnd &&
				x.EffectiveTo >= monthStart);
		}

		var canonicalDeduction = await deductionComponentRepository.FindByNameAsync(CanonicalEsiComponentName);
		var context = new SalaryTemplateEsiEvaluationContext(
			monthStart,
			rule,
			establishment,
			enrollment,
			treatments,
			overrides,
			canonicalDeduction?.Id ?? Guid.Empty,
			canonicalDeduction?.Name ?? CanonicalEsiComponentName);
		return new SalaryTemplateEsiResolution(
			context,
			enrollment?.Status == EmployeeStatutoryEnrollmentStatus.RegistrationPending
				? ["ESI_REGISTRATION_PENDING"]
				: [],
			enrollment?.Status == EmployeeStatutoryEnrollmentStatus.RegistrationPending);
	}
}
