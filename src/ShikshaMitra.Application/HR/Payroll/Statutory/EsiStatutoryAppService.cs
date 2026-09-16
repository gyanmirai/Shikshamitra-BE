using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using EsiPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes;
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
	IEmployeeAuthorizationService employeeAuthorizationService,
	IEmployeePayrollComponentRepository payrollComponentRepository,
	PayrollComponentVersionResolver componentVersionResolver,
	PayrollScopedEmployeeResolver scopedEmployeeResolver,
	IUserScopeService userScopeService,
	IEarningComponentRepository earningComponentRepository,
	IDeductionComponentRepository deductionComponentRepository,
	EsiStatutoryPreviewReader previewReader)
	: ApplicationService, IEsiStatutoryAppService
{
	private const string JurisdictionCode = "IN-GJ";
	private const string CanonicalEsiComponentName = "Employee State Insurance (ESI) - Auto";

	[Authorize(EsiPermissions.View)]
	public async Task<StatutoryRuleVersionDto> GetCurrentRuleAsync(DateTime? effectiveOn = null)
	{
		await AuthorizationService.CheckAsync(EsiPermissions.View);
		var rule = await ResolveRuleAsync(effectiveOn ?? Clock.Now);
		return MapRule(rule);
	}

	[Authorize(EsiPermissions.View)]
	public async Task<StatutoryEstablishmentDto> GetEstablishmentAsync()
	{
		await AuthorizationService.CheckAsync(EsiPermissions.View);
		var establishment = await FindEstablishmentAsync();
		return establishment is null
			? new StatutoryEstablishmentDto()
			: MapEstablishment(establishment);
	}

	[Authorize(EsiPermissions.Edit)]
	public async Task<StatutoryEstablishmentDto> ConfigureEstablishmentAsync(ConfigureStatutoryEstablishmentDto input)
	{
		await AuthorizationService.CheckAsync(EsiPermissions.Edit);
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

	[Authorize(EsiPermissions.View)]
	public async Task<List<PayrollComponentStatutoryTreatmentDto>> GetComponentTreatmentsAsync(DateTime? effectiveOn = null)
	{
		await AuthorizationService.CheckAsync(EsiPermissions.View);
		return await ReadComponentTreatmentsAsync((effectiveOn ?? Clock.Now).Date);
	}

	private async Task<List<PayrollComponentStatutoryTreatmentDto>> ReadComponentTreatmentsAsync(DateTime effectiveDate)
	{
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

	[Authorize(EsiPermissions.Edit)]
	public async Task<List<PayrollComponentStatutoryTreatmentDto>> UpdateComponentTreatmentsAsync(
		UpdatePayrollComponentStatutoryTreatmentsDto input)
	{
		await AuthorizationService.CheckAsync(EsiPermissions.Edit);
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
		// The edit response is part of the authorized mutation, not a separate View operation.
		return await ReadComponentTreatmentsAsync(input.Items.Min(x => x.EffectiveFrom).Date);
	}

	[Authorize(EsiPermissions.View)]
	public async Task<EmployeeStatutoryEnrollmentDto> GetEnrollmentAsync(Guid employeeId)
	{
		await AuthorizationService.CheckAsync(EsiPermissions.View);
		await employeeAuthorizationService.CheckEmployeeScopedPermissionAsync(employeeId, EsiPermissions.View);
		await employeeRepository.GetAsync(employeeId);
		var enrollment = await FindEnrollmentAsync(employeeId);
		return enrollment is null
			? new EmployeeStatutoryEnrollmentDto { EmployeeId = employeeId }
			: MapEnrollment(enrollment);
	}

	[Authorize(EsiPermissions.ManageEnrollment)]
	public async Task<EmployeeStatutoryEnrollmentDto> UpdateEnrollmentAsync(
		Guid employeeId,
		UpdateEmployeeStatutoryEnrollmentDto input)
	{
		await AuthorizationService.CheckAsync(EsiPermissions.ManageEnrollment);
		await employeeAuthorizationService.CheckEmployeeScopedPermissionAsync(employeeId, EsiPermissions.ManageEnrollment);
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

	[Authorize(EsiPermissions.ApproveOverride)]
	public async Task CreateOverrideAsync(Guid employeeId, CreateStatutoryOverrideDto input)
	{
		await AuthorizationService.CheckAsync(EsiPermissions.ApproveOverride);
		await employeeAuthorizationService.CheckEmployeeScopedPermissionAsync(employeeId, EsiPermissions.ApproveOverride);
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

	[Authorize(EsiPermissions.View)]
	public async Task<List<StatutoryOverrideDto>> GetOverridesAsync(Guid employeeId)
	{
		await AuthorizationService.CheckAsync(EsiPermissions.View);
		await employeeAuthorizationService.CheckEmployeeScopedPermissionAsync(employeeId, EsiPermissions.View);
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

	[Authorize(EsiPermissions.View)]
	public async Task<StatutoryImpactPreviewDto> PreviewAsync(StatutoryImpactPreviewRequestDto input)
	{
		await AuthorizationService.CheckAsync(EsiPermissions.View);
		ArgumentNullException.ThrowIfNull(input);
		var calculation = await previewReader.CalculateAsync(input.MonthYear);
		return EsiStatutoryPreviewReader.Project(calculation, input);
	}

	[Authorize(EsiPermissions.Activate)]
	public async Task<StatutoryEstablishmentDto> ActivateAsync(ActivateStatutorySchemeDto input)
	{
		await AuthorizationService.CheckAsync(EsiPermissions.Activate);
		ArgumentNullException.ThrowIfNull(input);
		await EnsureHrWideAccessAsync();
		var preview = await previewReader.CalculateAsync(input.MonthYear);
		if (!string.Equals(preview.PreviewFingerprint, input.PreviewFingerprint, StringComparison.Ordinal))
		{
			throw new BusinessException(HRErrorCodes.StatutoryPreviewStale);
		}
		if (!preview.Summary.CanActivate)
		{
			throw new BusinessException(HRErrorCodes.StatutoryActivationBlocked)
				.WithData("issueCodes", string.Join(",", preview.Summary.BlockingIssues.Select(x => x.Code)));
		}
		var establishment = await FindEstablishmentAsync();
		if (establishment is null || !establishment.CanActivate())
		{
			throw new BusinessException(HRErrorCodes.StatutoryEstablishmentNotReady);
		}

		var canonicalComponent = await EnsureCanonicalEsiComponentAsync();
		await EnsureEmployeeBindingsAsync(preview.Items.Select(x => x.EmployeeId), canonicalComponent);
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
