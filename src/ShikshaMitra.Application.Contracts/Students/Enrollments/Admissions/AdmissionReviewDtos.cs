using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionReviewDto
{
	public Guid ApplicationId { get; set; }
	public Guid StageId { get; set; }
	public bool ReviewEnabled { get; set; }
	public AdmissionReviewPolicy? Policy { get; set; }
	public bool IsActive { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public string Status { get; set; } = "notStarted";
	public bool IsStale { get; set; }
	public Guid? AssignedReviewerId { get; set; }
	public string? AssignedReviewerName { get; set; }
	public DateTime? DueAt { get; set; }
	public bool IsOverdue { get; set; }
	public bool CanSaveDraft { get; set; }
	public bool CanSubmit { get; set; }
	public bool CanAssign { get; set; }
	public bool CanRequestCorrection { get; set; }
	public bool CanComplete { get; set; }
	public List<string> Blockers { get; set; } = [];
	public List<AdmissionReviewEvidenceDto> Prerequisites { get; set; } = [];
	public AdmissionReviewRecordDto? LatestRecord { get; set; }
	public AdmissionReviewDecisionCapabilitiesDto Decision { get; set; } = new();
}
public class AdmissionReviewEvidenceDto
{
	public string Kind { get; set; } = string.Empty;
	public string SourceStageCode { get; set; } = string.Empty;
	public string SourceStageName { get; set; } = string.Empty;
	public Guid? StageId { get; set; }
	public string Status { get; set; } = string.Empty;
	public bool Passed { get; set; }
	public bool IsStale { get; set; }
	public string Summary { get; set; } = string.Empty;
}
public class AdmissionReviewChecklistDto
{
	[Required, MaxLength(64)] public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public bool Required { get; set; }
	public bool Checked { get; set; }
	[MaxLength(1024)] public string? Notes { get; set; }
}
public class AdmissionReviewRecordDto
{
	public Guid Id { get; set; }
	public int SequenceNumber { get; set; }
	public string Status { get; set; } = string.Empty;
	public string? Notes { get; set; }
	public string? Reason { get; set; }
	public string? Recommendation { get; set; }
	public List<AdmissionReviewChecklistDto> Checklist { get; set; } = [];
	public Guid RecordedBy { get; set; }
	public DateTime RecordedAt { get; set; }
	public Guid? AssignedReviewerId { get; set; }
	public string? AssignedReviewerName { get; set; }
	public DateTime? DueAt { get; set; }
	public bool IsStale { get; set; }
	public List<AdmissionReviewEvidenceDto> Prerequisites { get; set; } = [];
	public AdmissionReviewDecisionDto? Decision { get; set; }
}
public class AdmissionReviewDecisionDto
{
	public Guid Id { get; set; }
	public Guid RecordId { get; set; }
	public string Decision { get; set; } = string.Empty;
	public string? Reason { get; set; }
	public Guid DecidedBy { get; set; }
	public DateTime DecidedAt { get; set; }
}
public class AdmissionReviewDecisionCapabilitiesDto
{
	public bool ManagedReviewEnabled { get; set; }
	public bool IsSatisfied { get; set; }
	public bool CanApprove { get; set; }
	public bool CanReject { get; set; }
	public bool CanWaitlist { get; set; }
	public List<string> Blockers { get; set; } = [];
}
public class AdmissionReviewCommandDto
{
	[Required, MaxLength(40)] public string ConcurrencyStamp { get; set; } = string.Empty;
}
public class AdmissionReviewSaveDto : AdmissionReviewCommandDto
{
	[MaxLength(1024)] public string? Notes { get; set; }
	[MaxLength(32)] public string? Recommendation { get; set; }
	[MaxLength(1024)] public string? Reason { get; set; }
	public List<AdmissionReviewChecklistDto> Checklist { get; set; } = [];
}
public class AdmissionReviewCorrectionDto : AdmissionReviewCommandDto
{
	[Required, MaxLength(1024)] public string Reason { get; set; } = string.Empty;
}
public class AdmissionReviewAssignmentDto : AdmissionReviewCorrectionDto
{
	public Guid? AssignedReviewerId { get; set; }
	public DateTime? DueAt { get; set; }
}
public class AdmissionReviewUserDto
{
	public Guid Id { get; set; }
	public string UserName { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
}
public class AdmissionReviewQueueInput : PagedResultRequestDto
{
	public AdmissionReviewQueueInput() => MaxResultCount = 20;
	public bool OverdueOnly { get; set; }
	public bool AssignedToMe { get; set; }
}
public class AdmissionReviewQueueItemDto
{
	public Guid ApplicationId { get; set; }
	public Guid StageId { get; set; }
	public string ApplicationNumber { get; set; } = string.Empty;
	public string ApplicantName { get; set; } = string.Empty;
	public string StageName { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public Guid? AssignedReviewerId { get; set; }
	public string? AssignedReviewerName { get; set; }
	public DateTime? DueAt { get; set; }
	public bool IsOverdue { get; set; }
	public bool IsStale { get; set; }
}
