using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.Compensation;

public partial class CompensationRevisionAppService
{
	public async Task<ProtectionMigrationPreviewDto> PreviewProtectionMigrationAsync(Guid employeeId, PreviewProtectionMigrationDto input)
	{
		var (source, request, calculation) = await BuildProtectionMigrationAsync(employeeId, input);
		return new ProtectionMigrationPreviewDto
		{
			SourceRevisionId = source.Id, PreviousPolicy = ToContractEnum(source.ProtectionPolicy.ToString()),
			PreviousGross = source.GrossSalary, PreviousNet = source.NetSalary, PreviousEmployerCost = source.EmployerCost,
			Preview = previewProofService.Issue(MigrationIdentity(source, request), calculation)
		};
	}

	public async Task<CompensationRevisionDto> ApplyProtectionMigrationAsync(Guid employeeId, ApplyProtectionMigrationDto input)
	{
		await payrollInputRevisions.LockAsync();
		var key = MigrationKey(input);
		var replay = await asyncExecuter.FirstOrDefaultAsync((await revisionRepository.GetQueryableAsync())
			.Where(x => x.EmployeeId == employeeId && x.IdempotencyKey == key));
		if (replay != null)
			return await MapAsync(replay, await CanViewAuditAsync(), await activationReadinessService.GetAsync(employeeId));
		var (source, request, calculation) = await BuildProtectionMigrationAsync(employeeId, input);
		previewProofService.Validate(input.PreviewToken, MigrationIdentity(source, request), calculation);
		return await SaveAsync(employeeId, request);
	}

	private async Task<(EmployeeCompensationRevision Source, SaveEmployeeCompensationDto Request, SalaryTemplateCalculationDto Calculation)>
		BuildProtectionMigrationAsync(Guid employeeId, PreviewProtectionMigrationDto input)
	{
		if (input.SourceRevisionId == Guid.Empty || input.EffectiveMonth.Day != 1 || input.EffectiveMonth.Year < 1900)
			throw new BusinessException("HR:Payroll:ProtectionMigrationInvalidInput");
		var month = NormalizeMonth(input.EffectiveMonth);
		await EnsureNoLockedPayrollAsync(employeeId, month);
		var source = await asyncExecuter.FirstOrDefaultAsync((await revisionRepository.GetQueryableAsync())
			.Where(x => x.EmployeeId == employeeId && x.Status == CompensationRevisionStatus.Approved && x.EffectiveMonth <= month)
			.OrderByDescending(x => x.EffectiveMonth).ThenByDescending(x => x.RevisionNumber));
		if (source == null || source.Id != input.SourceRevisionId)
			throw new BusinessException("HR:Payroll:CompensationSaveConflict");
		SalaryTemplateCalculationDto? saved;
		try { saved = JsonSerializer.Deserialize<SalaryTemplateCalculationDto>(source.CalculationSnapshotJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
		catch (JsonException) { throw MissingSnapshot(); }
		if (saved == null || !source.SourceSalaryTemplateVersionId.HasValue || saved.GrossSalary <= 0m ||
			saved.GrossSalary != source.GrossSalary || saved.EmployerCost != source.EmployerCost ||
			saved.NetSalary != source.NetSalary || saved.NetSalary != saved.GrossSalary - saved.Deductions.Sum(x => x.Amount) ||
			saved.Earnings.Sum(x => x.Amount) != source.GrossSalary ||
			saved.EmployerCost != saved.GrossSalary + saved.EmployerContributionTotal)
			throw MissingSnapshot();
		var takeHome = source.ProtectionPolicy == CompensationProtectionPolicy.ProtectTakeHome;
		// A legacy gross package converts using approved employer cost, never an inferred live cost.
		var target = takeHome ? saved.GrossSalary - saved.Deductions.Where(x => x.IsStatutory).Sum(x => x.Amount) : saved.EmployerCost;
		System.Collections.Generic.List<SalaryRuleOverrideDto> overrides;
		try { overrides = JsonSerializer.Deserialize<System.Collections.Generic.List<SalaryRuleOverrideDto>>(source.OverridesJson) ?? []; }
		catch (JsonException) { throw MissingSnapshot(); }
		var request = new SaveEmployeeCompensationDto
		{
			TemplateVersionId = source.SourceSalaryTemplateVersionId.Value,
			SalaryBasis = takeHome ? "TAKE_HOME" : "EMPLOYER_COST",
			ProtectionPolicy = takeHome ? "PROTECT_TAKE_HOME" : "PROTECT_EMPLOYER_COST",
			MonthlyAmount = target, EffectiveMonth = month,
			PfApplicable = source.PfRuleId.HasValue, PfRuleId = source.PfRuleId, StatutoryProfile = source.StatutoryProfile,
			Overrides = overrides,
			CustomizationReason = source.CustomizationReason,
			ExpectedRevision = source.EffectiveMonth == month ? source.RevisionNumber : null,
			IdempotencyKey = MigrationKey(input)
		};
		var calculation = await salaryTemplateAppService.CalculateAsync(request.TemplateVersionId, new CalculateSalaryTemplateRequestDto
		{
			SalaryBasis = request.SalaryBasis, ProtectionPolicy = request.ProtectionPolicy, MonthlyAmount = request.MonthlyAmount,
			EffectiveMonth = month, EmployeeId = employeeId, PfApplicable = request.PfApplicable, PfRuleId = request.PfRuleId,
			StatutoryProfile = request.StatutoryProfile, Overrides = request.Overrides, CustomizationReason = request.CustomizationReason
		});
		EnsureCalculationCanBeSaved(calculation);
		return (source, request, calculation);
	}

	private static object MigrationIdentity(EmployeeCompensationRevision source, SaveEmployeeCompensationDto request) =>
		new { Operation = "SalaryProtectionMigrationV1", source.EmployeeId, SourceRevisionId = source.Id, source.ConcurrencyStamp, Request = request };
	private static string MigrationKey(PreviewProtectionMigrationDto input) => $"protection-v1:{input.SourceRevisionId:N}:{input.EffectiveMonth:yyyyMM}";
	private static BusinessException MissingSnapshot() => new BusinessException("HR:Payroll:ProtectionMigrationSnapshotRequired")
		.WithData("message", "The approved salary calculation is incomplete. Review and save an explicit salary revision before migrating.");
}
