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
public class LateFeeBalanceService(
	IDueFeeRepository dueFeeRepository,
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
	public sealed record Balance(decimal Total, decimal PaidAmount, decimal WaivedAmount, string Explanation, decimal ReconciliationAmount = 0m)
	{
		public decimal PendingAmount => Math.Max(0m, Total - PaidAmount - WaivedAmount);
	}

	public async Task<Dictionary<Guid, Balance>> GetBalancesAsync(
		IReadOnlyCollection<DueFee> dues, Guid academicYearId, DateTime asOfDate, bool preserveAssessment = false)
	{
		var result = new Dictionary<Guid, Balance>();
		if (dues.Count == 0) return result;
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
		var contexts = new List<(Guid StructureId, Guid VersionId, EffectiveCollectionRules Rules,
			List<DueFee> Dues, Func<DateTime, bool> CanRecover)>();

		foreach (var assignment in assignments)
		{
			// Incomplete legacy assignments cannot generate policy-owned charges. Existing
			// assessment/payment evidence remains visible even when no version was pinned.
			if (assignment.CollectionPolicyVersionId is null) continue;
			var rules = await policyResolver.ResolveEffectiveRulesForStudentAsync(
				assignment.StudentId, assignment.FeeStructureStandardId);
			var studentOverrides = overridesByAssignment.GetValueOrDefault(
				(assignment.StudentId, assignment.FeeStructureStandardId), []);
			contexts.Add((assignment.FeeStructureStandardId, assignment.CollectionPolicyVersionId.Value, rules,
				duesByStudent[assignment.StudentId].Where(d => d.StudentFeeStructureId == assignment.Id ||
					(d.FinanceFeeAccountId == null && d.StudentFeeStructureId == null)).ToList(),
				date => CanRecoverWithAssignedVersion(assignment, date) &&
					!studentOverrides.Any(x => x.CreationTime > date || x.LastModificationTime > date || x.DeletionTime > date)));
		}
		foreach (var account in accounts.Where(x => !x.StudentFeeStructureId.HasValue))
		{
			var rules = await policyResolver.ResolveEffectiveRulesForAccountAsync(account.Id);
			var version = account.GetPolicyReference("collection")
				?? throw new BusinessException(CollectionPolicyErrorCodes.PolicyVersionNotFound);
			contexts.Add((account.StructureId!.Value, version.VersionId, rules,
				dues.Where(d => d.FinanceFeeAccountId == account.Id).ToList(), date => account.CreationTime <= date));
		}
		foreach (var context in contexts)
		{
			var rules = context.Rules;
			foreach (var due in context.Dues)
			{
				var total = Math.Max(due.GetAssessedLateFeeAmount(), paid.GetValueOrDefault(due.Id) + waived.GetValueOrDefault(due.Id));
				var explanation = due.GetLateFeeAssessmentExplanation();
				async Task AccrueAsync(decimal outstanding, DateTime date)
				{
					if (outstanding <= 0m || rules.Policy is null || !due.IsBillGeneratedOn(date)) return;
					var key = (context.StructureId, date.Date);
					if (!windows.TryGetValue(key, out var window))
					{
						window = await windowResolver.ResolveActiveWindowAsync(academicYearId, key.Item1, key.Item2);
						windows[key] = window;
					}
					var calculated = calculator.Calculate(rules.Policy, new LateFeeCalculationInput(
						(date.Date - due.DueDate.Date).Days, outstanding, due.OriginalAmount, outstanding,
						due.FeeHeadId, rules.IsLateFeeExempt, rules.CustomGracePeriodDays,
						window?.Effect, window?.ExtensionDays, window?.Label));
					if (calculated.LateFee > total) { total = calculated.LateFee; explanation = calculated.Explanation; }
				}
				// Recover pre-fix charges only from immutable payment results containing the
				// actual payment date and pre-payment principal; never use today's date for a settled due.
				if (historical.TryGetValue(due.Id, out var payments))
					foreach (var payment in payments)
						if (TryReadPrincipalBefore(transactions[payment.FeeRecordMetadataId].CommandResultJson, due.Id, out var before, out var date)
							&& context.CanRecover(date)
							&& !windowEvidence.Any(x => x.CoversDate(date)
								&& (x.Scope != ExceptionWindowScope.FeeStructure || x.ScopeId == context.StructureId)
								&& (x.CreationTime > date || x.LastModificationTime > date || x.DeletionTime > date)))
							await AccrueAsync(before, date);
				await AccrueAsync(due.OutstandingAmount, asOfDate);
				result[due.Id] = new Balance(total, paid.GetValueOrDefault(due.Id), waived.GetValueOrDefault(due.Id), explanation!, reconciliation.GetValueOrDefault(due.Id));
				if (preserveAssessment && total > due.GetAssessedLateFeeAmount())
				{
					due.PreserveLateFeeAssessment(total, new DateTimeOffset(DateTime.SpecifyKind(asOfDate, DateTimeKind.Utc)),
						context.VersionId, explanation!);
					changed.Add(due);
				}
			}
		}
		foreach (var due in dues.Where(x => !result.ContainsKey(x.Id)))
			result[due.Id] = new Balance(Math.Max(due.GetAssessedLateFeeAmount(), paid.GetValueOrDefault(due.Id) + waived.GetValueOrDefault(due.Id)),
				paid.GetValueOrDefault(due.Id), waived.GetValueOrDefault(due.Id), due.GetLateFeeAssessmentExplanation()!, reconciliation.GetValueOrDefault(due.Id));
		if (changed.Count > 0) await dueFeeRepository.UpdateManyAsync(changed, autoSave: false);
		return result;
	}

	private static bool TryReadPrincipalBefore(string? json, Guid dueId, out decimal outstanding, out DateTime date)
	{
		outstanding = 0m; date = default;
		if (string.IsNullOrWhiteSpace(json)) return false;
		try
		{
			using var document = JsonDocument.Parse(json);
			var root = document.RootElement;
			if (root.ValueKind != JsonValueKind.Object
				|| !root.TryGetProperty("CollectedAt", out var collectedAt) || collectedAt.ValueKind != JsonValueKind.String
				|| !collectedAt.TryGetDateTime(out date)
				|| !root.TryGetProperty("AuditSummary", out var summary) || summary.ValueKind != JsonValueKind.Object
				|| !summary.TryGetProperty("AllocationLines", out var lines) || lines.ValueKind != JsonValueKind.Array) return false;
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
