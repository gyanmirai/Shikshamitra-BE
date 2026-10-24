using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeAdjustment;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Collections;

/// <summary>Internal owner operation. Records supplies an approved source/date, never a balance or policy.</summary>
[RemoteService(false)]
public class WithdrawalProrationService(IRepository<StudentFeeStructure, Guid> assignments,
	IRepository<DueFee, Guid> dues, IRepository<FinancePolicyVersion, Guid> versions,
	IRepository<AcademicYear, Guid> years, IRepository<FinanceAuditEvent, Guid> auditEvents,
	IRepository<FeeRecordMetadata, Guid> payments, IRepository<FeeRefundRequest, Guid> refunds,
	IRepository<FeeAdjustmentRequest, Guid> adjustments, IRepository<ConcessionRequest, Guid> concessions,
	FinanceScopeAccessService scope, ProrationPolicyCalculationService calculator, FinanceAuditWriter audit)
	: ApplicationService, ITransientDependency
{
	private const string AppliedAction = "withdrawal_prorated";
	private const string ReversedAction = "withdrawal_proration_reversed";
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<WithdrawalProrationPlan> PreviewAsync(Guid studentId, Guid yearId, DateTime leavingDate)
	{
		var assignment = await assignments.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.AcademicYearId == yearId);
		// Do not attach today's policy to historic unprorated assignments.
		if (assignment?.ProrationPolicyVersionId == null) return new();
		await scope.EnsureStudentAccessibleAsync(studentId);
		await scope.EnsureCollectionYearAccessibleAsync(yearId);
		var version = await versions.FindAsync(x => x.TenantId == CurrentTenant.Id && x.Id == assignment.ProrationPolicyVersionId &&
			x.PolicyId == assignment.ProrationPolicyId && x.AcademicYearId == yearId && x.PolicyType == "proration");
		ProrationPolicySnapshot? policy;
		StudentFeeAssignmentPreviewDto? evidence;
		try
		{
			policy = version == null ? null : JsonSerializer.Deserialize<ProrationPolicySnapshot>(version.SnapshotJson);
			evidence = string.IsNullOrWhiteSpace(assignment.AssignmentSnapshotJson) ? null : JsonSerializer.Deserialize<StudentFeeAssignmentPreviewDto>(assignment.AssignmentSnapshotJson);
		}
		catch (JsonException) { throw new BusinessException(ProrationPolicyErrorCodes.WithdrawalEvidenceInvalid); }
		if (policy == null || evidence == null || policy.Rules == null || policy.PolicyId != assignment.ProrationPolicyId ||
			policy.AcademicYearId != yearId || policy.Version != version!.VersionNumber ||
			evidence.StudentId != studentId || evidence.AcademicYearId != yearId || evidence.ProrationPolicyVersionId != version.Id)
			throw new BusinessException(ProrationPolicyErrorCodes.WithdrawalEvidenceInvalid);
		var year = await years.GetAsync(x => x.TenantId == CurrentTenant.Id && x.Id == yearId);
		if (leavingDate.Date >= year.EndDate.Date) return new();
		var joining = evidence.RegistrationJoiningDate ?? evidence.ChargeStartDate;
		if (leavingDate.Date < joining.Date || leavingDate.Date < year.StartDate.Date)
			throw new BusinessException(ProrationPolicyErrorCodes.InvalidChargePeriod);
		var rows = (await dues.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.StudentFeeStructureId == assignment.Id))
			.OrderBy(x => x.InstallmentSequence).ThenBy(x => x.Id).ToList();
		var expected = evidence.Lines.Select(x => x.DueFeeId).ToHashSet();
		if (expected.Contains(null) || expected.Count != rows.Count || rows.Any(x => !expected.Contains(x.Id)))
			throw new BusinessException(ProrationPolicyErrorCodes.WithdrawalEvidenceInvalid);
		var plan = new WithdrawalProrationPlan();
		var explanations = new HashSet<string>();
		foreach (var group in rows.GroupBy(x => x.FeeHeadId))
		{
			var lines = group.ToList();
			var head = evidence.Heads.SingleOrDefault(x => x.FeeHeadId == group.Key && x.Included);
			var rule = policy.Rules.SingleOrDefault(x => x.FeeHeadId == group.Key);
			if (head == null) throw new BusinessException(ProrationPolicyErrorCodes.WithdrawalEvidenceInvalid);
			if (head.IsOneTime || rule is not { ApplyOnEarlyWithdrawal: true } ||
				(rule.EarlyWithdrawalRuleType ?? rule.RuleType) == ProrationRuleType.FullCharge) continue;
			if (lines.Any(x => x.ScheduleSource != ScheduleSource.PaymentTerm || !x.AllocationPercentage.HasValue ||
				x.ProrationPolicyVersionId != version.Id || x.GetWithdrawalProrationSource().HasValue))
				throw new BusinessException(ProrationPolicyErrorCodes.AdjustmentConflict);
			var start = joining.Date > year.StartDate.Date ? joining.Date : year.StartDate.Date;
			var calculated = AssignmentAmountCalculator.Calculate(calculator, rule, lines.Sum(x => x.ScheduledAmount),
				year.StartDate, year.EndDate, start, leavingDate);
			var credits = WithdrawalProrationAllocation.Credits(lines.Select(x => x.OriginalAmount).ToArray(),
				lines.Select(x => x.NetAmount).ToArray(), lines.Select(x => x.AllocationPercentage!.Value).ToArray(), calculated.ProratedAmount,
				lines.Select(x => Math.Max(0m, x.PaidAmount - x.RefundedAmount)).ToArray());
			for (var i = 0; i < lines.Count; i++)
				if (credits[i] > 0m) plan.Lines.Add(new(lines[i].Id, credits[i], lines[i].PaidAmount, lines[i].RefundedAmount,
					lines[i].NetAmount, lines[i].ConcessionAmount, lines[i].AdjustmentCreditAmount,
					$"{head.FeeHeadName}: withdrawal on {leavingDate:yyyy-MM-dd}. {calculated.Explanation}"));
			if (credits.Sum() > 0m) explanations.Add($"{head.FeeHeadName}: {calculated.Explanation}");
		}
		if (plan.Lines.Count == 0) return plan;
		if (!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.Recalculate))
			throw new BusinessException(ProrationPolicyErrorCodes.RecalculationPermissionRequired);
		await EnsureNoPendingAsync(studentId, yearId);
		var byDue = plan.Lines.ToDictionary(x => x.DueFeeId, x => x.Credit);
		plan.Result = new()
		{
			PolicyId = version.PolicyId, PolicyVersionId = version.Id,
			PolicyName = version.PolicyName, PolicyVersion = version.VersionNumber, JoiningDate = joining,
			ChargeStartDate = evidence.ChargeStartDate, ChargeEndDate = DateTime.SpecifyKind(leavingDate.Date, DateTimeKind.Utc),
			ScheduledAmount = rows.Sum(x => x.ScheduledAmount), AdmissionReduction = rows.Sum(x => x.ScheduledAmount - x.OriginalAmount),
			WithdrawalCredit = plan.Lines.Sum(x => x.Credit), PayableAmount = rows.Sum(x => x.OriginalAmount - x.NetAmount - byDue.GetValueOrDefault(x.Id)),
			ExcessPaidAmount = rows.Sum(x => Math.Max(0m, x.PaidAmount - x.RefundedAmount + x.NetAmount + byDue.GetValueOrDefault(x.Id) - x.OriginalAmount)),
			Explanations = explanations.ToList()
		};
		return plan;
	}

	public async Task ApplyAsync(Guid sourceId, Guid studentId, Guid yearId, DateTime leavingDate, string reason)
	{
		var recorded = await auditEvents.FindAsync(x => x.TenantId == CurrentTenant.Id && x.Id == sourceId);
		if (recorded != null)
		{
			if (recorded.Action != AppliedAction || recorded.StudentId != studentId || recorded.AcademicYearId != yearId)
				throw new BusinessException(ProrationPolicyErrorCodes.AdjustmentConflict);
			await scope.EnsureStudentAccessibleAsync(studentId);
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.Recalculate);
			if (ReadRecorded(recorded, studentId, yearId).Result?.ChargeEndDate.Date != leavingDate.Date)
				throw new BusinessException(ProrationPolicyErrorCodes.AdjustmentConflict);
			return;
		}
		var plan = await PreviewAsync(studentId, yearId, leavingDate);
		if (plan.Result == null) return;
		RequireTransaction();
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.Recalculate);
		foreach (var line in plan.Lines)
		{
			var due = await dues.GetAsync(x => x.TenantId == CurrentTenant.Id && x.Id == line.DueFeeId && x.StudentId == studentId);
			due.ApplyWithdrawalProration(sourceId, line.Credit, line.Explanation);
			await dues.UpdateAsync(due, false);
		}
		await audit.WriteAsync(yearId, FinanceAuditEntityTypes.FeeAdjustment, sourceId, "Withdrawal proration", AppliedAction,
			"Recalculated the saved fee entitlement using the actual withdrawal date.", new() { StudentId = studentId }, reason,
			context: plan, correlationId: $"withdrawal-{sourceId:N}", eventId: sourceId);
	}

	public async Task<string?> CorrectionBlockerAsync(Guid originalId, Guid studentId, Guid yearId)
	{
		var recorded = await auditEvents.FindAsync(x => x.TenantId == CurrentTenant.Id && x.Id == originalId && x.Action == AppliedAction);
		if (recorded == null) return null;
		await scope.EnsureStudentAccessibleAsync(studentId);
		await scope.EnsureCollectionYearAccessibleAsync(yearId);
		if (!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.Recalculate))
			return ProrationPolicyErrorCodes.RecalculationPermissionRequired;
		await EnsureNoPendingAsync(studentId, yearId);
		var plan = ReadRecorded(recorded, studentId, yearId);
		foreach (var line in plan.Lines)
		{
			var due = await dues.GetAsync(x => x.TenantId == CurrentTenant.Id && x.Id == line.DueFeeId && x.StudentId == studentId);
			// Later financial activity must be resolved by its owner before restoring the old entitlement.
			if (due.GetWithdrawalProrationSource() != originalId || due.GetWithdrawalProrationCredit() != line.Credit ||
				due.PaidAmount != line.Paid || due.RefundedAmount != line.Refunded || due.ConcessionAmount != line.Concession ||
				due.NetAmount != line.Net + line.Credit || due.AdjustmentCreditAmount != line.Adjustment + line.Credit)
				return ProrationPolicyErrorCodes.AdjustmentConflict;
		}
		return null;
	}

	public async Task ReverseAsync(Guid correctionId, Guid originalId, Guid studentId, Guid yearId, string reason)
	{
		var replay = await auditEvents.FindAsync(x => x.TenantId == CurrentTenant.Id && x.Id == correctionId);
		if (replay != null)
		{
			if (replay.Action != ReversedAction || replay.StudentId != studentId || replay.AcademicYearId != yearId)
				throw new BusinessException(ProrationPolicyErrorCodes.AdjustmentConflict);
			return;
		}
		var original = await auditEvents.FindAsync(x => x.TenantId == CurrentTenant.Id && x.Id == originalId && x.Action == AppliedAction);
		if (original == null) return;
		RequireTransaction();
		var blocker = await CorrectionBlockerAsync(originalId, studentId, yearId);
		if (blocker != null) throw new BusinessException(blocker);
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.Recalculate);
		var plan = ReadRecorded(original, studentId, yearId);
		foreach (var line in plan.Lines)
		{
			var due = await dues.GetAsync(x => x.TenantId == CurrentTenant.Id && x.Id == line.DueFeeId && x.StudentId == studentId);
			due.ReverseWithdrawalProration(originalId);
			await dues.UpdateAsync(due, false);
		}
		await audit.WriteAsync(yearId, FinanceAuditEntityTypes.FeeAdjustment, correctionId, "Withdrawal proration correction", ReversedAction,
			"Reversed the linked non-cash withdrawal credit; original audit evidence is retained.", new() { StudentId = studentId }, reason,
			context: new { OriginalTransitionId = originalId, plan.Lines, plan.Result }, correlationId: $"withdrawal-{originalId:N}", eventId: correctionId);
	}

	private async Task EnsureNoPendingAsync(Guid studentId, Guid yearId)
	{
		if (await payments.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.AcademicYearId == yearId && x.Status == FeeCollectionStatus.InReconciliation) ||
			await refunds.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.AcademicYearId == yearId &&
				x.Status != FeeRefundStatus.Processed && x.Status != FeeRefundStatus.Reversed && x.Status != FeeRefundStatus.Rejected && x.Status != FeeRefundStatus.Cancelled) ||
			await adjustments.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.AcademicYearId == yearId && (x.Status == FeeAdjustmentStatus.Pending || x.Status == FeeAdjustmentStatus.PartiallyApplied)) ||
			await concessions.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.AcademicYearId == yearId && (x.Status == ConcessionRequestStatus.Pending || x.Status == ConcessionRequestStatus.Draft)))
			throw new BusinessException(ProrationPolicyErrorCodes.ReconciliationPending);
	}

	private static WithdrawalProrationPlan ReadRecorded(FinanceAuditEvent evidence, Guid studentId, Guid yearId)
	{
		if (evidence.StudentId != studentId || evidence.AcademicYearId != yearId || string.IsNullOrEmpty(evidence.ContextJson))
			throw new BusinessException(ProrationPolicyErrorCodes.WithdrawalEvidenceInvalid);
		return JsonSerializer.Deserialize<WithdrawalProrationPlan>(evidence.ContextJson, JsonOptions)
			?? throw new BusinessException(ProrationPolicyErrorCodes.WithdrawalEvidenceInvalid);
	}
	private void RequireTransaction()
	{
		if (CurrentUnitOfWork?.Options.IsTransactional != true || CurrentUnitOfWork.Options.IsolationLevel != System.Data.IsolationLevel.Serializable)
			throw new InvalidOperationException("Withdrawal recalculation requires the approved transition's serializable transaction.");
	}

	internal static bool IsSourceConflict(Exception exception)
	{
		for (Exception? current = exception; current != null; current = current.InnerException)
			if (current is DbException { SqlState: "23505" } &&
				current.GetType().GetProperty("ConstraintName")?.GetValue(current) as string == "PK_Finance_AuditEvents") return true;
		return false;
	}
}

public sealed class WithdrawalProrationPlan
{
	public FeeProrationResultDto? Result { get; set; }
	public List<WithdrawalProrationLine> Lines { get; set; } = [];
}
public sealed record WithdrawalProrationLine(Guid DueFeeId, decimal Credit, decimal Paid, decimal Refunded,
	decimal Net, decimal Concession, decimal Adjustment, string Explanation);
