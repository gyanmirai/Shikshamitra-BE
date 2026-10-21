using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace ShikshaMitra.HR.Payroll.Compensation;

[RemoteService(false)]
public class CompensationRevisionAppService(
	IRepository<EmployeeCompensationRevision, Guid> revisionRepository,
	IRepository<EmployeeCompensationRevisionLine, Guid> lineRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollSnapshotRepository,
	ISalaryTemplateAppService salaryTemplateAppService,
	EmployeeActivationReadinessService activationReadinessService,
	PayrollSnapshotReviewInvalidator payrollSnapshotReviewInvalidator,
	IPayrollInputRevisionStore inputRevisionStore,
	EsiPolicyResolver esiPolicies,
	IAsyncQueryableExecuter asyncExecuter,
	IClock clock, IEmployeeAuthorizationService employeeAuthorizationService, EmployeeStatutoryProfileAppService profileService, EmployeeStatutoryProfileResolver profileResolver) : ApplicationService, ICompensationRevisionAppService
{
	public async Task<CompensationRevisionListDto> GetListAsync(Guid employeeId)
	{
		var revisions = await asyncExecuter.ToListAsync(
			(await revisionRepository.GetQueryableAsync())
				.Where(x => x.EmployeeId == employeeId)
				.OrderByDescending(x => x.EffectiveMonth)
				.ThenByDescending(x => x.RevisionNumber));
		var readiness = await activationReadinessService.GetAsync(employeeId);
		return new CompensationRevisionListDto
		{
			Items = await MapManyAsync(revisions, await CanViewAuditAsync(), readiness)
		};
	}

	public async Task<CompensationRevisionDto?> GetEffectiveAsync(Guid employeeId, DateTime month)
	{
		var effectiveMonth = NormalizeMonth(month);
		var revision = await asyncExecuter.FirstOrDefaultAsync(
			(await revisionRepository.GetQueryableAsync())
				.Where(x => x.EmployeeId == employeeId && x.Status == CompensationRevisionStatus.Approved && x.EffectiveMonth <= effectiveMonth)
				.OrderByDescending(x => x.EffectiveMonth)
				.ThenByDescending(x => x.RevisionNumber));
		return revision == null
			? null
			: await MapAsync(revision, await CanViewAuditAsync(), await activationReadinessService.GetAsync(employeeId));
	}

	[Volo.Abp.Uow.UnitOfWork(true)]
	public virtual async Task<CompensationRevisionDto> SaveAsync(Guid employeeId, SaveEmployeeCompensationDto input)
	{
		await employeeAuthorizationService.CheckEmployeeScopedPermissionAsync(employeeId,
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Manage);
		await inputRevisionStore.LockAsync();
		var replay = await asyncExecuter.FirstOrDefaultAsync(
			(await revisionRepository.GetQueryableAsync()).Where(x =>
				x.EmployeeId == employeeId && x.IdempotencyKey == input.IdempotencyKey));
		if (replay != null)
		{
			var original = TryDeserializeCalculation(replay.CalculationSnapshotJson);
			if (original != null && input.ExpectedEsiPolicyVersionId != original.EmployeeStateInsurance.RuleId)
				throw new BusinessException(HRErrorCodes.CompensationPreviewStale);
			if (original != null && (original.PfEnrollment != input.PfEnrollment || replay.EsiManagementMode != input.EsiManagementMode || replay.EsiPolicyId != input.EsiPolicyId || replay.EsiExternalReason != input.EsiExternalReason?.Trim() || input.PfApplicable && replay.PfRuleId != input.PfRuleId || input.StatutoryProfileOverride != null && NormalizeProfile(original.StatutoryApplication?.Profile) != NormalizeProfile(input.StatutoryProfileOverride)))
				throw new BusinessException("HR:Payroll:CompensationSaveConflict");
			var replayResult = await MapAsync(replay, await CanViewAuditAsync(), await activationReadinessService.GetAsync(employeeId));
			replayResult.SavedStatutoryProfile = await profileService.GetAsync(employeeId, replay.EffectiveMonth.ToString("yyyy-MM"));
			return replayResult;
		}

		var effectiveMonth = NormalizeMonth(input.EffectiveMonth);
		if (input.EsiManagementMode == EsiManagement.InPlatform)
		{
			var policy = await esiPolicies.ResolveAsync(input.EsiPolicyId, effectiveMonth);
			if (input.ExpectedEsiPolicyVersionId != policy.Rule.Id)
				throw new BusinessException(HRErrorCodes.CompensationPreviewStale);
		}
		await EnsureNoLockedPayrollAsync(employeeId, effectiveMonth);
		var revisions = await revisionRepository.GetListAsync(x => x.EmployeeId == employeeId);
		var effectiveForMonth = revisions
			.Where(x => x.EffectiveMonth == effectiveMonth && x.Status == CompensationRevisionStatus.Approved)
			.OrderByDescending(x => x.RevisionNumber)
			.FirstOrDefault();
		if (input.ExpectedRevision != effectiveForMonth?.RevisionNumber)
		{
			throw new BusinessException("HR:Payroll:CompensationSaveConflict")
				.WithData("effectiveMonth", effectiveMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture))
				.WithData("currentRevision", effectiveForMonth?.RevisionNumber);
		}

		if (input.StatutoryProfileOverride != null)
		{
			var draft = await profileService.ValidateDraftAsync(employeeId, effectiveMonth, input.StatutoryProfileOverride,
				input.StatutoryProfileConcurrencyStamp, input.StatutoryProfileUpdatePf, input.StatutoryProfileUpdateEsic);
			var previous = await profileResolver.ResolveAsync(employeeId, effectiveMonth);
			if (draft != previous)
			{
				await profileService.SaveAsync(employeeId, new SaveEmployeeStatutoryProfileDto
				{ Profile = draft, EffectiveMonth = effectiveMonth.ToString("yyyy-MM"), ConcurrencyStamp = input.StatutoryProfileConcurrencyStamp,
					IdempotencyKey = Guid.NewGuid(), UpdatePf = input.StatutoryProfileUpdatePf, UpdateEsic = input.StatutoryProfileUpdateEsic });
			}
		}
		var calculation = await salaryTemplateAppService.CalculateAsync(
			input.TemplateVersionId,
			new CalculateSalaryTemplateRequestDto
			{
				SalaryBasis = input.SalaryBasis,
				ProtectionPolicy = input.ProtectionPolicy,
				MonthlyAmount = input.MonthlyAmount,
				EffectiveMonth = effectiveMonth,
				EmployeeId = employeeId,
				PfEnrollment = input.PfEnrollment, PfApplicable = input.PfApplicable,
				PfRuleId = input.PfRuleId,
				EsiManagementMode = input.EsiManagementMode, EsiPolicyId = input.EsiPolicyId, EsiExternalReason = input.EsiExternalReason,
				StatutoryProfile = input.StatutoryProfile,
				Overrides = input.Overrides,
				CustomizationReason = input.CustomizationReason
			});
		EnsureCalculationCanBeSaved(calculation);

		foreach (var current in revisions.Where(x =>
			x.EffectiveMonth == effectiveMonth && x.Status == CompensationRevisionStatus.Approved))
		{
			current.Supersede();
			await revisionRepository.UpdateAsync(current, autoSave: false);
		}

		var revisionNumber = revisions.Select(x => x.RevisionNumber).DefaultIfEmpty().Max() + 1;
		var revision = new EmployeeCompensationRevision(
			GuidGenerator.Create(), CurrentTenant.Id, employeeId, revisionNumber, effectiveMonth, input.IdempotencyKey);
		revision.ReplaceDraft(
			effectiveMonth,
			ParseEnum<SalaryInputBasis>(calculation.SalaryBasis),
			ParseEnum<CompensationProtectionPolicy>(calculation.ProtectionPolicy),
			calculation.QuotedMonthlyAmount,
			calculation.ProtectedMonthlyAmount,
			calculation.GrossSalary,
			calculation.NetSalary,
			calculation.EmployerCost,
			calculation.TemplateId,
			calculation.TemplateVersionId,
			calculation.TemplateVersion,
			calculation.PfRuleId,
			calculation.ProfessionalTaxProfile,
			JsonSerializer.Serialize(calculation),
			JsonSerializer.Serialize(input.Overrides),
			input.CustomizationReason);
		revision.SetEsiAssignment(input.EsiManagementMode, input.EsiPolicyId, input.EsiExternalReason);
		revision.MakeEffective(CurrentUser.GetId(), clock.Now);
		await revisionRepository.InsertAsync(revision, autoSave: false);
		await InsertLinesAsync(revision, calculation);
		await UnitOfWorkManager.Current!.SaveChangesAsync();
		await payrollSnapshotReviewInvalidator.InvalidateUnlockedForEmployeesAsync([employeeId]);
		var result = await MapAsync(revision, await CanViewAuditAsync(), await activationReadinessService.GetAsync(employeeId));
		result.SavedStatutoryProfile = await profileService.GetAsync(employeeId, effectiveMonth.ToString("yyyy-MM"));
		return result;
	}

	private static PayrollStatutoryProfile? NormalizeProfile(PayrollStatutoryProfile? profile) => profile == null ? null : profile with
	{
		VersionId = null, EffectiveMonth = default,
		PfNumber = EmployeeStatutoryProfileVersion.NormalizeNumber(profile.PfNumber), UanNumber = EmployeeStatutoryProfileVersion.NormalizeNumber(profile.UanNumber),
		EsicInsuranceNumber = EmployeeStatutoryProfileVersion.NormalizeNumber(profile.EsicInsuranceNumber),
		PfDisabledReason = profile.ApplyPf ? null : profile.PfDisabledReason?.Trim(), EsicDisabledReason = profile.ApplyEsic ? null : profile.EsicDisabledReason?.Trim()
	};
	private static void EnsureCalculationCanBeSaved(SalaryTemplateCalculationDto calculation)
	{
		if (calculation.PfEligibility is { CanSave: false } decision)
			throw new BusinessException("HR:Payroll:PfEnrollmentBlocked").WithData("issueCode", decision.IssueCode);
		if (!calculation.TargetResult.IsExact && !calculation.TargetResult.IsExplainedByEnrollment)
		{
			throw new BusinessException("HR:Payroll:CompensationTargetNotExact")
				.WithData("difference", calculation.TargetResult.Difference);
		}
		if (!calculation.EmployeeStateInsurance.CanCalculate)
		{
			throw new BusinessException("HR:Payroll:CompensationStatutoryCalculationBlocked")
				.WithData("issueCodes", calculation.EmployeeStateInsurance.IssueCodes.Concat(calculation.Warnings).Distinct().ToArray());
		}
		var blockingIssues = calculation.EmployeeStateInsurance.IssueCodes.Where(code =>
			code is EsiStatutoryCalculator.EnrollmentReviewIssue or EsiStatutoryCalculator.InsuranceNumberIssue or
				EsiStatutoryCalculator.BasicComponentIssue or EsiStatutoryCalculator.WageClassificationIssue or EsiStatutoryCalculator.EstablishmentInactiveIssue or
				EsiStatutoryCalculator.DisabilityReliefEvidenceIssue).ToList();
		if (calculation.EmployeeStateInsurance.RegistrationPending)
		{
			blockingIssues.Remove(EsiStatutoryCalculator.InsuranceNumberIssue);
		}
		if (blockingIssues.Count > 0)
		{
			throw new BusinessException("HR:Payroll:CompensationStatutoryCalculationBlocked")
				.WithData("issueCodes", blockingIssues);
		}
	}

	private async Task InsertLinesAsync(EmployeeCompensationRevision revision, SalaryTemplateCalculationDto calculation)
	{
		var inputs = calculation.Earnings.Select(x => (Line: x, IsEarning: true))
			.Concat(calculation.Deductions.Where(x => x.Persistable).Select(x => (Line: x, IsEarning: false)))
			.ToList();
		var lines = inputs.Select((input, index) => new EmployeeCompensationRevisionLine(
			GuidGenerator.Create(), CurrentTenant.Id, revision.Id, revision.EmployeeId,
			input.Line.ComponentId == Guid.Empty ? null : input.Line.ComponentId,
			input.Line.Name, description: null, input.Line.Amount, input.IsEarning,
			ParseEnum<PayrollComponentSemanticType>(input.Line.SemanticType),
			input.Line.LineSource == "CUSTOMIZED" ? CompensationLineSource.Manual : CompensationLineSource.Template,
			index)).ToList();
		await lineRepository.InsertManyAsync(lines, autoSave: false);
	}

	private async Task EnsureNoLockedPayrollAsync(Guid employeeId, DateTime effectiveMonth)
	{
		var locked = await payrollSnapshotRepository.GetListAsync(x => x.EmployeeId == employeeId && x.IsLocked);
		if (locked.Any(x => string.CompareOrdinal(x.MonthYear, effectiveMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture)) >= 0))
		{
			throw new BusinessException("HR:Payroll:CompensationRevisionLockedPayroll")
				.WithData("effectiveMonth", effectiveMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture));
		}
	}

	private async Task<List<CompensationRevisionDto>> MapManyAsync(
		IReadOnlyList<EmployeeCompensationRevision> revisions,
		bool includeAudit,
		EmployeeActivationReadinessDto readiness)
	{
		if (revisions.Count == 0)
		{
			return [];
		}
		var revisionIds = revisions.Select(x => x.Id).ToList();
		var lines = await lineRepository.GetListAsync(x => revisionIds.Contains(x.RevisionId));
		var linesByRevision = lines.ToLookup(x => x.RevisionId);
		var currentMonth = NormalizeMonth(clock.Now);
		var currentId = revisions.Where(x => x.Status == CompensationRevisionStatus.Approved && x.EffectiveMonth <= currentMonth)
			.OrderByDescending(x => x.EffectiveMonth).ThenByDescending(x => x.RevisionNumber).FirstOrDefault()?.Id;
		return revisions.Select(x => Map(x, linesByRevision[x.Id], includeAudit, readiness, currentId, currentMonth)).ToList();
	}

	private async Task<CompensationRevisionDto> MapAsync(
		EmployeeCompensationRevision revision,
		bool includeAudit,
		EmployeeActivationReadinessDto readiness)
	{
		var lines = await lineRepository.GetListAsync(x => x.RevisionId == revision.Id);
		var currentMonth = NormalizeMonth(clock.Now);
		var currentId = await asyncExecuter.FirstOrDefaultAsync(
			(await revisionRepository.GetQueryableAsync())
				.Where(x => x.EmployeeId == revision.EmployeeId && x.Status == CompensationRevisionStatus.Approved && x.EffectiveMonth <= currentMonth)
				.OrderByDescending(x => x.EffectiveMonth)
				.ThenByDescending(x => x.RevisionNumber)
				.Select(x => (Guid?)x.Id));
		return Map(revision, lines, includeAudit, readiness, currentId, currentMonth);
	}

	private static CompensationRevisionDto Map(
		EmployeeCompensationRevision revision,
		IEnumerable<EmployeeCompensationRevisionLine> lines,
		bool includeAudit,
		EmployeeActivationReadinessDto readiness,
		Guid? currentId,
		DateTime currentMonth)
	{
		var calculation = TryDeserializeCalculation(revision.CalculationSnapshotJson);
		var overrides = TryDeserializeOverrides(revision.OverridesJson);
		return new CompensationRevisionDto
		{
			PfEnrollment = calculation?.PfEnrollment ?? new(), PfEligibility = calculation?.PfEligibility,
			Id = revision.Id,
			EmployeeId = revision.EmployeeId,
			RevisionNumber = revision.RevisionNumber,
			EffectiveMonth = revision.EffectiveMonth,
			Status = revision.Status switch
			{
				CompensationRevisionStatus.Approved => "EFFECTIVE",
				CompensationRevisionStatus.Superseded => "SUPERSEDED",
				_ => "LEGACY_NON_EFFECTIVE"
			},
			IsCurrent = revision.Id == currentId,
			IsScheduled = revision.Status == CompensationRevisionStatus.Approved && revision.EffectiveMonth > currentMonth,
			SalaryBasis = ToContractEnum(revision.SalaryBasis.ToString()),
			ProtectionPolicy = ToContractEnum(revision.ProtectionPolicy.ToString()),
			QuotedMonthlyAmount = revision.QuotedMonthlyAmount,
			ProtectedMonthlyAmount = revision.ProtectedMonthlyAmount,
			GrossSalary = revision.GrossSalary,
			NetSalary = revision.NetSalary,
			EmployerCost = revision.EmployerCost,
			SourceSalaryTemplateId = revision.SourceSalaryTemplateId,
			SourceSalaryTemplateVersionId = revision.SourceSalaryTemplateVersionId,
			SourceSalaryTemplateVersion = revision.SourceSalaryTemplateVersion,
			PfRuleId = revision.PfRuleId,
			EsiManagementMode = revision.EsiManagementMode, EsiPolicyId = revision.EsiPolicyId, EsiExternalReason = revision.EsiExternalReason,
			StatutoryProfile = revision.StatutoryProfile,
			IsCustomized = overrides.Count > 0,
			Overrides = overrides,
			CustomizationReason = revision.CustomizationReason,
			Warnings = calculation?.Warnings ?? [],
			EstimatedStatutoryDeductions = calculation?.Deductions.Where(x => x.IsStatutory && !x.Persistable).ToList() ?? [],
			ActivationReadiness = readiness,
			CalculationSnapshotJson = includeAudit ? revision.CalculationSnapshotJson : null,
			CreatorId = includeAudit ? revision.CreatorId : null,
			SavedByUserId = includeAudit ? revision.ApprovedByUserId : null,
			SavedAt = includeAudit ? revision.ApprovedAt : null,
			Lines = lines.OrderBy(x => x.DisplayOrder).Select(x => new CompensationRevisionLineDto
			{
				Id = x.Id,
				ComponentId = x.ComponentId,
				Name = x.Name,
				Description = x.Description,
				Amount = x.Amount,
				IsEarning = x.IsEarning,
				SemanticType = ToContractEnum(x.SemanticType.ToString()),
				Source = ToContractEnum(x.Source.ToString()),
				DisplayOrder = x.DisplayOrder
			}).ToList()
		};
	}

	private static SalaryTemplateCalculationDto? TryDeserializeCalculation(string json)
	{
		try { return JsonSerializer.Deserialize<SalaryTemplateCalculationDto>(json); }
		catch (JsonException) { return null; }
	}

	private static List<SalaryRuleOverrideDto> TryDeserializeOverrides(string json)
	{
		try { return JsonSerializer.Deserialize<List<SalaryRuleOverrideDto>>(json) ?? []; }
		catch (JsonException) { return []; }
	}

	private Task<bool> CanViewAuditAsync() => AuthorizationService.IsGrantedAsync(
		ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.ViewAudit);

	private static DateTime NormalizeMonth(DateTime value) =>
		DateTime.SpecifyKind(new DateTime(value.Year, value.Month, 1), DateTimeKind.Utc);

	private static TEnum ParseEnum<TEnum>(string value) where TEnum : struct, Enum
	{
		var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
		if (Enum.TryParse<TEnum>(normalized, true, out var parsed)) return parsed;
		throw new BusinessException("HR:Payroll:CompensationRevisionInvalidEnum").WithData("value", value);
	}

	private static string ToContractEnum(string value) =>
		string.Concat(value.Select((character, index) => index > 0 && char.IsUpper(character) ? "_" + character : character.ToString())).ToUpperInvariant();
}
