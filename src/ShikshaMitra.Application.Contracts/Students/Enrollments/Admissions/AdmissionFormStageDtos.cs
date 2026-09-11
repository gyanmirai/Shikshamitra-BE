using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormStageDto
{
	public Guid ApplicationId { get; set; }
	public Guid StageId { get; set; }
	public bool FormStageEnabled { get; set; }
	public AdmissionFormStagePolicy? Policy { get; set; }
	public bool IsActive { get; set; }
	public AdmissionFormInstanceStatus FormStatus { get; set; }
	public DateTime? DueAt { get; set; }
	public bool IsOverdue { get; set; }
	public int SubmissionAttempts { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public bool CanSaveDraft { get; set; }
	public bool CanSubmit { get; set; }
	public bool CanVerify { get; set; }
	public bool CanRequestCorrection { get; set; }
	public bool CanComplete { get; set; }
	public List<string> Blockers { get; set; } = [];
	public List<AdmissionFormSectionVerificationDto> Sections { get; set; } = [];
}

public class AdmissionFormSectionVerificationDto
{
	public Guid SectionId { get; set; }
	public string SectionCode { get; set; } = string.Empty;
	public string SectionName { get; set; } = string.Empty;
	public bool Required { get; set; }
	public bool Verified { get; set; }
	public Guid? VerifiedBy { get; set; }
	public DateTime? VerifiedAt { get; set; }
	public string? Reason { get; set; }
}

public class VerifyAdmissionFormSectionDto
{
	public string? ConcurrencyStamp { get; set; }
	public bool Verified { get; set; }
	public string? Reason { get; set; }
}

public class RequestAdmissionFormCorrectionDto
{
	public string? ConcurrencyStamp { get; set; }
	public string Reason { get; set; } = string.Empty;
}
