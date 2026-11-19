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
	IRepository<StatutoryEstablishment, Guid> establishmentRepository,
	IRepository<EmployeeStatutoryEnrollment, Guid> enrollmentRepository,
	IRepository<PayrollComponentStatutoryTreatment, Guid> treatmentRepository,
	IRepository<StatutoryOverride, Guid> overrideRepository,
	IEmployeeRepository employeeRepository,
	IEmployeeAuthorizationService employeeAuthorizationService,
	IEmployeePayrollComponentRepository payrollComponentRepository,
	IRepository<PayrollAdjustment, Guid> adjustmentRepository,
	PayrollComponentVersionResolver componentVersionResolver,
	PayrollScopedEmployeeResolver scopedEmployeeResolver,
	IUserScopeService userScopeService,
	IEarningComponentRepository earningComponentRepository,
	EsiStatutoryPreviewReader previewReader,
	EsiPayrollConfigurationResolver configurationResolver, EmployeeStatutoryProfileResolver profileResolver, EsiPolicyResolver policyResolver)
	: ApplicationService, IEsiStatutoryAppService
{
	private const string JurisdictionCode = "IN-GJ";

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

	[Authorize(EsiPermissions.View)]
	public async Task<List<PayrollComponentStatutoryTreatmentDto>> GetComponentTreatmentsAsync(DateTime? effectiveOn = null)
	{
		await AuthorizationService.CheckAsync(EsiPermissions.View);
		return await ReadComponentTreatmentsAsync((effectiveOn ?? Clock.Now).Date);
	}

	internal async Task<List<PayrollComponentStatutoryTreatmentDto>> ReadComponentTreatmentsAsync(DateTime effectiveDate)
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

		var configured = (await configurationResolver.ResolveAsync(effectiveDate))?.GetTreatments();
		return componentCatalog.Select(component =>
		{
			var treatment = byComponent.GetValueOrDefault(component.Id);
			return new PayrollComponentStatutoryTreatmentDto
			{
				ComponentId = component.Id,
				ComponentName = component.Name,
				Treatment = (configured != null && configured.TryGetValue(component.Id, out var configuredTreatment)
					? configuredTreatment : treatment?.Treatment ?? EsiWageDefaults.ByName.GetValueOrDefault(component.Name, StatutoryWageTreatment.Unclassified)).ToString(),
				EffectiveFrom = treatment?.EffectiveFrom ?? effectiveDate,
				EffectiveTo = treatment?.EffectiveTo
			};
		}).ToList();
	}

	[Authorize(EsiPermissions.View)]
	public async Task<EmployeeStatutoryEnrollmentDto> GetEnrollmentAsync(Guid employeeId)
	{
		await AuthorizationService.CheckAsync(EsiPermissions.View);
		await employeeAuthorizationService.CheckEmployeeScopedPermissionAsync(employeeId, EsiPermissions.View);
		await employeeRepository.GetAsync(employeeId);
		return await ReadEnrollmentAsync(employeeId);
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
		// Coverage and contribution-period facts are resolved from payroll history, never manual form dates.
		enrollment.Update(status == EmployeeStatutoryEnrollmentStatus.RegistrationPending
			? status : EmployeeStatutoryEnrollmentStatus.PendingReview, ceilingCategory, enrollment.InsuranceNumber, enrollment.CoveredFrom,
			enrollment.CoveredTo, enrollment.ContributionPeriodEnd, input.StatusReason,
			input.EmployerShareReliefEligible, input.EmployerShareReliefFrom,
			input.EmployerShareReliefTo, input.DisabilityEvidenceReference);
		await enrollmentRepository.UpdateAsync(enrollment, autoSave: true);
		return await ReadEnrollmentAsync(employeeId);
	}

	private async Task<EmployeeStatutoryEnrollmentDto> ReadEnrollmentAsync(Guid employeeId)
	{
		var enrollment = await FindEnrollmentAsync(employeeId);
		var dto = enrollment is null ? new EmployeeStatutoryEnrollmentDto { EmployeeId = employeeId } : MapEnrollment(enrollment);
		var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(Clock.Now, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"));
		dto.InsuranceNumber = (await profileResolver.ResolveAsync(employeeId, new DateTime(local.Year, local.Month, 1))).EsicInsuranceNumber;
		return dto;
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
	public Task<StatutoryImpactPreviewDto> PreviewAsync(StatutoryImpactPreviewRequestDto input) => ReadPreviewAsync(input, false);

	[Authorize(EsiPermissions.View)]
	public Task<StatutoryImpactPreviewDto> GetTablePopulationAsync(StatutoryImpactPreviewRequestDto input) => ReadPreviewAsync(input, true);

	private async Task<StatutoryImpactPreviewDto> ReadPreviewAsync(StatutoryImpactPreviewRequestDto input, bool allRows)
	{
		await AuthorizationService.CheckAsync(EsiPermissions.View);
		ArgumentNullException.ThrowIfNull(input);
		if (input.ConfigurationDraft != null)
		{
			await AuthorizationService.CheckAsync(EsiPermissions.ConfigureCalculation);
			if (!new EsiConfigurationDraftDtoValidator().Validate(input.ConfigurationDraft).IsValid)
				throw new Volo.Abp.Validation.AbpValidationException("Review the ESIC configuration.");
			input.ConfigurationDraft.CalculationSettings.Validate();
			if (DateTime.ParseExact(input.ConfigurationDraft.EffectiveMonth, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)
				!= DateTime.ParseExact(input.MonthYear, "MM-yyyy", System.Globalization.CultureInfo.InvariantCulture))
				throw new BusinessException(HRErrorCodes.EsiConfigurationInvalidMonth);
		}
		var calculation = await previewReader.CalculateAsync(input.MonthYear, input.ConfigurationDraft);
		return EsiStatutoryPreviewReader.Project(calculation, input, allRows);
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
		var month = effectiveDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
		var adjustments = await adjustmentRepository.GetListAsync(x =>
			x.IsEarning && x.MonthYear == month && employeeIds.Contains(x.EmployeeId));
		catalog.AddRange(adjustments.Select(x => new WageComponentCatalogItem(
			x.Id, $"{x.Name} - {employeeNames.GetValueOrDefault(x.EmployeeId, "Employee-specific")}")));
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
		return (await policyResolver.ResolveAsync(null, effectiveOn)).Rule;
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
		EmployeeShareDailyWageExemption = rule.EmployeeShareDailyWageExemption,
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
