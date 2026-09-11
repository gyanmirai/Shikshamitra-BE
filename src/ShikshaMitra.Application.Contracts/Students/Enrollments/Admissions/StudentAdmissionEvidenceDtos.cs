using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Documents;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Admissions evidence retained with its source and review history after finalization.</summary>
public class StudentAdmissionEvidenceDto
{
	public Guid Id { get; set; }
	public Guid ApplicationId { get; set; }
	public Guid StageInstanceId { get; set; }
	public string RequirementCode { get; set; } = string.Empty;
	public string RequirementLabel { get; set; } = string.Empty;
	public Guid? DocumentTypeId { get; set; }
	public int VersionNumber { get; set; }
	public string Kind { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public string? FileName { get; set; }
	public string? ContentType { get; set; }
	public long? Size { get; set; }
	public bool OriginalSeen { get; set; }
	public Guid? OriginalSeenBy { get; set; }
	public DateTime? OriginalSeenAt { get; set; }
	public Guid SubmittedBy { get; set; }
	public DateTime SubmittedAt { get; set; }
	public string? Notes { get; set; }
	public DateTime? StudentLinkedAt { get; set; }
	public Guid? StudentLinkedBy { get; set; }
	public bool CanDownload { get; set; }
	public List<StudentAdmissionEvidenceReviewDto> Reviews { get; set; } = [];
}

public class StudentAdmissionEvidenceReviewDto
{
	public Guid Id { get; set; }
	public string Decision { get; set; } = string.Empty;
	public string? Reason { get; set; }
	public Guid ReviewedBy { get; set; }
	public DateTime ReviewedAt { get; set; }
}

/// <summary>Scoped, read-only access to admissions evidence from the student record.</summary>
public interface IStudentAdmissionEvidenceAppService : IApplicationService
{
	Task<List<StudentAdmissionEvidenceDto>> GetListAsync(Guid studentId);
	Task<StudentDocumentDownloadDto> DownloadAsync(Guid studentId, Guid evidenceId);
}
