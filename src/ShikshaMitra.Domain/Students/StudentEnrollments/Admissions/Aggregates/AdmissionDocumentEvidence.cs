using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionDocumentFile(string FileName, string ContentType, long Size, string Sha256Hash, string BlobName, Guid SubmittedBy);

public class AdmissionDocumentEvidence : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public Guid StageInstanceId { get; private set; }
	public string RequirementCode { get; private set; } = string.Empty;
	public string RequirementLabel { get; private set; } = string.Empty;
	public string RequirementKind { get; private set; } = string.Empty;
	public string CollectionMode { get; private set; } = string.Empty;
	public Guid? DocumentTypeId { get; private set; }
	public int VersionNumber { get; private set; }
	public string Kind { get; private set; } = string.Empty;
	public string? FileName { get; private set; }
	public string? ContentType { get; private set; }
	public long? Size { get; private set; }
	public string? Sha256Hash { get; private set; }
	public string? BlobName { get; private set; }
	public Guid? FileSubmittedBy { get; private set; }
	public Guid? OriginalSeenBy { get; private set; }
	public DateTime? OriginalSeenAt { get; private set; }
	public bool OriginalSeen => OriginalSeenBy.HasValue && OriginalSeenAt.HasValue;
	public Guid SubmittedBy { get; private set; }
	public DateTime SubmittedAt { get; private set; }
	public string? Notes { get; private set; }
	public string PolicySnapshotJson { get; private set; } = string.Empty;
	public Guid? StudentId { get; private set; }
	public Guid? StudentLinkedBy { get; private set; }
	public DateTime? StudentLinkedAt { get; private set; }
	protected AdmissionDocumentEvidence() { }
	public AdmissionDocumentEvidence(Guid id, Guid? tenantId, Guid applicationId, Guid stageId,
		AdmissionDocumentRequirement requirement, int version, string kind, Guid submittedBy, DateTime submittedAt,
		string policySnapshotJson, AdmissionDocumentFile? file = null, Guid? originalSeenBy = null, DateTime? originalSeenAt = null, string? notes = null) : base(id)
	{
		requirement.Validate();
		if (version < 1 || submittedBy == Guid.Empty || kind is not ("upload" or "original" or "waiver")) throw Invalid("Invalid document evidence.");
		if (kind == "upload" && file == null || kind == "original" && (!originalSeenBy.HasValue || !originalSeenAt.HasValue)) throw Invalid("Record the required document evidence.");
		if (kind is "original" or "waiver" && string.IsNullOrWhiteSpace(notes)) throw Invalid("Record a reason or original-document observation.");
		if (file != null && (file.Size <= 0 || file.Sha256Hash.Length != 64 || file.SubmittedBy == Guid.Empty)) throw Invalid("Invalid document file metadata.");
		TenantId = tenantId; ApplicationId = applicationId; StageInstanceId = stageId;
		RequirementCode = requirement.Code; RequirementLabel = requirement.Name; RequirementKind = requirement.Requirement;
		CollectionMode = requirement.CollectionMode; DocumentTypeId = requirement.DocumentTypeId;
		VersionNumber = version; Kind = kind; SubmittedBy = submittedBy; SubmittedAt = submittedAt;
		PolicySnapshotJson = Check.NotNullOrWhiteSpace(policySnapshotJson, nameof(policySnapshotJson));
		FileName = file == null ? null : Check.Length(file.FileName, nameof(file.FileName), 255);
		ContentType = file == null ? null : Check.Length(file.ContentType, nameof(file.ContentType), 128);
		Size = file?.Size; Sha256Hash = file?.Sha256Hash; BlobName = file?.BlobName; FileSubmittedBy = file?.SubmittedBy;
		OriginalSeenBy = originalSeenBy; OriginalSeenAt = originalSeenAt;
		Notes = string.IsNullOrWhiteSpace(notes) ? null : Check.Length(notes.Trim(), nameof(notes), 1024);
	}
	public AdmissionDocumentFile? GetFile() => BlobName == null ? null : new(FileName!, ContentType!, Size!.Value, Sha256Hash!, BlobName, FileSubmittedBy!.Value);
	public void LinkToStudent(Guid studentId, Guid linkedBy, DateTime at)
	{
		if (studentId == Guid.Empty || linkedBy == Guid.Empty || StudentId.HasValue && StudentId != studentId) throw Invalid("Document evidence cannot be linked to a different Student.");
		if (StudentId == studentId) return;
		StudentId = studentId; StudentLinkedBy = linkedBy; StudentLinkedAt = at;
	}
	private static BusinessException Invalid(string message) => new(AdmissionDocumentErrorCodes.InvalidEvidence, message);
}
