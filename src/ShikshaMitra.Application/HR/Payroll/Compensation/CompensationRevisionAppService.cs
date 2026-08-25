using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.RunPayroll;
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
	PayrollSnapshotReviewInvalidator payrollSnapshotReviewInvalidator,
	IAsyncQueryableExecuter asyncExecuter,
	IClock clock) : ApplicationService, ICompensationRevisionAppService
{
	public async Task<CompensationRevisionListDto> GetListAsync(Guid employeeId)
	{
		var revisions = await asyncExecuter.ToListAsync(
			(await revisionRepository.GetQueryableAsync())
				.Where(x => x.EmployeeId == employeeId)
				.OrderByDescending(x => x.EffectiveMonth)
				.ThenByDescending(x => x.RevisionNumber));
		return new CompensationRevisionListDto
		{
			Items = await MapManyAsync(revisions, await CanViewAuditAsync())
		};
	}

	public async Task<CompensationRevisionDto?> GetEffectiveAsync(Guid employeeId, DateTime month)
	{
		var effectiveMonth = NormalizeMonth(month);
		var revision = await asyncExecuter.FirstOrDefaultAsync(
			(await revisionRepository.GetQueryableAsync())
				.Where(x =>
					x.EmployeeId == employeeId &&
					x.Status == CompensationRevisionStatus.Approved &&
					x.EffectiveMonth <= effectiveMonth)
				.OrderByDescending(x => x.EffectiveMonth)
				.ThenByDescending(x => x.RevisionNumber));
		return revision == null ? null : await MapAsync(revision, await CanViewAuditAsync());
	}

	public async Task<CompensationRevisionDto> CreateAsync(Guid employeeId, CreateCompensationRevisionDto input)
	{
		var effectiveMonth = NormalizeMonth(input.EffectiveMonth);
		await EnsureNoLockedPayrollAsync(employeeId, effectiveMonth);
		if ((await revisionRepository.GetListAsync(x =>
			x.EmployeeId == employeeId && x.Status == CompensationRevisionStatus.Draft)).Count > 0)
		{
			throw new BusinessException("HR:Payroll:CompensationRevisionDraftAlreadyExists");
		}
		var calculation = await CalculateAsync(employeeId, input);
		var revisionNumber = (await revisionRepository.GetListAsync(x => x.EmployeeId == employeeId))
			.Select(x => x.RevisionNumber)
			.DefaultIfEmpty()
			.Max() + 1;
		var revision = new EmployeeCompensationRevision(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			employeeId,
			revisionNumber,
			effectiveMonth);
		ApplyCalculation(revision, calculation, input);
		await revisionRepository.InsertAsync(revision, autoSave: true);
		await ReplaceLinesAsync(revision, calculation);
		return await MapAsync(revision, await CanViewAuditAsync());
	}

	public async Task<CompensationRevisionDto> UpdateAsync(
		Guid employeeId,
		Guid revisionId,
		UpdateCompensationRevisionDto input)
	{
		var revision = await GetRevisionAsync(employeeId, revisionId);
		var effectiveMonth = NormalizeMonth(input.EffectiveMonth);
		await EnsureNoLockedPayrollAsync(employeeId, effectiveMonth);
		var calculation = await CalculateAsync(employeeId, input);
		ApplyCalculation(revision, calculation, input);
		await revisionRepository.UpdateAsync(revision, autoSave: true);
		await ReplaceLinesAsync(revision, calculation);
		return await MapAsync(revision, await CanViewAuditAsync());
	}

	public async Task<CompensationRevisionDto> SubmitAsync(Guid employeeId, Guid revisionId)
	{
		var revision = await GetRevisionAsync(employeeId, revisionId);
		await EnsureNoLockedPayrollAsync(employeeId, revision.EffectiveMonth);
		revision.Submit(CurrentUser.GetId(), clock.Now);
		await revisionRepository.UpdateAsync(revision, autoSave: true);
		return await MapAsync(revision, await CanViewAuditAsync());
	}

	public async Task<CompensationRevisionDto> ApproveAsync(Guid employeeId, Guid revisionId)
	{
		var revision = await GetRevisionAsync(employeeId, revisionId);
		await EnsureNoLockedPayrollAsync(employeeId, revision.EffectiveMonth);
		var recalculation = await salaryTemplateAppService.CalculateAsync(
			revision.SourceSalaryTemplateVersionId ?? throw new BusinessException("HR:Payroll:CompensationRevisionTemplateRequired"),
			BuildCalculationRequest(employeeId, revision));
		var storedLines = await lineRepository.GetListAsync(x => x.RevisionId == revision.Id);
		if (!CalculationMatches(revision, storedLines, recalculation))
		{
			throw new BusinessException("HR:Payroll:CompensationRevisionStale")
				.WithData("revisionId", revision.Id)
				.WithData("effectiveMonth", revision.EffectiveMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture));
		}

		var approvedForMonth = await revisionRepository.GetListAsync(x =>
			x.EmployeeId == employeeId &&
			x.EffectiveMonth == revision.EffectiveMonth &&
			x.Status == CompensationRevisionStatus.Approved &&
			x.Id != revision.Id);
		foreach (var approved in approvedForMonth)
		{
			approved.Supersede();
			await revisionRepository.UpdateAsync(approved, autoSave: false);
		}

		revision.Approve(CurrentUser.GetId(), clock.Now);
		await revisionRepository.UpdateAsync(revision, autoSave: true);
		await payrollSnapshotReviewInvalidator.InvalidateUnlockedForEmployeesAsync([employeeId]);
		return await MapAsync(revision, await CanViewAuditAsync());
	}

	public async Task<CompensationRevisionDto> RejectAsync(
		Guid employeeId,
		Guid revisionId,
		RejectCompensationRevisionDto input)
	{
		var revision = await GetRevisionAsync(employeeId, revisionId);
		revision.Reject(CurrentUser.GetId(), clock.Now, input.Reason);
		await revisionRepository.UpdateAsync(revision, autoSave: true);
		return await MapAsync(revision, await CanViewAuditAsync());
	}

	private async Task<EmployeeCompensationRevision> GetRevisionAsync(Guid employeeId, Guid revisionId)
	{
		var revision = await revisionRepository.GetAsync(revisionId);
		if (revision.EmployeeId != employeeId)
		{
			throw new BusinessException("HR:Payroll:CompensationRevisionEmployeeMismatch");
		}

		return revision;
	}

	private async Task<SalaryTemplateCalculationDto> CalculateAsync(
		Guid employeeId,
		CompensationRevisionQuoteDto input) =>
		await salaryTemplateAppService.CalculateAsync(
			input.TemplateVersionId,
			new CalculateSalaryTemplateRequestDto
			{
				SalaryBasis = input.SalaryBasis,
				ProtectionPolicy = input.ProtectionPolicy,
				MonthlyAmount = input.MonthlyAmount,
				EffectiveMonth = NormalizeMonth(input.EffectiveMonth),
				EmployeeId = employeeId,
				PfRuleId = input.PfRuleId,
				StatutoryProfile = input.StatutoryProfile
			});

	private static CalculateSalaryTemplateRequestDto BuildCalculationRequest(
		Guid employeeId,
		EmployeeCompensationRevision revision) => new()
	{
		SalaryBasis = ToContractEnum(revision.SalaryBasis.ToString()),
		ProtectionPolicy = ToContractEnum(revision.ProtectionPolicy.ToString()),
		MonthlyAmount = revision.QuotedMonthlyAmount,
		EffectiveMonth = revision.EffectiveMonth,
		EmployeeId = employeeId,
		PfRuleId = revision.PfRuleId,
		StatutoryProfile = revision.StatutoryProfile
	};

	private static void ApplyCalculation(
		EmployeeCompensationRevision revision,
		SalaryTemplateCalculationDto calculation,
		CompensationRevisionQuoteDto input)
	{
		revision.ReplaceDraft(
			NormalizeMonth(input.EffectiveMonth),
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
			JsonSerializer.Serialize(calculation));
	}

	private async Task ReplaceLinesAsync(
		EmployeeCompensationRevision revision,
		SalaryTemplateCalculationDto calculation)
	{
		var existing = await lineRepository.GetListAsync(x => x.RevisionId == revision.Id);
		if (existing.Count > 0)
		{
			await lineRepository.DeleteManyAsync(existing, autoSave: false);
		}

		var inputs = calculation.Earnings
			.Select(x => (Line: x, IsEarning: true))
			.Concat(calculation.Deductions.Where(x => x.Persistable).Select(x => (Line: x, IsEarning: false)))
			.ToList();
		var lines = inputs.Select((input, index) => new EmployeeCompensationRevisionLine(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			revision.Id,
			revision.EmployeeId,
			input.Line.ComponentId == Guid.Empty ? null : input.Line.ComponentId,
			input.Line.Name,
			description: null,
			input.Line.Amount,
			input.IsEarning,
			ParseEnum<PayrollComponentSemanticType>(input.Line.SemanticType),
			CompensationLineSource.Template,
			index)).ToList();
		await lineRepository.InsertManyAsync(lines, autoSave: true);
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

	private static bool CalculationMatches(
		EmployeeCompensationRevision revision,
		IReadOnlyCollection<EmployeeCompensationRevisionLine> storedLines,
		SalaryTemplateCalculationDto calculation)
	{
		if (!EqualMoney(revision.GrossSalary, calculation.GrossSalary) ||
			!EqualMoney(revision.NetSalary, calculation.NetSalary) ||
			!EqualMoney(revision.EmployerCost, calculation.EmployerCost) ||
			!EqualMoney(revision.ProtectedMonthlyAmount, calculation.ProtectedMonthlyAmount))
		{
			return false;
		}

		var currentLines = calculation.Earnings
			.Select(x => (x.ComponentId, x.Amount, IsEarning: true))
			.Concat(calculation.Deductions.Where(x => x.Persistable).Select(x => (x.ComponentId, x.Amount, IsEarning: false)))
			.OrderBy(x => x.IsEarning)
			.ThenBy(x => x.ComponentId)
			.ToList();
		var persistedLines = storedLines
			.Select(x => (ComponentId: x.ComponentId ?? Guid.Empty, x.Amount, x.IsEarning))
			.OrderBy(x => x.IsEarning)
			.ThenBy(x => x.ComponentId)
			.ToList();
		return currentLines.Count == persistedLines.Count &&
			currentLines.Zip(persistedLines).All(pair =>
				pair.First.ComponentId == pair.Second.ComponentId &&
				pair.First.IsEarning == pair.Second.IsEarning &&
				EqualMoney(pair.First.Amount, pair.Second.Amount));
	}

	private async Task<List<CompensationRevisionDto>> MapManyAsync(
		IReadOnlyList<EmployeeCompensationRevision> revisions,
		bool includeAudit)
	{
		if (revisions.Count == 0)
		{
			return [];
		}

		var revisionIds = revisions.Select(x => x.Id).ToList();
		var lines = await lineRepository.GetListAsync(x => revisionIds.Contains(x.RevisionId));
		var linesByRevision = lines.ToLookup(x => x.RevisionId);
		return revisions.Select(x => Map(x, linesByRevision[x.Id], includeAudit)).ToList();
	}

	private async Task<CompensationRevisionDto> MapAsync(EmployeeCompensationRevision revision, bool includeAudit)
	{
		var lines = await lineRepository.GetListAsync(x => x.RevisionId == revision.Id);
		return Map(revision, lines, includeAudit);
	}

	private static CompensationRevisionDto Map(
		EmployeeCompensationRevision revision,
		IEnumerable<EmployeeCompensationRevisionLine> lines,
		bool includeAudit) => new()
	{
		Id = revision.Id,
		EmployeeId = revision.EmployeeId,
		RevisionNumber = revision.RevisionNumber,
		EffectiveMonth = revision.EffectiveMonth,
		Status = ToContractEnum(revision.Status.ToString()),
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
		StatutoryProfile = revision.StatutoryProfile,
		CalculationSnapshotJson = includeAudit ? revision.CalculationSnapshotJson : null,
		CreatorId = includeAudit ? revision.CreatorId : null,
		SubmittedByUserId = includeAudit ? revision.SubmittedByUserId : null,
		SubmittedAt = includeAudit ? revision.SubmittedAt : null,
		ApprovedByUserId = includeAudit ? revision.ApprovedByUserId : null,
		ApprovedAt = includeAudit ? revision.ApprovedAt : null,
		RejectedByUserId = includeAudit ? revision.RejectedByUserId : null,
		RejectedAt = includeAudit ? revision.RejectedAt : null,
		RejectionReason = includeAudit ? revision.RejectionReason : null,
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

	private Task<bool> CanViewAuditAsync() => AuthorizationService.IsGrantedAsync(
		ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.ViewAudit);

	private static DateTime NormalizeMonth(DateTime value) =>
		DateTime.SpecifyKind(new DateTime(value.Year, value.Month, 1), DateTimeKind.Utc);

	private static bool EqualMoney(decimal left, decimal right) => Math.Abs(left - right) <= 0.01m;

	private static TEnum ParseEnum<TEnum>(string value) where TEnum : struct, Enum
	{
		var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
		if (Enum.TryParse<TEnum>(normalized, true, out var parsed))
		{
			return parsed;
		}

		throw new BusinessException("HR:Payroll:CompensationRevisionInvalidEnum").WithData("value", value);
	}

	private static string ToContractEnum(string value) =>
		string.Concat(value.Select((character, index) => index > 0 && char.IsUpper(character) ? "_" + character : character.ToString()))
			.ToUpperInvariant();
}
