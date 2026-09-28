using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionOfferEventDto
{
	public Guid Id { get; set; }
	public string Action { get; set; } = string.Empty;
	public AdmissionApplicationStatus ApplicationStatus { get; set; }
	public string? Reason { get; set; }
	public Guid? ActorId { get; set; }
	public DateTime OccurredAt { get; set; }
	public string? MetadataJson { get; set; }
}

public class AdmissionOfferStageDto
{
	public Guid ApplicationId { get; set; }
	public Guid AdmissionOfferingId { get; set; }
	public Guid StageId { get; set; }
	public bool OfferStageEnabled { get; set; }
	public AdmissionOfferStagePolicy? Policy { get; set; }
	public bool IsActive { get; set; }
	public string Status { get; set; } = "ready";
	public int VersionNumber { get; set; }
	public int ExtensionCount { get; set; }
	public int ReissueCount { get; set; }
	public DateTime? IssuedAt { get; set; }
	public DateTime? ExpiresAt { get; set; }
	public DateTime? RespondedAt { get; set; }
	public Guid? AssignedCounsellorId { get; set; }
	public string? AssignedCounsellorName { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public List<AdmissionOfferEventDto> Events { get; set; } = [];
	public List<string> Blockers { get; set; } = [];
	public bool CanIssue { get; set; }
	public bool CanAccept { get; set; }
	public bool CanDecline { get; set; }
	public bool CanExtend { get; set; }
	public bool CanWithdraw { get; set; }
	public bool CanReissue { get; set; }
	public bool CanComplete { get; set; }
}

public class IssueAdmissionOfferDto
{
	public DateTime? ExpiresAt { get; set; }
	public Guid? CounsellorId { get; set; }
	public string? Reason { get; set; }
	public string IdempotencyKey { get; set; } = string.Empty;
	public string? ConcurrencyStamp { get; set; }
}

public class RespondAdmissionOfferDto
{
	public bool Accepted { get; set; }
	public string? Reason { get; set; }
	public bool ConfirmTermsAccepted { get; set; }
	public bool ConfirmConditionsSatisfied { get; set; }
	public string IdempotencyKey { get; set; } = string.Empty;
	public string? ConcurrencyStamp { get; set; }
}

public class ExtendAdmissionOfferDto
{
	public DateTime ExpiresAt { get; set; }
	public string? Reason { get; set; }
	public string IdempotencyKey { get; set; } = string.Empty;
	public string? ConcurrencyStamp { get; set; }
}

public class AdmissionOfferReasonDto
{
	public string Reason { get; set; } = string.Empty;
	public string IdempotencyKey { get; set; } = string.Empty;
	public string? ConcurrencyStamp { get; set; }
}
