using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionReviewRecord : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public Guid StageInstanceId { get; private set; }
	public int SequenceNumber { get; private set; }
	public string Status { get; private set; } = string.Empty;
	public string? Recommendation { get; private set; }
	public string? Reason { get; private set; }
	public string? Notes { get; private set; }
	public Guid? AssignedReviewerId { get; private set; }
	public DateTime? DueAt { get; private set; }
	public string InputFingerprint { get; private set; } = string.Empty;
	public string PolicySnapshotJson { get; private set; } = string.Empty;
	public string EvidenceSnapshotJson { get; private set; } = string.Empty;
	public string ChecklistSnapshotJson { get; private set; } = string.Empty;
	public Guid RecordedBy { get; private set; }
	public DateTime RecordedAt { get; private set; }
	private AdmissionReviewRecord() { }
	public AdmissionReviewRecord(Guid id, Guid? tenantId, Guid applicationId, Guid stageInstanceId, int sequenceNumber,
		string status, string? recommendation, string? reason, string? notes, Guid? assignedReviewerId, DateTime? dueAt,
		string fingerprint, string policyJson, string evidenceJson, string checklistJson, Guid actor, DateTime now) : base(id)
	{
		if (sequenceNumber < 1 || status is not ("draft" or "submitted" or "assignment") ||
			recommendation != null && recommendation is not ("approve" or "waitlist" or "reject" or "requestCorrection") ||
			reason?.Length > 1024 || notes?.Length > 1024 || status == "submitted" && (recommendation == null || string.IsNullOrWhiteSpace(reason)) ||
			status == "assignment" && string.IsNullOrWhiteSpace(reason) || actor == Guid.Empty || fingerprint.Length != 64)
			throw new BusinessException(AdmissionReviewErrorCodes.InvalidInput);
		TenantId = tenantId; ApplicationId = applicationId; StageInstanceId = stageInstanceId; SequenceNumber = sequenceNumber;
		Status = status; Recommendation = recommendation; Reason = reason?.Trim(); Notes = notes?.Trim(); AssignedReviewerId = assignedReviewerId;
		DueAt = dueAt?.ToUniversalTime(); InputFingerprint = fingerprint; PolicySnapshotJson = policyJson; EvidenceSnapshotJson = evidenceJson;
		ChecklistSnapshotJson = checklistJson; RecordedBy = actor; RecordedAt = now.ToUniversalTime();
	}
}
