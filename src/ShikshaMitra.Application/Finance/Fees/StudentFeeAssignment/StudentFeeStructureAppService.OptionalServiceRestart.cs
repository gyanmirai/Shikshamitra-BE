using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Finance.Policies;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	private const string OptionalServiceRestarted = "optional_fee_service_restarted";
	private const string OptionalServiceEnabled = "optional_fee_service_enabled";
	private IDataProtector OptionalServiceRestartProtector => protectionProvider.CreateProtector("Finance.OptionalFeeServiceRestart.v1");

	[Authorize(ViewPermission)]
	[Authorize(AssignPermission)]
	[Authorize(StopOptionalServicePermission)]
	public async Task<OptionalServiceRestartContext> GetOptionalServiceRestartContextAsync(Guid studentId, Guid academicYearId, Guid feeHeadId)
	{
		await RequireEnrollmentAsync(studentId, academicYearId, true);
		var scope = await ReadRestartScopeAsync(studentId, academicYearId, feeHeadId);
		var resolver = LazyServiceProvider.LazyGetRequiredService<AdmissionProrationPolicyResolver>();
		var defaultPolicy = (await resolver.ResolveAsync(academicYearId))?.Id;
		var policies = await resolver.GetActiveVersionsAsync(academicYearId);
		var terms = await ReadRestartTermsAsync(scope);
		return new(scope.EarliestStart, DateOnly.FromDateTime(scope.Year.EndDate),
			scope.LatestPeriod?.MonthlyRate ?? (scope.Head.AmountBasis == "MonthlyRate" ? scope.Head.EffectiveAmount : null),
			defaultPolicy, terms.FirstOrDefault()?.Version.Id,
			policies.Select(policy => new OptionalServiceRestartOption(policy.Id, policy.PolicyName, policy.VersionNumber)).ToList(),
			terms.Select(term => new OptionalServiceRestartOption(term.Version.Id, term.Name, term.Version.VersionNumber)).ToList(),
			scope.PrepaidCredit);
	}

	[Authorize(ViewPermission)]
	[Authorize(AssignPermission)]
	[Authorize(StopOptionalServicePermission)]
	public async Task<OptionalServiceRestartResult> PreviewOptionalServiceRestartAsync(Guid studentId, OptionalServiceRestartInput input)
	{
		var command = await ValidateOptionalServiceRestartAsync(studentId, input);
		var plan = await PlanOptionalServiceRestartAsync(studentId, input);
		var assertion = new PreviewAssertion(CurrentTenant.Id, CurrentUser.Id, command, plan.Fingerprint, Clock.Now.AddMinutes(20));
		return plan.Result with { PreviewToken = OptionalServiceRestartProtector.Protect(JsonSerializer.Serialize(assertion)) };
	}

	[Authorize(ViewPermission)]
	[Authorize(AssignPermission)]
	[Authorize(StopOptionalServicePermission)]
	public async Task<OptionalServiceRestartResult> RestartOptionalServiceAsync(Guid studentId, OptionalServiceRestartInput input)
	{
		var acceptedAt = Clock.Now;
		try
		{
			return await CommitOptionalServiceRestartAsync(studentId, input, acceptedAt);
		}
		catch (Exception ex) when (ex.GetType().Name is "DbUpdateException" or "AbpDbConcurrencyException" ||
			(ex.GetType().Name == "PostgresException" && ex.GetType().GetProperty("SqlState")?.GetValue(ex) as string == "40001"))
		{
			// The previous transaction is disposed. A concurrent retry may have completed the same operation.
			using var read = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
			var command = await ValidateOptionalServiceRestartAsync(studentId, input);
			return await ReadOptionalServiceRestartReplayAsync(studentId, input, command)
				?? throw new BusinessException(AssignmentErrorCodes.StalePreview);
		}
	}

	private async Task<OptionalServiceRestartResult> CommitOptionalServiceRestartAsync(Guid studentId, OptionalServiceRestartInput input, DateTime acceptedAt)
	{
		using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
			{ IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
		var command = await ValidateOptionalServiceRestartAsync(studentId, input);
		if (await ReadOptionalServiceRestartReplayAsync(studentId, input, command) is { } replay) return replay;

		PreviewAssertion? assertion;
		try { assertion = JsonSerializer.Deserialize<PreviewAssertion>(OptionalServiceRestartProtector.Unprotect(input.PreviewToken ?? "")); }
		catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException)
		{ throw new BusinessException(AssignmentErrorCodes.StalePreview); }
		if (assertion == null || assertion.TenantId != CurrentTenant.Id || assertion.UserId != CurrentUser.Id ||
			assertion.Command != command || assertion.Expires <= acceptedAt)
			throw new BusinessException(AssignmentErrorCodes.StalePreview);

		var plan = await PlanOptionalServiceRestartAsync(studentId, input);
		if (plan.Fingerprint != assertion.Prepared)
			throw new BusinessException(AssignmentErrorCodes.StalePreview);
		if (!await LazyServiceProvider.LazyGetRequiredService<IPaymentTermReferenceLock>()
			.AcquireAsync([plan.PaymentTermId]))
			throw new BusinessException(AssignmentErrorCodes.StalePreview);

		// Use the original stopped period as history even for assignments stopped before the end date was stored.
		if (plan.Scope.LatestPeriod == null && plan.Scope.CancellationEffectiveDate is { } cutoff &&
			!plan.Scope.Assignment.GetOptionalServiceEndDate(input.FeeHeadId).HasValue)
			plan.Scope.Assignment.RecordOptionalServiceEndDate(input.FeeHeadId, cutoff);
		plan.Scope.Assignment.AddOptionalServicePeriod(input.FeeHeadId, new(input.RequestId, input.StartDate, input.EndDate,
			null, input.MonthlyRate, plan.Policy.Id, plan.Policy.PolicyName, plan.Policy.VersionNumber,
			input.PaymentTermVersionId, plan.Result.AssignedTotal, plan.Dues.Select(due => due.Id).ToArray()));
		await studentFeeStructureRepository.UpdateAsync(plan.Scope.Assignment, autoSave: false);
		if (plan.Dues.Count > 0)
			await dueFeeRepository.InsertManyAsync(plan.Dues, autoSave: false);
		var result = plan.Result with { Lines = plan.Dues.Select(due => OptionalServiceRestartLine(due, plan.Scope.Head.FeeHeadName, true)).ToList() };
		await LazyServiceProvider.LazyGetRequiredService<FinanceAuditWriter>().WriteAsync(input.AcademicYearId,
			FinanceAuditEntityTypes.FeeAdjustment, plan.Scope.Assignment.Id,
			"Optional fee service", plan.Scope.CancellationEffectiveDate.HasValue ? OptionalServiceRestarted : OptionalServiceEnabled,
			"Recorded a new optional service period.",
			new() { StudentId = studentId }, context: new OptionalServiceRestartEvidence(command, result, input.FeeHeadId,
				plan.Scope.CancellationEffectiveDate, input.ProrationPolicyVersionId, input.PaymentTermVersionId), eventId: input.RequestId);
		await uow.CompleteAsync();
		return result;
	}

	private async Task<OptionalServiceRestartResult?> ReadOptionalServiceRestartReplayAsync(Guid studentId, OptionalServiceRestartInput input, string command)
	{
		var history = LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceAuditEvent, Guid>>();
		var replay = await history.FindAsync(item => item.Id == input.RequestId && item.TenantId == CurrentTenant.Id);
		if (replay == null) return null;
		var evidence = (replay.Action is OptionalServiceRestarted or OptionalServiceEnabled) && replay.StudentId == studentId &&
			replay.AcademicYearId == input.AcademicYearId && replay.ContextJson != null
			? JsonSerializer.Deserialize<OptionalServiceRestartEvidence>(replay.ContextJson, OptionalServiceAuditJson) : null;
		if (evidence?.Command != command) throw new BusinessException(AssignmentErrorCodes.StalePreview);
		return evidence.Result;
	}

	private async Task<string> ValidateOptionalServiceRestartAsync(Guid studentId, OptionalServiceRestartInput input)
	{
		if (studentId == Guid.Empty || input.AcademicYearId == Guid.Empty || input.FeeHeadId == Guid.Empty ||
			input.RequestId == Guid.Empty || input.ProrationPolicyVersionId == Guid.Empty || input.PaymentTermVersionId == Guid.Empty ||
			input.MonthlyRate is <= 0 or > 9999999999.99m || decimal.Round(input.MonthlyRate, 2) != input.MonthlyRate)
			throw new BusinessException(AssignmentErrorCodes.InvalidInput);
		await RequireEnrollmentAsync(studentId, input.AcademicYearId, true);
		return Hash(new { CurrentUser.Id, StudentId = studentId, Request = input with { PreviewToken = null } });
	}

	private async Task<OptionalServiceRestartScope> ReadRestartScopeAsync(Guid studentId, Guid yearId, Guid feeHeadId)
	{
		var assignment = await studentFeeStructureRepository.FindAsync(x => x.StudentId == studentId && x.AcademicYearId == yearId)
			?? throw new BusinessException(AssignmentErrorCodes.NotAssigned);
		var snapshot = SavedAssignment(assignment);
		var head = snapshot?.Heads.SingleOrDefault(x => x.FeeHeadId == feeHeadId && x.IsOptional && !x.IsOneTime);
		if (head == null || snapshot?.StudentId != studentId || snapshot.AcademicYearId != yearId)
			throw new BusinessException(AssignmentErrorCodes.OptionalServiceRestartUnavailable);
		var latest = assignment.GetOptionalServicePeriods(feeHeadId).LastOrDefault();
		var rows = await dueFeeRepository.GetListAsync(row => row.StudentId == studentId && row.FeeHeadId == feeHeadId);
		var cancelled = rows.Where(row => row.StudentFeeStructureId == assignment.Id && row.GetOptionalServiceCancellationId().HasValue).ToList();
		var end = assignment.GetOptionalServiceEndDate(feeHeadId);
		if (!end.HasValue && latest == null && cancelled.Count > 0)
		{
			var eventIds = cancelled.Select(row => row.GetOptionalServiceCancellationId()!.Value).Distinct().ToArray();
			var events = await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceAuditEvent, Guid>>()
			.GetListAsync(item => Enumerable.Contains(eventIds, item.Id) && item.TenantId == CurrentTenant.Id && item.StudentId == studentId &&
					item.AcademicYearId == yearId && item.Action == OptionalServiceStopped);
			end = events.Where(item => item.ContextJson != null)
				.Select(item => JsonSerializer.Deserialize<OptionalServiceStopEvidence>(item.ContextJson!, OptionalServiceAuditJson))
				.Where(item => item?.FeeHeadId == feeHeadId).Select(item => (DateOnly?)item!.LastServiceDate.AddDays(1)).Max();
			end ??= cancelled.Select(row => row.CoverageStartDate.HasValue ? (DateOnly?)DateOnly.FromDateTime(row.CoverageStartDate.Value) : null).Min();
		}
		if (!end.HasValue && (head.Included || latest != null))
			throw new BusinessException(AssignmentErrorCodes.OptionalServiceRestartUnavailable);
		var structure = await feeStructureRepository.GetAsync(assignment.FeeStructureStandardId);
		var detail = (await feeStructureDetailRepository.GetListAsync(row => row.FeeStructureId == structure.Id && row.FeeHeadId == feeHeadId)).SingleOrDefault();
		var metadata = await feeStructureMetadataRepository.GetAsync(structure.FeeStructureMetadataId);
		if (detail == null || !detail.IsOptional || detail.IsOneTime || metadata.AcademicBoardId is not Guid boardId)
			throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
		var year = await academicYearRepository.GetAsync(yearId);
		var yearStart = DateOnly.FromDateTime(year.StartDate);
		var assignmentStart = snapshot.ChargeStartDate == default ? yearStart : DateOnly.FromDateTime(snapshot.ChargeStartDate);
		var earliest = end ?? assignmentStart;
		if (earliest < yearStart) earliest = yearStart;
		var retained = rows.Where(row => !row.GetOptionalServiceCancellationId().HasValue &&
			(row.OriginalAmount > 0m || row.EffectiveObligationAmount > 0m)).ToList();
		if (retained.Any(row => !row.CoverageStartDate.HasValue || !row.CoverageEndDate.HasValue))
			throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
		return new(assignment, head, detail, year, boardId, latest, end, earliest,
			cancelled.Sum(row => row.AdjustableBalance), rows, retained);
	}

	private async Task<List<OptionalServiceRestartTerm>> ReadRestartTermsAsync(OptionalServiceRestartScope scope)
	{
		var pinned = scope.LatestPeriod?.PaymentTermVersionId ?? scope.Head.PaymentTermVersionId;
		var terms = await paymentTermRepository.GetListAsync(term => term.AcademicYearId == scope.Year.Id &&
			term.BoardId == scope.BoardId && (term.Status == PaymentTermStatus.Active ||
				(pinned.HasValue && term.Versions.Any(version => version.Id == pinned.Value))));
		var resolver = LazyServiceProvider.LazyGetRequiredService<PaymentTermScheduleResolver>();
		var available = new List<OptionalServiceRestartTerm>();
		foreach (var term in terms)
		{
			foreach (var version in term.Versions.Where(version =>
				(version.Status == PaymentTermVersionStatus.Published &&
					(term.Status == PaymentTermStatus.Active || version.Id == pinned)) ||
				(version.Id == pinned && version.Status == PaymentTermVersionStatus.Superseded)))
			{
				// Avoid applying structure overrides to an alternative term version.
				var schedule = resolver.ResolveServiceTerm(scope.Detail, term, version.Id);
				if (MonthlyFeePricing.IsMonthlySchedule(schedule.Installments) &&
					schedule.Installments.Any(row => DateOnly.FromDateTime(row.CoverageEndDate) >= scope.EarliestStart))
					available.Add(new(term.Name, term.IsDefault, version, schedule));
			}
		}
		return available.OrderByDescending(term => term.Version.Id == pinned).ThenByDescending(term => term.IsDefault)
			.ThenBy(term => term.Name).ThenByDescending(term => term.Version.VersionNumber).ToList();
	}

	private async Task<OptionalServiceRestartPlan> PlanOptionalServiceRestartAsync(Guid studentId, OptionalServiceRestartInput input)
	{
		var scope = await ReadRestartScopeAsync(studentId, input.AcademicYearId, input.FeeHeadId);
		if (input.StartDate < scope.EarliestStart || input.EndDate < input.StartDate || input.EndDate > DateOnly.FromDateTime(scope.Year.EndDate))
			throw new BusinessException(AssignmentErrorCodes.OptionalServiceRestartDates);
		var term = (await ReadRestartTermsAsync(scope)).SingleOrDefault(term => term.Version.Id == input.PaymentTermVersionId)
			?? throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
		var start = input.StartDate.ToDateTime(TimeOnly.MinValue);
		var end = input.EndDate.ToDateTime(TimeOnly.MinValue);
		var coveredThrough = start.AddDays(-1);
		var alreadyBilled = new Dictionary<Guid, OptionalServiceExistingInstallment>();
		var billedInstallmentIds = new HashSet<Guid>();
		foreach (var row in term.Schedule.Installments
			.Where(row => row.CoverageStartDate.Date <= end && row.CoverageEndDate.Date >= start)
			.OrderBy(row => row.CoverageStartDate))
		{
			if (row.CoverageEndDate.Date <= coveredThrough) continue;
			if (row.CoverageStartDate.Date > coveredThrough.AddDays(1)) break;
			coveredThrough = row.CoverageEndDate.Date;
			var serviceStart = row.CoverageStartDate.Date > start ? row.CoverageStartDate.Date : start;
			var serviceEnd = row.CoverageEndDate.Date < end ? row.CoverageEndDate.Date : end;
			var overlapping = scope.Retained.Where(due => due.CoverageStartDate!.Value.Date <= serviceEnd &&
				due.CoverageEndDate!.Value.Date >= serviceStart).ToList();
			if (overlapping.Count > 0)
			{
				var billed = overlapping[0];
				// Carry the existing amount only when one obligation on this assignment covers the selected dates.
				if (overlapping.Count != 1 || billed.StudentFeeStructureId != scope.Assignment.Id ||
					billed.CoverageStartDate!.Value.Date > serviceStart || billed.CoverageEndDate!.Value.Date < serviceEnd ||
					billed.ProrationEligibleStartDate?.Date > serviceStart || billed.ProrationEligibleEndDate?.Date < serviceEnd)
					throw new BusinessException(AssignmentErrorCodes.OptionalServiceRestartDates);
				billedInstallmentIds.Add(row.PaymentTermInstallmentId);
				var selectedEnd = DateOnly.FromDateTime(serviceEnd);
				alreadyBilled[billed.Id] = alreadyBilled.TryGetValue(billed.Id, out var previous)
					? previous with { CoverageEndDate = selectedEnd }
					: new(billed.Id, billed.GetDisplayInstallmentLabel(), DateOnly.FromDateTime(serviceStart),
						selectedEnd, billed.NetPayableAmount, billed.OutstandingAmount);
			}
			if (coveredThrough >= end) break;
		}
		if (coveredThrough < end) throw new BusinessException(AssignmentErrorCodes.OptionalServiceRestartDates);
		var policy = await LazyServiceProvider.LazyGetRequiredService<AdmissionProrationPolicyResolver>()
			.ResolveActiveVersionAsync(input.AcademicYearId, input.ProrationPolicyVersionId);
		var rules = JsonSerializer.Deserialize<ProrationPolicySnapshot>(policy.SnapshotJson)
			?? throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
		var dues = dueFeeManager.GenerateRestartedOptionalServiceDues(scope.Assignment, scope.Detail, term.Schedule, input.MonthlyRate,
			rules, policy.Id, start, end, scope.Year, input.RequestId, billedInstallmentIds);
		var scheduled = dues.Sum(due => due.ScheduledAmount);
		var assigned = dues.Sum(due => due.OriginalAmount);
		var result = new OptionalServiceRestartResult(input.StartDate, input.EndDate, input.MonthlyRate, scheduled, scheduled - assigned,
			assigned, scope.PrepaidCredit, dues.Select(due => OptionalServiceRestartLine(due, scope.Head.FeeHeadName, false)).ToList())
		{
			ExistingInstallments = alreadyBilled.Values.OrderBy(installment => installment.CoverageStartDate).ToList()
		};
		return new(scope, policy, term.Version.PaymentTermId, dues, result, Hash(new { scope.Assignment.Id, scope.Assignment.ConcurrencyStamp,
			scope.CancellationEffectiveDate, scope.EarliestStart, Result = result, Policy = policy.SnapshotJson,
			Dues = scope.Rows.OrderBy(row => row.Id).Select(row => new { row.Id, row.ConcurrencyStamp, row.NetPayableAmount, row.AdjustableBalance }) }));
	}

	private static StudentFeeAssignmentPreviewLineDto OptionalServiceRestartLine(DueFee due, string name, bool committed) => new()
	{
		DueFeeId = committed ? due.Id : null, FeeHeadId = due.FeeHeadId, FeeHeadName = name,
		Sequence = due.InstallmentSequence ?? 1, PaymentTermVersionId = due.PaymentTermVersionId, InstallmentLabel = due.GetDisplayInstallmentLabel(),
		DueDate = due.DueDate, DemandDate = due.DemandDate,
		CoverageStartDate = due.ProrationEligibleStartDate ?? due.CoverageStartDate,
		CoverageEndDate = due.ProrationEligibleEndDate ?? due.CoverageEndDate,
		AllocationPercentage = due.AllocationPercentage ?? 100m, ScheduledAmount = due.ScheduledAmount,
		ProratedAmount = due.OriginalAmount, ReductionAmount = due.ScheduledAmount - due.OriginalAmount,
		Factor = due.ProrationFactor, RuleType = due.ProrationRuleType?.ToString(), RoundingMode = due.ProrationRoundingMode?.ToString(),
		Explanation = due.ProrationExplanation, OutstandingAmount = due.OutstandingAmount, PaidAmount = due.PaidAmount,
		OptionalServicePeriodId = due.GetOptionalServicePeriodId(), CoverageDays = due.ProrationCoverageDays,
		EligibleDays = due.ProrationEligibleDays, ChargedDays = due.ProrationChargedDays,
		OptionalServiceCancellationAmount = due.GetOptionalServiceCancellationAmount()
	};

	private static void ProjectOptionalServicePeriods(StudentFeeStructure assignment, AssignmentHeadDto head)
	{
		head.OptionalServiceEndDate = assignment.GetOptionalServiceEndDate(head.FeeHeadId);
		var periods = assignment.GetOptionalServicePeriods(head.FeeHeadId);
		if (periods.Count > 0)
		{
			head.Included = true;
			head.OriginalOptionalServiceEndDate = assignment.GetOriginalOptionalServiceEndDate(head.FeeHeadId);
			head.OptionalServicePeriods = periods.Select(period => new OptionalFeeServicePeriodDto(period.Id, period.StartDate, period.EndDate,
				period.CancellationEffectiveDate, period.MonthlyRate, period.ProrationPolicyName, period.ProrationPolicyVersion, period.AssignedAmount)).ToList();
		}
	}

	private sealed record OptionalServiceRestartScope(StudentFeeStructure Assignment, AssignmentHeadDto Head, FeeStructureDetail Detail,
		AcademicYear Year, Guid BoardId, OptionalFeeServicePeriod? LatestPeriod, DateOnly? CancellationEffectiveDate,
		DateOnly EarliestStart, decimal PrepaidCredit, List<DueFee> Rows, List<DueFee> Retained);
	private sealed record OptionalServiceRestartTerm(string Name, bool IsDefault, PaymentTermVersion Version, ResolvedFeeHeadSchedule Schedule);
	private sealed record OptionalServiceRestartPlan(OptionalServiceRestartScope Scope, FinancePolicyVersion Policy, Guid PaymentTermId, List<DueFee> Dues,
		OptionalServiceRestartResult Result, string Fingerprint);
	private sealed record OptionalServiceRestartEvidence(string Command, OptionalServiceRestartResult Result, Guid FeeHeadId,
		DateOnly? PreviousCancellationEffectiveDate, Guid ProrationPolicyVersionId, Guid PaymentTermVersionId);
}
