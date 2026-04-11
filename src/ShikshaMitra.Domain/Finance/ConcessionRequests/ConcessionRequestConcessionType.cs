using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Finance.ConcessionRequests;

/// <summary>
/// Step 2 — links a ConcessionRequest to selected ConcessionTypes.
/// </summary>
public class ConcessionRequestConcessionType : FullAuditedEntity<Guid>
{
	public Guid ConcessionRequestId { get; private set; }
	public Guid ConcessionTypeId { get; private set; }

	public ConcessionRequestStatus Status { get; private set; }

	// Approval / Rejection metadata
	public Guid? ApproverId { get; private set; }
	public string? ApproverMessage { get; private set; }
	public DateTime? ApprovedAt { get; private set; }

	public Guid? RejectorId { get; private set; }
	public string? RejectionReason { get; private set; }
	public DateTime? RejectedAt { get; private set; }

	// Navigation
	public virtual ICollection<ConcessionRequestFeeHeadMapping> FeeHeadMappings { get; private set; }

	protected ConcessionRequestConcessionType()
	{
		FeeHeadMappings = new List<ConcessionRequestFeeHeadMapping>();
	}

	public ConcessionRequestConcessionType(
		Guid id,
		Guid concessionRequestId,
		Guid concessionTypeId) : base(id)
	{
		ConcessionRequestId = concessionRequestId;
		ConcessionTypeId = concessionTypeId;
		Status = ConcessionRequestStatus.DRAFT;
		FeeHeadMappings = new List<ConcessionRequestFeeHeadMapping>();
	}

	/// <summary>Transitions the status from DRAFT to PENDING once the wizard is fully completed.</summary>
	public void MarkAsPending()
	{
		if (Status == ConcessionRequestStatus.DRAFT)
			Status = ConcessionRequestStatus.PENDING;
	}

	public void Approve(Guid approverId, string? message)
	{
		ApproverId = approverId;
		ApproverMessage = message;
		ApprovedAt = DateTime.UtcNow;
		Status = ConcessionRequestStatus.APPROVED;
	}

	public void Reject(Guid rejectorId, string reason)
	{
		RejectorId = rejectorId;
		RejectionReason = reason;
		RejectedAt = DateTime.UtcNow;
		Status = ConcessionRequestStatus.REJECTED;
	}
}
