using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Statutory;

[RemoteService(false)]
public class EsiStatutoryAppService(
	IRepository<StatutoryRuleVersion, Guid> ruleRepository,
	IRepository<StatutoryEstablishment, Guid> establishmentRepository,
	IRepository<EmployeeStatutoryEnrollment, Guid> enrollmentRepository,
	IRepository<PayrollComponentStatutoryTreatment, Guid> treatmentRepository,
	IRepository<StatutoryOverride, Guid> overrideRepository,
	IEmployeeRepository employeeRepository,
	IEmployeePayrollComponentRepository payrollComponentRepository,
	PayrollComponentVersionResolver componentVersionResolver,
	PayrollCompensationRevisionResolver compensationRevisionResolver,
	PayrollScopedEmployeeResolver scopedEmployeeResolver,
	IUserScopeService userScopeService,
	IEarningComponentRepository earningComponentRepository,
	IDeductionComponentRepository deductionComponentRepository)
	: ApplicationService, IEsiStatutoryAppService
{
	private const string JurisdictionCode = "IN-GJ";
	private const string CanonicalEsiComponentName = "Employee State Insurance (ESI) - Auto";

	public async Task<StatutoryRuleVersionDto> GetCurrentRuleAsync(DateTime? effectiveOn = null)
	{
		var rule = await ResolveRuleAsync(effectiveOn ?? Clock.Now);
		return MapRule(rule);
	}

	public async Task<StatutoryEstablishmentDto> GetEstablishmentAsync()
	{
		var establishment = await FindEstablishmentAsync();
		return establishment is null
			? new StatutoryEstablishmentDto()
			: MapEstablishment(establishment);
	}

	public async Task<StatutoryEstablishmentDto> ConfigureEstablishmentAsync(ConfigureStatutoryEstablishmentDto input)
	{
		ArgumentNullException.ThrowIfNull(input);
		await EnsureHrWideAccessAsync();
		var establishment = await FindEstablishmentAsync();
		if (establishment is null)
		{
			establishment = new StatutoryEstablishment(
				GuidGenerator.Create(), CurrentTenant.Id, StatutorySchemeCodes.EmployeeStateInsurance, JurisdictionCode);
			await establishmentRepository.InsertAsync(establishment);
		}

		establishment.Configure(
			input.JurisdictionCode,
			input.RegistrationNumber,
			input.EmployeeCount,
			input.CoveredSince,
			input.BenefitsActiveFrom,
			input.EvidenceReference);
		await establishmentRepository.UpdateAsync(establishment, autoSave: true);
		return MapEstablishment(establishment);
	}

	public async Task<List<PayrollComponentStatutoryTreatmentDto>> GetComponentTreatmentsAsync(DateTime? effectiveOn = null)
	{
		var effectiveDate = (effectiveOn ?? Clock.Now).Date;
		var componentCatalog = await GetWageComponentCatalogAsync(effectiveDate);
		var componentIds = componentCatalog.Select(x => x.Id).ToList();
		var treatments = await treatmentRepository.GetListAsync(x =>
			x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance &&
			componentIds.Contains(x.ComponentId) &&
			x.EffectiveFrom <= effectiveDate &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= effectiveDate));
		var byComponent = treatments
			.OrderByDescending(x => x.EffectiveFrom)
			.GroupBy(x => x.ComponentId)
			.ToDictionary(x => x.Key, x => x.First());

		return componentCatalog.Select(component =>
		{
			var treatment = byComponent.GetValueOrDefault(component.Id);
			return new PayrollComponentStatutoryTreatmentDto
			{
				ComponentId = component.Id,
				ComponentName = component.Name,
				Treatment = (treatment?.Treatment ?? StatutoryWageTreatment.Unclassified).ToString(),
				EffectiveFrom = treatment?.EffectiveFrom ?? effectiveDate,
				EffectiveTo = treatment?.EffectiveTo
			};
		}).ToList();
	}

	public async Task<List<PayrollComponentStatutoryTreatmentDto>> UpdateComponentTreatmentsAsync(
		UpdatePayrollComponentStatutoryTreatmentsDto input)
	{
		ArgumentNullException.ThrowIfNull(input);
		await EnsureHrWideAccessAsync();
		var activeEarningIds = (await earningComponentRepository.GetListAsync(x => x.IsActive)).Select(x => x.Id).ToHashSet();
		var effectiveMonth = input.Items.Min(x => x.EffectiveFrom);
		var tenantEmployeeIds = (await scopedEmployeeResolver.GetScopedPayrollEmployeesAsync(
			effectiveMonth.Year, effectiveMonth.Month, includeUnknownStageEmployees: true)).Select(x => x.Id).ToList();
		var tenantPayrollComponents = await payrollComponentRepository.GetByEmployeeIdsAsync(tenantEmployeeIds);
		foreach (var component in tenantPayrollComponents.SelectMany(x => x).Where(x => x.IsEarning && !x.ComponentId.HasValue))
		{
			activeEarningIds.Add(component.Id);
		}
		foreach (var item in input.Items)
		{
			if (!activeEarningIds.Contains(item.ComponentId))
			{
				throw new BusinessException(HRErrorCodes.PayrollComponentInactive).WithData("componentId", item.ComponentId);
			}
			if (!Enum.TryParse<StatutoryWageTreatment>(item.Treatment, true, out var treatment))
			{
				throw new BusinessException(HRErrorCodes.StatutoryActivationBlocked).WithData("treatment", item.Treatment);
			}

			var existingTreatments = (await treatmentRepository.GetListAsync(x =>
				x.ComponentId == item.ComponentId && x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance))
				.OrderByDescending(x => x.EffectiveFrom)
				.ToList();
			var sameEffectiveDate = existingTreatments.FirstOrDefault(x => x.EffectiveFrom == item.EffectiveFrom.Date);
			if (sameEffectiveDate is not null)
			{
				sameEffectiveDate.Update(treatment, item.EffectiveFrom, item.EffectiveTo);
				await treatmentRepository.UpdateAsync(sameEffectiveDate);
				continue;
			}

			var previous = existingTreatments.FirstOrDefault(x =>
				x.EffectiveFrom < item.EffectiveFrom.Date &&
				(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= item.EffectiveFrom.Date));
			if (previous is not null)
			{
				previous.Update(previous.Treatment, previous.EffectiveFrom, item.EffectiveFrom.Date.AddDays(-1));
				await treatmentRepository.UpdateAsync(previous);
			}

			var newTreatment = new PayrollComponentStatutoryTreatment(
				GuidGenerator.Create(), CurrentTenant.Id, item.ComponentId,
				StatutorySchemeCodes.EmployeeStateInsurance, treatment, item.EffectiveFrom, item.EffectiveTo);
			await treatmentRepository.InsertAsync(newTreatment);
		}
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await GetComponentTreatmentsAsync(input.Items.Min(x => x.EffectiveFrom));
	}

	public async Task<EmployeeStatutoryEnrollmentDto> GetEnrollmentAsync(Guid employeeId)
	{
		await employeeRepository.GetAsync(employeeId);
		var enrollment = await FindEnrollmentAsync(employeeId);
		return enrollment is null
			? new EmployeeStatutoryEnrollmentDto { EmployeeId = employeeId }
			: MapEnrollment(enrollment);
	}

	public async Task<EmployeeStatutoryEnrollmentDto> UpdateEnrollmentAsync(
		Guid employeeId,
		UpdateEmployeeStatutoryEnrollmentDto input)
	{
		ArgumentNullException.ThrowIfNull(input);
		await employeeRepository.GetAsync(employeeId);
		if (!Enum.TryParse<EmployeeStatutoryEnrollmentStatus>(input.Status, true, out var status) ||
			!Enum.TryParse<StatutoryWageCeilingCategory>(input.WageCeilingCategory, true, out var ceilingCategory))
		{
			throw new BusinessException(HRErrorCodes.StatutoryActivationBlocked);
		}

		var enrollment = await FindEnrollmentAsync(employeeId);
		if (enrollment is null)
		{
			enrollment = new EmployeeStatutoryEnrollment(
				GuidGenerator.Create(), CurrentTenant.Id, employeeId, StatutorySchemeCodes.EmployeeStateInsurance);
			await enrollmentRepository.InsertAsync(enrollment);
		}
		enrollment.Update(status, ceilingCategory, input.InsuranceNumber, input.CoveredFrom,
			input.CoveredTo, input.ContributionPeriodEnd, input.StatusReason,
			input.EmployerShareReliefEligible, input.EmployerShareReliefFrom,
			input.EmployerShareReliefTo, input.DisabilityEvidenceReference);
		await enrollmentRepository.UpdateAsync(enrollment, autoSave: true);
		return MapEnrollment(enrollment);
	}

	public async Task CreateOverrideAsync(Guid employeeId, CreateStatutoryOverrideDto input)
	{
		ArgumentNullException.ThrowIfNull(input);
		await employeeRepository.GetAsync(employeeId);
		if (!Enum.TryParse<StatutoryOverrideType>(input.OverrideType, true, out var overrideType) ||
			(overrideType is StatutoryOverrideType.EmployeeAmount or StatutoryOverrideType.EmployerAmount && !input.Amount.HasValue))
		{
			throw new BusinessException(HRErrorCodes.StatutoryOverrideInvalid);
		}

		var statutoryOverride = new StatutoryOverride(
			GuidGenerator.Create(), CurrentTenant.Id, employeeId, StatutorySchemeCodes.EmployeeStateInsurance,
			overrideType, input.Amount, input.Reason, input.EvidenceReference,
			input.EffectiveFrom, input.EffectiveTo, CurrentUser.Id);
		await overrideRepository.InsertAsync(statutoryOverride, autoSave: true);
	}

	public async Task<List<StatutoryOverrideDto>> GetOverridesAsync(Guid employeeId)
	{
		await employeeRepository.GetAsync(employeeId);
		return (await overrideRepository.GetListAsync(x =>
				x.EmployeeId == employeeId && x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance))
			.OrderByDescending(x => x.EffectiveFrom)
			.ThenByDescending(x => x.CreationTime)
			.Select(x => new StatutoryOverrideDto
			{
				Id = x.Id,
				EmployeeId = x.EmployeeId,
				SchemeCode = x.SchemeCode,
				OverrideType = x.OverrideType.ToString(),
				Amount = x.Amount,
				Reason = x.Reason,
				EvidenceReference = x.EvidenceReference,
				EffectiveFrom = x.EffectiveFrom,
				EffectiveTo = x.EffectiveTo,
				ApprovedByUserId = x.ApprovedByUserId,
				ApprovedAt = x.CreationTime
			})
			.ToList();
	}

	public async Task<StatutoryImpactPreviewDto> PreviewAsync(StatutoryImpactPreviewRequestDto input)
	{
		ArgumentNullException.ThrowIfNull(input);
		var payrollMonth = ParseMonth(input.MonthYear);
		var establishment = await FindEstablishmentAsync();
		var monthEnd = DateOnly.FromDateTime(payrollMonth).AddMonths(1).AddDays(-1);
		var rule = await ResolveRuleAsync(monthEnd.ToDateTime(TimeOnly.MinValue));
		var employees = await scopedEmployeeResolver.GetScopedPayrollEmployeesAsync(
			payrollMonth.Year, payrollMonth.Month, includeUnknownStageEmployees: false);
		var employeeIds = employees.Select(x => x.Id).ToList();
		var existingComponents = await payrollComponentRepository.GetByEmployeeIdsAsync(employeeIds);
		var existingComponentsByEmployee = existingComponents.SelectMany(x => x).ToLookup(x => x.EmployeeId);
		var compensation = await compensationRevisionResolver.ResolveAsync(
			employeeIds, payrollMonth.Year, payrollMonth.Month, existingComponentsByEmployee);
		var treatments = await GetEffectiveTreatmentMapAsync(payrollMonth);
		var enrollments = (await enrollmentRepository.GetListAsync(x =>
			x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance && employeeIds.Contains(x.EmployeeId)))
			.ToDictionary(x => x.EmployeeId);
		var overrides = (await overrideRepository.GetListAsync(x =>
			x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance && employeeIds.Contains(x.EmployeeId) &&
			x.EffectiveFrom <= payrollMonth && x.EffectiveTo >= payrollMonth))
			.ToLookup(x => x.EmployeeId);

		var result = new StatutoryImpactPreviewDto
		{
			MonthYear = input.MonthYear
		};
		foreach (var employee in employees.OrderBy(x => x.EmployeeName).ThenBy(x => x.EmployeeCode))
		{
			if (!compensation.TryGetValue(employee.Id, out var compensationResolution))
			{
				var missingRevisionEnrollment = enrollments.GetValueOrDefault(employee.Id);
				result.Employees.Add(new EmployeeStatutoryImpactDto
				{
					EmployeeId = employee.Id,
					EmployeeIdentifier = employee.EmployeeCode ?? string.Empty,
					EmployeeName = employee.EmployeeName,
					CoverageDecision = IssueCodes.CompensationRevisionRequired,
					EnrollmentStatus = (missingRevisionEnrollment?.Status ?? EmployeeStatutoryEnrollmentStatus.PendingReview).ToString(),
					WageCeilingCategory = (missingRevisionEnrollment?.WageCeilingCategory ?? StatutoryWageCeilingCategory.Standard).ToString(),
					InsuranceNumber = missingRevisionEnrollment?.InsuranceNumber,
					ContributionPeriodEnd = missingRevisionEnrollment?.ContributionPeriodEnd,
					IssueCodes = [IssueCodes.CompensationRevisionRequired]
				});
				continue;
			}

			var wageLines = compensationResolution.Components
				.Where(x => x.IsEarning)
				.Select(x => new StatutoryWageLine(
					x.ComponentId ?? x.Id,
					x.Name,
					x.Amount,
					treatments.TryGetValue(x.ComponentId ?? x.Id, out var treatment)
						? treatment
						: StatutoryWageTreatment.Unclassified))
				.ToList();
			if (wageLines.Count == 0)
			{
				continue;
			}
			var calculation = EsiStatutoryCalculator.Calculate(
				payrollMonth, rule, establishment, enrollments.GetValueOrDefault(employee.Id),
				wageLines, overrides[employee.Id].ToList(), assumeEstablishmentActive: true);
			var enrollment = enrollments.GetValueOrDefault(employee.Id);
			result.Employees.Add(new EmployeeStatutoryImpactDto
			{
				EmployeeId = employee.Id,
				EmployeeIdentifier = employee.EmployeeCode ?? string.Empty,
				EmployeeName = employee.EmployeeName,
				IsCovered = calculation.IsCovered,
				CanCalculate = calculation.CanCalculate,
				CoverageDecision = calculation.CoverageDecision,
				StatutoryWages = calculation.StatutoryWages,
				WageCeiling = calculation.WageCeiling,
				EmployeeContribution = calculation.EmployeeContribution,
				EmployerContribution = calculation.EmployerContribution,
				GovernmentContribution = calculation.GovernmentContribution,
				EnrollmentStatus = (enrollment?.Status ?? EmployeeStatutoryEnrollmentStatus.PendingReview).ToString(),
				WageCeilingCategory = (enrollment?.WageCeilingCategory ?? StatutoryWageCeilingCategory.Standard).ToString(),
				InsuranceNumber = enrollment?.InsuranceNumber,
				ContributionPeriodEnd = enrollment?.ContributionPeriodEnd,
				IssueCodes = calculation.IssueCodes.ToList()
			});
		}

		result.EmployeeCount = result.Employees.Count;
		result.CoveredEmployeeCount = result.Employees.Count(x => x.IsCovered);
		result.EmployeeContributionTotal = result.Employees.Sum(x => x.EmployeeContribution);
		result.EmployerContributionTotal = result.Employees.Sum(x => x.EmployerContribution);
		result.GovernmentContributionTotal = result.Employees.Sum(x => x.GovernmentContribution);
		result.BlockingIssueCodes = result.Employees.SelectMany(x => x.IssueCodes).Distinct().OrderBy(x => x).ToList();
		if (establishment is null || !establishment.CanActivate())
		{
			result.BlockingIssueCodes.Add(EsiStatutoryCalculator.EstablishmentInactiveIssue);
		}
		result.BlockingIssueCodes = result.BlockingIssueCodes.Distinct().ToList();
		result.CanActivate = result.BlockingIssueCodes.Count == 0;
		return result;
	}

	public async Task<StatutoryEstablishmentDto> ActivateAsync(StatutoryImpactPreviewRequestDto input)
	{
		await EnsureHrWideAccessAsync();
		var preview = await PreviewAsync(input);
		if (!preview.CanActivate)
		{
			throw new BusinessException(HRErrorCodes.StatutoryActivationBlocked)
				.WithData("issueCodes", string.Join(",", preview.BlockingIssueCodes));
		}
		var establishment = await FindEstablishmentAsync();
		if (establishment is null || !establishment.CanActivate())
		{
			throw new BusinessException(HRErrorCodes.StatutoryEstablishmentNotReady);
		}

		var canonicalComponent = await EnsureCanonicalEsiComponentAsync();
		await EnsureEmployeeBindingsAsync(preview.Employees.Select(x => x.EmployeeId), canonicalComponent);
		establishment.Activate(CurrentUser.Id, Clock.Now);
		await establishmentRepository.UpdateAsync(establishment, autoSave: true);
		return MapEstablishment(establishment);
	}

	private async Task<DeductionComponent> EnsureCanonicalEsiComponentAsync()
	{
		var existing = await deductionComponentRepository.FindByNameAsync(CanonicalEsiComponentName);
		if (existing is not null)
		{
			if (!existing.IsActive)
			{
				existing.Activate();
				await deductionComponentRepository.UpdateAsync(existing);
			}
			return existing;
		}

		var component = new DeductionComponent(
			GuidGenerator.Create(), CurrentTenant.Id, CanonicalEsiComponentName,
			"System-managed ESI employee contribution. Calculated from the effective statutory rule.",
			DeductionComponentType.Fixed, CalculationType.Amount, 0m, null, null,
			ComponentDependency.None, visibleInPayslip: true, displayOrder: 4);
		await deductionComponentRepository.InsertAsync(component);
		return component;
	}

	private async Task EnsureEmployeeBindingsAsync(IEnumerable<Guid> employeeIds, DeductionComponent component)
	{
		foreach (var employeeId in employeeIds.Distinct())
		{
			var existing = (await payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId))
				.FirstOrDefault(x => x.SemanticType == PayrollComponentSemanticType.EmployeeStateInsurance);
			if (existing is not null)
			{
				continue;
			}
			var binding = new EmployeePayrollComponent(
				GuidGenerator.Create(), employeeId, component.Name, component.Id, component.Description,
				0m, true, false, null, SalaryComponentType.Fixed, SalaryComponentDependency.None,
				false, true, PayrollComponentSemanticType.EmployeeStateInsurance);
			await payrollComponentRepository.InsertAsync(binding);
		}
		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	private async Task<Dictionary<Guid, StatutoryWageTreatment>> GetEffectiveTreatmentMapAsync(DateTime date)
	{
		var treatments = await treatmentRepository.GetListAsync(x =>
			x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance &&
			x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= date));
		return treatments.OrderByDescending(x => x.EffectiveFrom)
			.GroupBy(x => x.ComponentId)
			.ToDictionary(x => x.Key, x => x.First().Treatment);
	}

	private async Task<List<WageComponentCatalogItem>> GetWageComponentCatalogAsync(DateTime effectiveDate)
	{
		var catalog = (await earningComponentRepository.GetListAsync(x => x.IsActive))
			.OrderBy(x => x.DisplayOrder)
			.ThenBy(x => x.Name)
			.Select(x => new WageComponentCatalogItem(x.Id, x.Name))
			.ToList();
		var employees = await scopedEmployeeResolver.GetScopedPayrollEmployeesAsync(
			effectiveDate.Year, effectiveDate.Month, includeUnknownStageEmployees: true);
		var employeeIds = employees.Select(x => x.Id).ToList();
		var employeeNames = employees.ToDictionary(x => x.Id, x => x.EmployeeName);
		var components = await componentVersionResolver.ResolveByEmployeeAsync(
			await payrollComponentRepository.GetByEmployeeIdsAsync(employeeIds),
			effectiveDate.ToString("yyyy-MM", CultureInfo.InvariantCulture));
		var customComponents = employeeIds
			.SelectMany(employeeId => PayrollComponentMonthScope.FilterForMonth(
				components[employeeId], effectiveDate.Year, effectiveDate.Month))
			.Where(x => x.IsEarning && !x.ComponentId.HasValue)
			.Select(x => new WageComponentCatalogItem(
				x.Id,
				$"{x.Name} - {employeeNames.GetValueOrDefault(x.EmployeeId, "Employee-specific")}"));
		catalog.AddRange(customComponents);
		return catalog.GroupBy(x => x.Id).Select(x => x.First()).ToList();
	}

	private async Task EnsureHrWideAccessAsync()
	{
		var userId = CurrentUser.Id;
		if (!userId.HasValue)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		}

		var scope = await userScopeService.GetUserScopeAsync(userId.Value);
		if (!HrUserScopeAccess.HasHrWideAccess(scope))
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}
	}

	private async Task<StatutoryRuleVersion> ResolveRuleAsync(DateTime effectiveOn)
	{
		var rules = await ruleRepository.GetListAsync(x =>
			x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance && x.JurisdictionCode == JurisdictionCode &&
			x.IsActive && x.EffectiveFrom <= effectiveOn.Date &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= effectiveOn.Date));
		return rules.OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Version).FirstOrDefault()
			?? throw new BusinessException(HRErrorCodes.StatutoryRuleNotFound);
	}

	private async Task<StatutoryEstablishment?> FindEstablishmentAsync() =>
		await establishmentRepository.FindAsync(x => x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance);

	private async Task<EmployeeStatutoryEnrollment?> FindEnrollmentAsync(Guid employeeId) =>
		await enrollmentRepository.FindAsync(x =>
			x.EmployeeId == employeeId && x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance);

	private static DateTime ParseMonth(string monthYear) =>
		DateTime.ParseExact(monthYear, "MM-yyyy", CultureInfo.InvariantCulture);

	private static StatutoryRuleVersionDto MapRule(StatutoryRuleVersion rule) => new()
	{
		Id = rule.Id,
		SchemeCode = rule.SchemeCode,
		JurisdictionCode = rule.JurisdictionCode,
		Version = rule.Version,
		EffectiveFrom = rule.EffectiveFrom,
		EffectiveTo = rule.EffectiveTo,
		EstablishmentThreshold = rule.EstablishmentThreshold,
		StandardWageCeiling = rule.StandardWageCeiling,
		DisabilityWageCeiling = rule.DisabilityWageCeiling,
		EmployeePercentage = rule.EmployeePercentage,
		EmployerPercentage = rule.EmployerPercentage,
		ExclusionCapPercentage = rule.ExclusionCapPercentage,
		InKindCapPercentage = rule.InKindCapPercentage,
		RoundingPolicy = rule.RoundingPolicy.ToString(),
		SourceUrl = rule.SourceUrl,
		SourceVerifiedOn = rule.SourceVerifiedOn
	};

	private static StatutoryEstablishmentDto MapEstablishment(StatutoryEstablishment value) => new()
	{
		Id = value.Id,
		SchemeCode = value.SchemeCode,
		JurisdictionCode = value.JurisdictionCode,
		RegistrationNumber = value.RegistrationNumber,
		EmployeeCount = value.EmployeeCount,
		CoveredSince = value.CoveredSince,
		BenefitsActiveFrom = value.BenefitsActiveFrom,
		Status = value.Status.ToString(),
		EvidenceReference = value.EvidenceReference,
		ConfirmedByUserId = value.ConfirmedByUserId,
		ConfirmedAt = value.ConfirmedAt,
		CanActivate = value.CanActivate()
	};

	private static EmployeeStatutoryEnrollmentDto MapEnrollment(EmployeeStatutoryEnrollment value) => new()
	{
		Id = value.Id,
		EmployeeId = value.EmployeeId,
		SchemeCode = value.SchemeCode,
		Status = value.Status.ToString(),
		WageCeilingCategory = value.WageCeilingCategory.ToString(),
		InsuranceNumber = value.InsuranceNumber,
		CoveredFrom = value.CoveredFrom,
		CoveredTo = value.CoveredTo,
		ContributionPeriodEnd = value.ContributionPeriodEnd,
		StatusReason = value.StatusReason
		,EmployerShareReliefEligible = value.EmployerShareReliefEligible
		,EmployerShareReliefFrom = value.EmployerShareReliefFrom
		,EmployerShareReliefTo = value.EmployerShareReliefTo
		,DisabilityEvidenceReference = value.DisabilityEvidenceReference
	};

	private sealed record WageComponentCatalogItem(Guid Id, string Name);
}
