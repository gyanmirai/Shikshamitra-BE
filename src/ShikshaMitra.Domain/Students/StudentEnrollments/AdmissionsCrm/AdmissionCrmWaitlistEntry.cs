using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmWaitlistEntry : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid SeatPoolId { get; private set; }
	public int PriorityScore { get; private set; }
	public long PositionSequence { get; private set; }
	public AdmissionCrmWaitlistStatus Status { get; private set; }
	public DateTime AddedAt { get; private set; }
	public DateTime? PromotedAt { get; private set; }
	public Guid? OfferId { get; private set; }
	public DateTime? ClosedAt { get; private set; }
	public string? ClosureReason { get; private set; }
	public bool IsProtected { get; private set; }
	public string? ProtectionReason { get; private set; }
	public bool PromotionPaused { get; private set; }
	public string? PromotionPauseReason { get; private set; }

	protected AdmissionCrmWaitlistEntry()
	{
	}

	public void UpdateGovernance(int priorityScore, bool isProtected, bool promotionPaused, string reason)
	{
		EnsureActive();
		if (priorityScore is < 0 or > 100) throw new BusinessException("AdmissionCrm:InvalidWaitlistPosition");
		var normalizedReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		PriorityScore = priorityScore;
		IsProtected = isProtected;
		ProtectionReason = isProtected ? normalizedReason : null;
		PromotionPaused = promotionPaused;
		PromotionPauseReason = promotionPaused ? normalizedReason : null;
	}

	public AdmissionCrmWaitlistEntry(
		Guid id, Guid? tenantId, Guid academicYearId, Guid leadId, Guid seatPoolId,
		int priorityScore, long positionSequence, DateTime addedAt) : base(id)
	{
		if (priorityScore is < 0 or > 100 || positionSequence <= 0)
		{
			throw new BusinessException("AdmissionCrm:InvalidWaitlistPosition");
		}
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		SeatPoolId = seatPoolId;
		PriorityScore = priorityScore;
		PositionSequence = positionSequence;
		AddedAt = addedAt.ToUniversalTime();
		Status = AdmissionCrmWaitlistStatus.Active;
	}

	public void Promote(Guid offerId, DateTime promotedAt)
	{
		EnsureActive();
		OfferId = offerId;
		PromotedAt = promotedAt.ToUniversalTime();
		Status = AdmissionCrmWaitlistStatus.Promoted;
	}

	public void MarkAccepted(DateTime acceptedAt)
	{
		if (Status != AdmissionCrmWaitlistStatus.Promoted)
		{
			throw new BusinessException("AdmissionCrm:WaitlistEntryNotPromoted");
		}
		Status = AdmissionCrmWaitlistStatus.Accepted;
		ClosedAt = acceptedAt.ToUniversalTime();
	}

	public void ClosePromotedOffer(string reason, DateTime closedAt)
	{
		if (Status != AdmissionCrmWaitlistStatus.Promoted)
		{
			throw new BusinessException("AdmissionCrm:WaitlistEntryNotPromoted");
		}
		Status = AdmissionCrmWaitlistStatus.Withdrawn;
		ClosedAt = closedAt.ToUniversalTime();
		ClosureReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
	}

	public void ReactivatePromotedOffer()
	{
		if (Status != AdmissionCrmWaitlistStatus.Promoted)
		{
			throw new BusinessException("AdmissionCrm:WaitlistEntryNotPromoted");
		}
		Status = AdmissionCrmWaitlistStatus.Active;
		OfferId = null;
		ClosedAt = null;
		ClosureReason = null;
	}

	public void ExpirePromotedOffer(DateTime expiredAt)
	{
		if (Status != AdmissionCrmWaitlistStatus.Promoted)
		{
			throw new BusinessException("AdmissionCrm:WaitlistEntryNotPromoted");
		}
		Status = AdmissionCrmWaitlistStatus.Expired;
		ClosedAt = expiredAt.ToUniversalTime();
		ClosureReason = "Promoted offer expired.";
	}

	public void Withdraw(string reason, DateTime withdrawnAt)
	{
		EnsureActive();
		Status = AdmissionCrmWaitlistStatus.Withdrawn;
		ClosedAt = withdrawnAt.ToUniversalTime();
		ClosureReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
	}

	public void Expire(DateTime expiredAt)
	{
		EnsureActive();
		Status = AdmissionCrmWaitlistStatus.Expired;
		ClosedAt = expiredAt.ToUniversalTime();
		ClosureReason = "Waitlist entry expired.";
	}

	private void EnsureActive()
	{
		if (Status != AdmissionCrmWaitlistStatus.Active)
		{
			throw new BusinessException("AdmissionCrm:WaitlistEntryNotActive");
		}
	}
}
