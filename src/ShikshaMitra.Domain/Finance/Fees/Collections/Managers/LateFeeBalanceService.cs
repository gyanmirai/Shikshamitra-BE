using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Finance.Fees.Collections;

/// <summary>
/// Canonical system-charge balance. Principal settlement never settles an assessed late fee.
/// Reads are side-effect free; collection/waiver preserve the assessment transactionally.
/// </summary>
public partial class LateFeeBalanceService(
	IDueFeeRepository dueFeeRepository,
	IRepository<ShikshaMitra.Domain.Finance.Fees.Settings.FeeHead, Guid> feeHeadRepository,
	ILateFeeAdmissionDateReader admissionDates,
	IRepository<StudentFeeStructure, Guid> assignmentRepository,
	IRepository<FinanceFeeAccount, Guid> accountRepository,
	IRepository<StudentFeePolicyOverride, Guid> overrideRepository,
	IRepository<CollectionPolicyExceptionWindow, Guid> windowRepository,
	IDataFilter<ISoftDelete> softDeleteFilter,
	IRepository<LateFeeCollectionAllocation, Guid> lateFeeAllocationRepository,
	IRepository<FeeCollectionDueFeeAllocation, Guid> principalAllocationRepository,
	IRepository<FeeRecordMetadata, Guid> metadataRepository,
	LateFeeWaiverLookupService waiverLookupService,
	CollectionPolicyResolver policyResolver,
	LateFeeCalculationService calculator,
	ExceptionWindowResolver windowResolver) : DomainService
{
	public sealed record Balance(decimal Total, decimal PaidAmount, decimal WaivedAmount, string Explanation, decimal ReconciliationAmount = 0m,
		bool IsCommonCharge = false)
	{
		public decimal PendingAmount => Math.Max(0m, Total - PaidAmount - WaivedAmount);
		public IReadOnlyList<AppliedInterval>? AppliedIntervals { get; init; }
		public DateOnly? AppliedFromDate => AppliedIntervals is { Count: > 0 } ? AppliedIntervals[0].FromDate : null;
		public DateOnly? AppliedThroughDate => AppliedIntervals is { Count: > 0 } ? AppliedIntervals[^1].ThroughDate : null;
		public int? AppliedDays => AppliedIntervals?.Sum(x => x.ThroughDate.DayNumber - x.FromDate.DayNumber + 1);
	}

	public async Task<Dictionary<Guid, Balance>> GetBalancesAsync(
		IReadOnlyCollection<DueFee> dues, Guid academicYearId, DateTime asOfDate, bool preserveAssessment = false)
	{
		var result = new Dictionary<Guid, Balance>();
		if (dues.Count == 0) return result;
		var requestedIds = dues.Select(x => x.Id).ToHashSet();
		dues = await ReadCompleteScopeAsync(dues, academicYearId);
		var dueIds = dues.Select(x => x.Id).ToList();
		var studentIds = dues.Where(x => x.StudentId.HasValue).Select(x => x.StudentId!.Value).Distinct().ToList();
		var assignments = await assignmentRepository.GetListAsync(x =>
			studentIds.Contains(x.StudentId) && x.AcademicYearId == academicYearId);
		var accountIds = dues.Where(x => x.FinanceFeeAccountId.HasValue).Select(x => x.FinanceFeeAccountId!.Value).Distinct().ToList();
		var accounts = await accountRepository.GetListAsync(x => accountIds.Contains(x.Id));
		if (accounts.Count != accountIds.Count || accounts.Any(x => x.AcademicYearId != academicYearId || !x.StructureId.HasValue))
			throw new BusinessException(FinanceFeeAccountErrorCodes.InvalidAccount);
		var structureIds = assignments.Select(x => x.FeeStructureStandardId)
			.Concat(accounts.Select(x => x.StructureId!.Value)).Distinct().ToList();
		List<StudentFeePolicyOverride> overrideEvidence;
		List<CollectionPolicyExceptionWindow> windowEvidence;
		// A removed exemption/grace override is still evidence that today's rules may
		// differ from a historical payment. Keep tenant filtering enabled in this read.
		using (softDeleteFilter.Disable())
		{
			overrideEvidence = await overrideRepository.GetListAsync(x =>
				studentIds.Contains(x.StudentId) && structureIds.Contains(x.FeeStructureId));
			windowEvidence = await windowRepository.GetListAsync(x => x.Scope == ExceptionWindowScope.SchoolWide
				|| x.Scope == ExceptionWindowScope.AcademicYear && x.ScopeId == academicYearId
				|| x.Scope == ExceptionWindowScope.FeeStructure && x.ScopeId.HasValue && structureIds.Contains(x.ScopeId.Value));
		}
		var overridesByAssignment = overrideEvidence.GroupBy(x => (x.StudentId, x.FeeStructureId))
			.ToDictionary(x => x.Key, x => x.ToList());
		var latePayments = await lateFeeAllocationRepository.GetListAsync(x => dueIds.Contains(x.DueFeeId));
		var principalPayments = await principalAllocationRepository.GetListAsync(x => dueIds.Contains(x.DueFeeId));
		var transactionIds = latePayments.Select(x => x.FeeRecordMetadataId)
			.Concat(principalPayments.Select(x => x.FeeRecordMetadataId)).Distinct().ToList();
		var transactions = (await metadataRepository.GetListAsync(x => transactionIds.Contains(x.Id)))
			.ToDictionary(x => x.Id);
		bool IsActive(Guid id) => transactions.TryGetValue(id, out var tx) && tx.Status != FeeCollectionStatus.Reversed;
		var paid = latePayments.Where(x => IsActive(x.FeeRecordMetadataId)).GroupBy(x => x.DueFeeId)
			.ToDictionary(x => x.Key, x => x.Sum(y => (decimal)y.Amount));
		var reconciliation = latePayments.Where(x => transactions.TryGetValue(x.FeeRecordMetadataId, out var tx)
			&& tx.Status == FeeCollectionStatus.InReconciliation).GroupBy(x => x.DueFeeId)
			.ToDictionary(x => x.Key, x => x.Sum(y => (decimal)y.Amount));
		var waived = await waiverLookupService.GetAppliedWaivedAmountsAsync(dueIds);
		var historical = principalPayments.Where(x => IsActive(x.FeeRecordMetadataId))
			.GroupBy(x => x.DueFeeId).ToDictionary(x => x.Key, x => x.ToList());
		var changed = new List<DueFee>();
		var duesByStudent = dues.Where(x => x.StudentId.HasValue).GroupBy(x => x.StudentId!.Value).ToDictionary(x => x.Key, x => x.ToList());
		var windows = new Dictionary<(Guid StructureId, DateTime Date), ActiveExceptionWindow?>();
		var contexts = new List<AccrualContext>();
		var assignmentCounts = assignments.GroupBy(x => x.StudentId).ToDictionary(x => x.Key, x => x.Count());
		var duesByAccount = dues.Where(x => x.FinanceFeeAccountId.HasValue)
			.GroupBy(x => x.FinanceFeeAccountId!.Value).ToDictionary(x => x.Key, x => x.ToList());

		var studentPolicies = await policyResolver.CreateStudentReadContextAsync(
			assignments.Where(x => x.CollectionPolicyVersionId.HasValue).ToList());

		foreach (var assignment in assignments)
		{
			// Incomplete legacy assignments cannot generate policy-owned charges. Existing
			// assessment/payment evidence remains visible even when no version was pinned.
			if (assignment.CollectionPolicyVersionId is null) continue;
			var rules = await policyResolver.ResolveEffectiveRulesForStudentAsync(
				assignment, studentPolicies);
			var studentOverrides = overridesByAssignment.GetValueOrDefault(
				(assignment.StudentId, assignment.FeeStructureStandardId), []);
			var applicableFrom = rules.Policy?.EnableLateFee == true && !rules.IsLateFeeExempt
				? await ReadApplicableFromAsync(assignment.AssignmentSnapshotJson, assignment.ChargeStartDate,
					assignment.StudentId, null, academicYearId) : null;
			contexts.Add(new(assignment.FeeStructureStandardId, assignment.CollectionPolicyVersionId.Value, rules,
				duesByStudent[assignment.StudentId].Where(d => d.StudentFeeStructureId == assignment.Id ||
					(d.FinanceFeeAccountId == null && d.StudentFeeStructureId == null &&
					 assignmentCounts[assignment.StudentId] == 1)).ToList(),
				date => CanRecoverWithAssignedVersion(assignment, date) &&
					!studentOverrides.Any(x => x.CreationTime > date || x.LastModificationTime > date || x.DeletionTime > date), applicableFrom));
		}
		// Reuse immutable version rows only within this calculation; policies remain per payer.
		var accountVersions = studentPolicies.Versions.ToDictionary(x => x.Key, x => (FinancePolicyVersion?)x.Value);
		foreach (var account in accounts.Where(x => !x.StudentFeeStructureId.HasValue))
		{
			var rules = await policyResolver.ResolveEffectiveRulesForAccountAsync(account, accountVersions);
			var version = account.GetPolicyReference("collection")
				?? throw new BusinessException(CollectionPolicyErrorCodes.PolicyVersionNotFound);
			var applicableFrom = rules.Policy?.EnableLateFee == true && !rules.IsLateFeeExempt
				? await ReadApplicableFromAsync(account.AssignmentSnapshotJson, null,
					account.StudentId, account.ApplicationId, academicYearId) : null;
			contexts.Add(new(account.StructureId!.Value, version.VersionId, rules,
				duesByAccount.GetValueOrDefault(account.Id, []), date => account.CreationTime <= date, applicableFrom));
		}
		foreach (var context in contexts)
		{
			await AccrueContextAsync(context, academicYearId, asOfDate, preserveAssessment, paid, waived,
				reconciliation, historical, transactions, windowEvidence, windows, result, changed);
		}
		foreach (var due in dues.Where(x => !result.ContainsKey(x.Id)))
			result[due.Id] = new Balance(Math.Max(due.GetAssessedLateFeeAmount(), paid.GetValueOrDefault(due.Id) + waived.GetValueOrDefault(due.Id)),
				paid.GetValueOrDefault(due.Id), waived.GetValueOrDefault(due.Id), due.GetLateFeeAssessmentExplanation()!, reconciliation.GetValueOrDefault(due.Id))
				{ AppliedIntervals = ReadAssessmentIntervals(due) };
		if (changed.Count > 0) await dueFeeRepository.UpdateManyAsync(changed, autoSave: false);
		return result.Where(x => requestedIds.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
	}

	private static bool TryReadPrincipalBefore(string? json, Guid dueId, out decimal outstanding,
		out DateTime recoveryTimestamp, out DateTime calendarDate)
	{
		outstanding = 0m; recoveryTimestamp = default; calendarDate = default;
		if (string.IsNullOrWhiteSpace(json)) return false;
		try
		{
			using var document = JsonDocument.Parse(json);
			var root = document.RootElement;
			if (root.ValueKind != JsonValueKind.Object
				|| !root.TryGetProperty("CollectedAt", out var collectedAt) || collectedAt.ValueKind != JsonValueKind.String
				|| !collectedAt.TryGetDateTimeOffset(out var recordedAt)
				|| !collectedAt.TryGetDateTime(out recoveryTimestamp)
				|| !root.TryGetProperty("AuditSummary", out var summary) || summary.ValueKind != JsonValueKind.Object
				|| !summary.TryGetProperty("AllocationLines", out var lines) || lines.ValueKind != JsonValueKind.Array) return false;
			// Late days use the payment's recorded calendar, never a server-local
			// conversion that can move an offset timestamp onto the following day.
			// Keep the existing timestamp basis for policy/audit chronology guards.
			calendarDate = recordedAt.DateTime;
			foreach (var line in lines.EnumerateArray())
			{
				if (line.ValueKind != JsonValueKind.Object) return false;
				if (line.TryGetProperty("DueFeeId", out var id) && id.ValueKind == JsonValueKind.String
					&& id.TryGetGuid(out var parsed) && parsed == dueId
					&& line.TryGetProperty("PendingBefore", out var pending) && pending.ValueKind == JsonValueKind.Number
					&& pending.TryGetDecimal(out outstanding)) return true;
			}
			return false;
		}
		catch (JsonException) { return false; }
	}

	private static bool CanRecoverWithAssignedVersion(StudentFeeStructure assignment, DateTime paymentDate)
	{
		if (!assignment.ExtraProperties.TryGetValue("AssignedPolicyHistory", out var saved)) return true;
		if (saved is null || string.IsNullOrWhiteSpace(saved.ToString())) return false;
		try
		{
			using var document = JsonDocument.Parse(saved.ToString()!);
			if (document.RootElement.ValueKind != JsonValueKind.Array) return false;
			foreach (var change in document.RootElement.EnumerateArray())
			{
				if (change.ValueKind != JsonValueKind.Object
					|| !change.TryGetProperty("OccurredAt", out var when) || when.ValueKind != JsonValueKind.String
					|| !when.TryGetDateTime(out var changedAt)
					|| !change.TryGetProperty("Before", out var before) || before.ValueKind != JsonValueKind.Object
					|| !change.TryGetProperty("After", out var after) || after.ValueKind != JsonValueKind.Object
					|| !before.TryGetProperty("CollectionPolicyVersionId", out var oldVersion)
					|| !after.TryGetProperty("CollectionPolicyVersionId", out var newVersion)
					|| !IsVersionIdOrNull(oldVersion) || !IsVersionIdOrNull(newVersion)) return false;
				if (changedAt >= paymentDate && oldVersion.ToString() != newVersion.ToString()) return false;
			}
			return true;
		}
		catch (JsonException) { return false; }
	}

	private static bool IsVersionIdOrNull(JsonElement value) => value.ValueKind == JsonValueKind.Null
		|| value.ValueKind == JsonValueKind.String && value.TryGetGuid(out _);
}
