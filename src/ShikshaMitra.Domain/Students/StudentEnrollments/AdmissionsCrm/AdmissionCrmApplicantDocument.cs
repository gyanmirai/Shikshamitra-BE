using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmApplicantDocument : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid ChecklistAssignmentId { get; private set; }
	public string RequirementKey { get; private set; } = string.Empty;
	public int Version { get; private set; }
	public AdmissionCrmApplicantDocumentStatus Status { get; private set; }
	public string? BlobName { get; private set; }
	public string? FileName { get; private set; }
	public string? ContentType { get; private set; }
	public long? FileSize { get; private set; }
	public string? Sha256Hash { get; private set; }
	public DateTime? ValidUntil { get; private set; }
	public DateTime UploadedAt { get; private set; }
	public Guid? ReviewerUserId { get; private set; }
	public string? DecisionReason { get; private set; }
	public DateTime? DecisionAt { get; private set; }
	public DateTime? OriginalSeenAt { get; private set; }
	public DateTime? SupersededAt { get; private set; }

	protected AdmissionCrmApplicantDocument() { }

	public AdmissionCrmApplicantDocument(
		Guid id, Guid? tenantId, Guid academicYearId, Guid leadId, Guid checklistAssignmentId,
		string requirementKey, int version, string blobName, string fileName, string contentType,
		long fileSize, string sha256Hash, DateTime? validUntil, DateTime uploadedAt) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		ChecklistAssignmentId = checklistAssignmentId;
		RequirementKey = Check.NotNullOrWhiteSpace(requirementKey, nameof(requirementKey), 80);
		Version = version;
		BlobName = Check.NotNullOrWhiteSpace(blobName, nameof(blobName), AdmissionCrmConsts.MaxDocumentBlobNameLength);
		FileName = Check.NotNullOrWhiteSpace(fileName, nameof(fileName), AdmissionCrmConsts.MaxDocumentFileNameLength);
		ContentType = Check.NotNullOrWhiteSpace(contentType, nameof(contentType), AdmissionCrmConsts.MaxDocumentContentTypeLength);
		FileSize = fileSize;
		Sha256Hash = Check.NotNullOrWhiteSpace(sha256Hash, nameof(sha256Hash), AdmissionCrmConsts.MaxDocumentHashLength);
		ValidUntil = validUntil;
		UploadedAt = uploadedAt;
		Status = AdmissionCrmApplicantDocumentStatus.Uploaded;
	}

	public static AdmissionCrmApplicantDocument CreateWaiver(
		Guid id, Guid? tenantId, Guid academicYearId, Guid leadId, Guid assignmentId,
		string requirementKey, int version, Guid reviewerId, string reason, DateTime at)
	{
		var document = new AdmissionCrmApplicantDocument
		{
			Id = id, TenantId = tenantId, AcademicYearId = academicYearId, LeadId = leadId,
			ChecklistAssignmentId = assignmentId, RequirementKey = Check.NotNullOrWhiteSpace(requirementKey, nameof(requirementKey), 80),
			Version = version, Status = AdmissionCrmApplicantDocumentStatus.Waived, UploadedAt = at,
			ReviewerUserId = reviewerId, DecisionReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength), DecisionAt = at
		};
		return document;
	}

	public void StartReview()
	{
		if (Status != AdmissionCrmApplicantDocumentStatus.Uploaded)
			throw new BusinessException("AdmissionCrm:DocumentCannotEnterReview");
		Status = AdmissionCrmApplicantDocumentStatus.UnderReview;
	}

	public void Verify(Guid reviewerId, DateTime at)
	{
		if (Status is not (AdmissionCrmApplicantDocumentStatus.Uploaded or AdmissionCrmApplicantDocumentStatus.UnderReview))
			throw new BusinessException("AdmissionCrm:DocumentCannotBeVerified");
		Status = AdmissionCrmApplicantDocumentStatus.Verified;
		ReviewerUserId = reviewerId;
		DecisionAt = at;
		DecisionReason = null;
	}

	public void Reject(Guid reviewerId, string reason, DateTime at)
	{
		if (Status is AdmissionCrmApplicantDocumentStatus.Waived or AdmissionCrmApplicantDocumentStatus.OriginalSeen)
			throw new BusinessException("AdmissionCrm:DocumentCannotBeRejected");
		Status = AdmissionCrmApplicantDocumentStatus.Rejected;
		ReviewerUserId = reviewerId;
		DecisionReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		DecisionAt = at;
	}

	public void MarkOriginalSeen(Guid reviewerId, DateTime at)
	{
		if (Status != AdmissionCrmApplicantDocumentStatus.Verified)
			throw new BusinessException("AdmissionCrm:DocumentMustBeVerifiedBeforeOriginalSeen");
		Status = AdmissionCrmApplicantDocumentStatus.OriginalSeen;
		ReviewerUserId = reviewerId;
		OriginalSeenAt = at;
	}

	public void MarkSuperseded(DateTime at) => SupersededAt = at;
}
