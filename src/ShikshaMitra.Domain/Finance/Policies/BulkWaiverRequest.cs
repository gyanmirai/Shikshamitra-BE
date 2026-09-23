using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Policies;

public enum BulkWaiverScope
{
	Class = 0,
	Section = 1,
	FeeStructure = 2,
	WholeSchool = 3
}

public enum BulkWaiverStatus
{
	PendingApproval = 0,
	Approved = 1,
	Rejected = 2,
	Applied = 3
}

/// <summary>
/// A request to waive the outstanding late fee for every due line matching a scope (a class,
/// section, fee structure, or the whole school) — implementation plan §4d. Always requires
/// approval before it can be applied: a bulk scope can span multiple Collection Policies at
/// once. Approval is all-or-nothing per resolved policy, so there is no amount threshold
/// to check against the way an individual waiver has one; requiring approval unconditionally is
/// the conservative default rather than guessing which policy's threshold should govern.
/// </summary>
public class BulkWaiverRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid AcademicYearId { get; protected set; }
	public BulkWaiverScope Scope { get; protected set; }
	/// <summary>StandardId, DivisionId, or FeeStructureId depending on Scope. Null when Scope = WholeSchool.</summary>
	public Guid? ScopeId { get; protected set; }
	public string ReasonCode { get; protected set; } = string.Empty;
	public string Reason { get; protected set; } = string.Empty;
	public Guid RequestedByUserId { get; protected set; }
	public BulkWaiverStatus Status { get; protected set; }
	public Guid? ApprovedByUserId { get; protected set; }
	public DateTime? ApprovedAt { get; protected set; }
	public DateTime? AppliedAt { get; protected set; }
	/// <summary>Preview totals captured at request time, shown to the requester before submit.</summary>
	public int PreviewAffectedStudentCount { get; protected set; }
	public int PreviewAffectedDueLineCount { get; protected set; }
	public decimal PreviewAffectedAmount { get; protected set; }
	/// <summary>The due-line IDs this request will waive — snapshotted at approval time, not
	/// re-computed at apply time, so the set of who was waived is stable and auditable even if
	/// new dues appear in the scope between approval and apply.</summary>
	public string AffectedDueFeeIdsJson { get; protected set; } = "[]";

	protected BulkWaiverRequest()
	{
	}

	public BulkWaiverRequest(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		BulkWaiverScope scope,
		Guid? scopeId,
		string reasonCode,
		string reason,
		Guid requestedByUserId,
		int previewAffectedStudentCount,
		int previewAffectedDueLineCount,
		decimal previewAffectedAmount) : base(id)
	{
		if (scope != BulkWaiverScope.WholeSchool && scopeId is null)
		{
			throw new BusinessException(BulkWaiverErrorCodes.ScopeIdRequired)
				.WithData("scope", scope.ToString());
		}

		if (string.IsNullOrWhiteSpace(reason))
		{
			throw new BusinessException(BulkWaiverErrorCodes.ReasonRequired);
		}

		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Scope = scope;
		ScopeId = scope == BulkWaiverScope.WholeSchool ? null : scopeId;
		ReasonCode = reasonCode?.Trim() ?? string.Empty;
		Reason = reason.Trim();
		RequestedByUserId = requestedByUserId;
		Status = BulkWaiverStatus.PendingApproval;
		PreviewAffectedStudentCount = previewAffectedStudentCount;
		PreviewAffectedDueLineCount = previewAffectedDueLineCount;
		PreviewAffectedAmount = previewAffectedAmount;
	}

	public void Approve(Guid approvedByUserId, DateTime approvedAt, IReadOnlyList<Guid> affectedDueFeeIds)
	{
		if (Status != BulkWaiverStatus.PendingApproval)
		{
			throw new BusinessException(BulkWaiverErrorCodes.NotPendingApproval);
		}

		Status = BulkWaiverStatus.Approved;
		ApprovedByUserId = approvedByUserId;
		ApprovedAt = approvedAt;
		AffectedDueFeeIdsJson = affectedDueFeeIds is { Count: > 0 }
			? JsonSerializer.Serialize(affectedDueFeeIds)
			: "[]";
	}

	public void Reject(Guid approvedByUserId, DateTime approvedAt)
	{
		if (Status != BulkWaiverStatus.PendingApproval)
		{
			throw new BusinessException(BulkWaiverErrorCodes.NotPendingApproval);
		}

		Status = BulkWaiverStatus.Rejected;
		ApprovedByUserId = approvedByUserId;
		ApprovedAt = approvedAt;
	}

	public void MarkApplied(DateTime appliedAt)
	{
		if (Status != BulkWaiverStatus.Approved)
		{
			throw new BusinessException(BulkWaiverErrorCodes.NotApproved);
		}

		Status = BulkWaiverStatus.Applied;
		AppliedAt = appliedAt;
	}

	public IReadOnlyList<Guid> GetAffectedDueFeeIds() =>
		string.IsNullOrWhiteSpace(AffectedDueFeeIdsJson)
			? Array.Empty<Guid>()
			: JsonSerializer.Deserialize<List<Guid>>(AffectedDueFeeIdsJson) ?? new List<Guid>();
}
