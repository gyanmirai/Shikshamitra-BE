using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRouteAppeal : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid RouteAssignmentId { get; private set; }
	public AdmissionCrmRouteEligibilityStatus OriginalStatus { get; private set; }
	public AdmissionCrmRouteEligibilityStatus RequestedStatus { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public string EvidenceJson { get; private set; } = "[]";
	public AdmissionCrmRouteAppealStatus Status { get; private set; }
	public Guid RequestedByUserId { get; private set; }
	public DateTime RequestedAt { get; private set; }
	public Guid? DecidedByUserId { get; private set; }
	public DateTime? DecidedAt { get; private set; }
	public string? DecisionReason { get; private set; }

	protected AdmissionCrmRouteAppeal() { }

	public AdmissionCrmRouteAppeal(Guid id, Guid? tenantId, Guid academicYearId, Guid leadId,
		Guid routeAssignmentId, AdmissionCrmRouteEligibilityStatus originalStatus,
		AdmissionCrmRouteEligibilityStatus requestedStatus, string reason, string evidenceJson,
		Guid requestedByUserId, DateTime requestedAt) : base(id)
	{
		if (requestedStatus is AdmissionCrmRouteEligibilityStatus.PendingReview or AdmissionCrmRouteEligibilityStatus.Superseded)
			throw new BusinessException("AdmissionCrm:InvalidRouteAppealOutcome");
		using var document = JsonDocument.Parse(Check.NotNullOrWhiteSpace(evidenceJson, nameof(evidenceJson), AdmissionCrmConsts.MaxRouteDataJsonLength));
		if (document.RootElement.ValueKind != JsonValueKind.Array)
			throw new BusinessException("AdmissionCrm:InvalidRouteAppealEvidence");
		TenantId = tenantId; AcademicYearId = academicYearId; LeadId = leadId;
		RouteAssignmentId = routeAssignmentId; OriginalStatus = originalStatus; RequestedStatus = requestedStatus;
		Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		EvidenceJson = evidenceJson; RequestedByUserId = requestedByUserId;
		RequestedAt = requestedAt.ToUniversalTime(); Status = AdmissionCrmRouteAppealStatus.Requested;
	}

	public void Decide(bool approved, string reason, Guid userId, DateTime at)
	{
		if (Status is not (AdmissionCrmRouteAppealStatus.Requested or AdmissionCrmRouteAppealStatus.UnderReview))
			throw new BusinessException("AdmissionCrm:RouteAppealNotPending");
		if (RequestedByUserId == userId)
			throw new BusinessException("AdmissionCrm:DistinctRouteAppealApproverRequired");
		DecisionReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		DecidedByUserId = userId; DecidedAt = at.ToUniversalTime();
		Status = approved ? AdmissionCrmRouteAppealStatus.Approved : AdmissionCrmRouteAppealStatus.Rejected;
	}
}
