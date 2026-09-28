using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Content;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionApplicationStageDto
{
	public bool DocumentCollectionAvailable { get; set; }
}

public class AdmissionDocumentVerificationDto
{
	public Guid ApplicationId { get; set; }
	public Guid StageInstanceId { get; set; }
	public bool DocumentVerificationEnabled { get; set; }
	public AdmissionDocumentPolicy? Policy { get; set; }
	public bool IsActive { get; set; }
	public bool CanCollectDuringForm { get; set; }
	public bool CanComplete { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public List<string> Blockers { get; set; } = [];
	public List<AdmissionDocumentRequirementDto> Requirements { get; set; } = [];
}
public class AdmissionDocumentRequirementDto
{
	public string[] AllowedExtensions { get; set; } = [];
	public double MaxFileSizeMb { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string Requirement { get; set; } = string.Empty;
	public string CollectionMode { get; set; } = string.Empty;
	public bool IsRequired { get; set; }
	public string ConditionStatus { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public List<string> Blockers { get; set; } = [];
	public bool CanUpload { get; set; }
	public bool CanRecordOriginal { get; set; }
	public bool CanReview { get; set; }
	public bool CanAccept { get; set; }
	public bool CanWaive { get; set; }
	public List<AdmissionDocumentEvidenceDto> Evidence { get; set; } = [];
}
public class AdmissionDocumentEvidenceDto
{
	public Guid Id { get; set; }
	public int VersionNumber { get; set; }
	public string Kind { get; set; } = string.Empty;
	public string? FileName { get; set; }
	public string? ContentType { get; set; }
	public long? Size { get; set; }
	public string? Sha256Hash { get; set; }
	public bool OriginalSeen { get; set; }
	public Guid? OriginalSeenBy { get; set; }
	public DateTime? OriginalSeenAt { get; set; }
	public Guid SubmittedBy { get; set; }
	public DateTime SubmittedAt { get; set; }
	public string? Notes { get; set; }
	public string Status { get; set; } = string.Empty;
	public bool CanDownload { get; set; }
	public List<AdmissionDocumentReviewDto> Reviews { get; set; } = [];
}
public class AdmissionDocumentReviewDto
{
	public Guid Id { get; set; }
	public string Decision { get; set; } = string.Empty;
	public string? Reason { get; set; }
	public Guid ReviewedBy { get; set; }
	public DateTime ReviewedAt { get; set; }
}
public class AdmissionDocumentActionInput
{
	[Required] public string ConcurrencyStamp { get; set; } = string.Empty;
	[StringLength(1024)] public string? Notes { get; set; }
}
public class AdmissionDocumentUploadInput : AdmissionDocumentActionInput
{
	[Required] public IRemoteStreamContent File { get; set; } = null!;
}
public class AdmissionDocumentWaiverInput : AdmissionDocumentActionInput
{
	[Required, StringLength(1024)] public string Reason { get; set; } = string.Empty;
}
public class AdmissionDocumentReviewInput : AdmissionDocumentActionInput
{
	[Required] public string Decision { get; set; } = string.Empty;
	[StringLength(1024)] public string? Reason { get; set; }
}
public class AdmissionDocumentDownloadDto
{
	public byte[] Content { get; set; } = [];
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
}
public class AdmissionDocumentClassDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Context { get; set; } = string.Empty;
}
public class AdmissionDocumentTypeDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public bool IsRequired { get; set; }
	public string[] AllowedExtensions { get; set; } = [];
	public double MaxFileSizeMb { get; set; }
}
