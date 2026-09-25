using System;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public enum LateFeeWaiverStatus
{
	Applied = 0,
	PendingApproval = 1,
	Rejected = 2
}

/// <summary>
/// Records a waiver granted (or requested) against a due line's computed late fee.
/// Deliberately does not mutate DueFee — late fee is a computed-at-read figure
/// (see collection-policy-implementation-plan.md Phase 3 notes), not a persisted
/// balance, so there is nothing on DueFee for a waiver to reduce. Instead, the
/// collect-form query subtracts the sum of Applied waivers for a due line from the
/// freshly-computed late fee before showing it to the cashier.
/// </summary>
public class LateFeeWaiverApplication : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	private const string DecisionReasonProperty = "DecisionReason";
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid DueFeeId { get; private set; }
	public Guid CollectionPolicyId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public decimal ComputedLateFeeAmount { get; private set; }
	public decimal RequestedAmount { get; private set; }
	public decimal EffectiveAmount { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public LateFeeWaiverStatus Status { get; private set; }
	public Guid AppliedByUserId { get; private set; }
	public Guid? ApprovedByUserId { get; private set; }
	public DateTime? ApprovedAt { get; private set; }
	[NotMapped]
	public string? DecisionReason => this.GetProperty<string?>(DecisionReasonProperty, null);
	/// <summary>Set when this waiver was created by applying a BulkWaiverRequest (Phase 4d) —
	/// lets every individual waiver produced by one bulk action be queried/rolled up together,
	/// reusing this per-due-line audit mechanism rather than inventing a parallel one.</summary>
	public Guid? BulkWaiverRequestId { get; private set; }
	/// <summary>Client command key used to make an immediate Fee Collection waiver retry-safe.</summary>
	public string? IdempotencyKey { get; private set; }

	protected LateFeeWaiverApplication()
	{
	}

	public LateFeeWaiverApplication(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid dueFeeId,
		Guid collectionPolicyId,
		Guid academicYearId,
		decimal computedLateFeeAmount,
		decimal requestedAmount,
		decimal effectiveAmount,
		string reason,
		bool requiresApproval,
		Guid appliedByUserId,
		Guid? bulkWaiverRequestId = null,
		string? idempotencyKey = null) : base(id)
	{
		if (string.IsNullOrWhiteSpace(reason))
		{
			throw new BusinessException(LateFeeWaiverErrorCodes.ReasonRequired);
		}

		TenantId = tenantId;
		StudentId = studentId;
		DueFeeId = dueFeeId;
		CollectionPolicyId = collectionPolicyId;
		AcademicYearId = academicYearId;
		ComputedLateFeeAmount = computedLateFeeAmount;
		RequestedAmount = requestedAmount;
		EffectiveAmount = effectiveAmount;
		Reason = reason.Trim();
		Status = requiresApproval ? LateFeeWaiverStatus.PendingApproval : LateFeeWaiverStatus.Applied;
		AppliedByUserId = appliedByUserId;
		BulkWaiverRequestId = bulkWaiverRequestId;
		IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
	}

	public void Approve(Guid approvedByUserId, DateTime approvedAt)
	{
		if (Status != LateFeeWaiverStatus.PendingApproval)
		{
			throw new BusinessException(LateFeeWaiverErrorCodes.NotPendingApproval);
		}

		Status = LateFeeWaiverStatus.Applied;
		ApprovedByUserId = approvedByUserId;
		ApprovedAt = approvedAt;
		this.SetProperty(DecisionReasonProperty, null);
	}

	public void Reject(Guid approvedByUserId, DateTime approvedAt, string reason)
	{
		if (Status != LateFeeWaiverStatus.PendingApproval)
		{
			throw new BusinessException(LateFeeWaiverErrorCodes.NotPendingApproval);
		}
		if (string.IsNullOrWhiteSpace(reason))
		{
			throw new BusinessException(LateFeeWaiverErrorCodes.DecisionReasonRequired);
		}

		Status = LateFeeWaiverStatus.Rejected;
		ApprovedByUserId = approvedByUserId;
		ApprovedAt = approvedAt;
		this.SetProperty(DecisionReasonProperty, reason.Trim());
	}
}
