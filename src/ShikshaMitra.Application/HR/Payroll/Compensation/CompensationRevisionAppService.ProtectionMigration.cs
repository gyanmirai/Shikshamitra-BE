using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Users;

namespace ShikshaMitra.HR.Payroll.Compensation;

public partial class CompensationRevisionAppService
{
	private const string CompletedMigrationProof = "CompletedProtectionMigrationProofV1";

	public async Task<ProtectionMigrationPreviewDto> PreviewProtectionMigrationAsync(Guid employeeId, PreviewProtectionMigrationDto input)
	{
		await employeeAuthorizationService.CheckEmployeeScopedPermissionAsync(employeeId,
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Manage);
		var (source, request, calculation) = await BuildProtectionMigrationAsync(employeeId, input);
		return new ProtectionMigrationPreviewDto
		{
			SourceRevisionId = source.Id, PreviousPolicy = ToContractEnum(source.ProtectionPolicy.ToString()),
			PreviousGross = source.GrossSalary, PreviousNet = source.NetSalary, PreviousEmployerCost = source.EmployerCost,
			Preview = previewProofService.Issue(MigrationIdentity(source, request), calculation)
		};
	}

	[Volo.Abp.Uow.UnitOfWork(true)]
	public virtual async Task<CompensationRevisionDto> ApplyProtectionMigrationAsync(Guid employeeId, ApplyProtectionMigrationDto input)
	{
		await employeeAuthorizationService.CheckEmployeeScopedPermissionAsync(employeeId,
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Manage);
		ValidateMigrationInput(input);
		if (string.IsNullOrWhiteSpace(input.PreviewToken))
			throw new BusinessException("HR:Payroll:SalaryTemplatePreviewRequired");
		await inputRevisionStore.LockAsync();
		var key = MigrationKey(input);
		var replay = await asyncExecuter.FirstOrDefaultAsync((await revisionRepository.GetQueryableAsync())
			.Where(x => x.EmployeeId == employeeId && x.IdempotencyKey == key));
		var proofHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.PreviewToken)));
		if (replay != null)
		{
			if (replay.ApprovedByUserId != CurrentUser.GetId() || replay.GetProperty<string>(CompletedMigrationProof) != proofHash)
				throw new BusinessException("HR:Payroll:SalaryTemplatePreviewRequired");
			var result = await MapAsync(replay, await CanViewAuditAsync(), await activationReadinessService.GetAsync(employeeId));
			result.SavedStatutoryProfile = await profileService.GetAsync(employeeId, replay.EffectiveMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture));
			return result;
		}
		var (source, request, calculation) = await BuildProtectionMigrationAsync(employeeId, input);
		previewProofService.Validate(input.PreviewToken, MigrationIdentity(source, request), calculation);
		var applied = await SaveAsync(employeeId, request);
		var revision = await revisionRepository.GetAsync(applied.Id);
		revision.SetProperty(CompletedMigrationProof, proofHash);
		await revisionRepository.UpdateAsync(revision);
		return applied;
	}

	private async Task<(EmployeeCompensationRevision Source, SaveEmployeeCompensationDto Request, SalaryTemplateCalculationDto Calculation)>
		BuildProtectionMigrationAsync(Guid employeeId, PreviewProtectionMigrationDto input)
	{
		ValidateMigrationInput(input);
		var month = NormalizeMonth(input.EffectiveMonth);
		await EnsureNoLockedPayrollAsync(employeeId, month);
		var source = await asyncExecuter.FirstOrDefaultAsync((await revisionRepository.GetQueryableAsync())
			.Where(x => x.EmployeeId == employeeId && x.Status == CompensationRevisionStatus.Approved && x.EffectiveMonth <= month)
			.OrderByDescending(x => x.EffectiveMonth).ThenByDescending(x => x.RevisionNumber));
		if (source == null || source.Id != input.SourceRevisionId)
			throw new BusinessException("HR:Payroll:CompensationSaveConflict");
		if (source.SalaryBasis != SalaryInputBasis.Gross || source.ProtectionPolicy != CompensationProtectionPolicy.ProtectGross)
			throw new BusinessException("HR:Payroll:ProtectionMigrationRequiresGrossSource");
		var saved = TryDeserializeCalculation(source.CalculationSnapshotJson);
		if (saved == null || !source.SourceSalaryTemplateVersionId.HasValue || saved.GrossSalary <= 0m ||
			saved.EmployerCost <= 0m || saved.EmployerCost > 100_000_000m || saved.Earnings == null || saved.Deductions == null ||
			saved.GrossSalary != source.GrossSalary || saved.EmployerCost != source.EmployerCost ||
			saved.NetSalary != source.NetSalary || saved.NetSalary != saved.GrossSalary - saved.Deductions.Sum(x => x.Amount) ||
			saved.Earnings.Sum(x => x.Amount) != source.GrossSalary ||
			saved.EmployerCost != saved.GrossSalary + saved.EmployerContributionTotal)
			throw MissingSnapshot();
		// The approved employer cost is the only source for the new CTC quote.
		List<SalaryRuleOverrideDto> overrides;
		try { overrides = JsonSerializer.Deserialize<System.Collections.Generic.List<SalaryRuleOverrideDto>>(source.OverridesJson) ?? []; }
		catch (JsonException) { throw MissingSnapshot(); }
		var request = new SaveEmployeeCompensationDto
		{
			TemplateVersionId = source.SourceSalaryTemplateVersionId.Value,
			SalaryBasis = "EMPLOYER_COST",
			MonthlyAmount = saved.EmployerCost, EffectiveMonth = month,
			PfEnrollment = saved.PfEnrollment, PfApplicable = source.PfRuleId.HasValue, PfRuleId = source.PfRuleId,
			EsiManagementMode = source.EsiManagementMode, EsiPolicyId = source.EsiPolicyId, EsiExternalReason = source.EsiExternalReason,
			StatutoryProfile = source.StatutoryProfile,
			Overrides = overrides,
			CustomizationReason = source.CustomizationReason,
			ExpectedRevision = source.EffectiveMonth == month ? source.RevisionNumber : null,
			IdempotencyKey = MigrationKey(input)
		};
		var calculation = await salaryTemplateAppService.CalculateAsync(request.TemplateVersionId, new CalculateSalaryTemplateRequestDto
		{
			SalaryBasis = request.SalaryBasis, MonthlyAmount = request.MonthlyAmount,
			EffectiveMonth = month, EmployeeId = employeeId, PfEnrollment = request.PfEnrollment,
			PfApplicable = request.PfApplicable, PfRuleId = request.PfRuleId,
			EsiManagementMode = request.EsiManagementMode, EsiPolicyId = request.EsiPolicyId, EsiExternalReason = request.EsiExternalReason,
			StatutoryProfile = request.StatutoryProfile, Overrides = request.Overrides, CustomizationReason = request.CustomizationReason
		});
		EnsureCalculationCanBeSaved(calculation);
		request.ExpectedEsiPolicyVersionId = calculation.EmployeeStateInsurance.RuleId;
		return (source, request, calculation);
	}

	private static void ValidateMigrationInput(PreviewProtectionMigrationDto input)
	{
		if (input.SourceRevisionId == Guid.Empty || input.EffectiveMonth.Day != 1 || input.EffectiveMonth.Year < 1900)
			throw new BusinessException("HR:Payroll:ProtectionMigrationInvalidInput");
	}

	private static object MigrationIdentity(EmployeeCompensationRevision source, SaveEmployeeCompensationDto request) =>
		new { Operation = "SalaryProtectionMigrationV1", source.EmployeeId, SourceRevisionId = source.Id, source.ConcurrencyStamp, Request = request };
	private static string MigrationKey(PreviewProtectionMigrationDto input) => $"protection-v1:{input.SourceRevisionId:N}:{input.EffectiveMonth.ToString("yyyyMM", CultureInfo.InvariantCulture)}";
	private static BusinessException MissingSnapshot() => new BusinessException("HR:Payroll:ProtectionMigrationSnapshotRequired")
		.WithData("message", "The approved salary calculation is incomplete. Review and save an explicit salary revision before migrating.");
}
