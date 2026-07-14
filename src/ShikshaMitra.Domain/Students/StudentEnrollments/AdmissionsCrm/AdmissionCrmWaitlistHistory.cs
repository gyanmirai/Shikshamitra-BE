using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmWaitlistHistory : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid WaitlistEntryId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid SeatPoolId { get; private set; }
	public AdmissionCrmWaitlistHistoryAction Action { get; private set; }
	public int? PreviousPriorityScore { get; private set; }
	public int PriorityScore { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public Guid? RelatedOfferId { get; private set; }
	public Guid? ActorUserId { get; private set; }
	public DateTime OccurredAt { get; private set; }

	protected AdmissionCrmWaitlistHistory() { }
	public AdmissionCrmWaitlistHistory(Guid id, Guid? tenantId, Guid academicYearId, Guid waitlistEntryId,
		Guid leadId, Guid seatPoolId, AdmissionCrmWaitlistHistoryAction action, int? previousPriorityScore,
		int priorityScore, string reason, Guid? relatedOfferId, Guid? actorUserId, DateTime occurredAt) : base(id)
	{
		TenantId = tenantId; AcademicYearId = academicYearId; WaitlistEntryId = waitlistEntryId;
		LeadId = leadId; SeatPoolId = seatPoolId; Action = action; PreviousPriorityScore = previousPriorityScore;
		PriorityScore = priorityScore; Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		RelatedOfferId = relatedOfferId; ActorUserId = actorUserId; OccurredAt = occurredAt.ToUniversalTime();
	}
}
