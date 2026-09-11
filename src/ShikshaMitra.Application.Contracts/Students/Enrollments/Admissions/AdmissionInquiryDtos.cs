using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryDto : FullAuditedEntityDto<Guid>
{
	public string InquiryNumber { get; set; } = string.Empty;
	public Guid AdmissionCycleId { get; set; }
	public Guid? AdmissionOfferingId { get; set; }
	public Guid? FamilyId { get; set; }
	public Guid? ApplicantId { get; set; }
	public Guid? AssignedCounsellorId { get; set; }
	public string ApplicantName { get; set; } = string.Empty;
	public string NormalizedPhone { get; set; } = string.Empty;
	public string? Email { get; set; }
	public AdmissionInquirySource Source { get; set; }
	public string? SourceDetail { get; set; }
	public AdmissionInquiryStatus Status { get; set; }
	public Guid? DuplicateOfId { get; set; }
	public DateTime? ContactedAt { get; set; }
	public DateTime? LastActivityAt { get; set; }
}

public class CreateAdmissionInquiryDto
{
	public Guid? ExistingFamilyId { get; set; }
	public bool CreateSeparateFamily { get; set; }
	public string? DuplicateOverrideReason { get; set; }
	public Guid AdmissionCycleId { get; set; }
	public Guid? AdmissionOfferingId { get; set; }
	public string ApplicantName { get; set; } = string.Empty;
	public string Phone { get; set; } = string.Empty;
	public string? Email { get; set; }
	public AdmissionInquirySource Source { get; set; }
	public string? SourceDetail { get; set; }
	public string GuardianFirstName { get; set; } = string.Empty;
	public string? GuardianLastName { get; set; }
	public string GuardianPhone { get; set; } = string.Empty;
	public string? GuardianEmail { get; set; }
	public string ApplicantFirstName { get; set; } = string.Empty;
	public string? ApplicantLastName { get; set; }
	public DateTime? ApplicantDateOfBirth { get; set; }
	public System.Collections.Generic.List<AdmissionFormValueDto> FormValues { get; set; } = [];
}

public class AdmissionInquiryListInput : PagedAndSortedResultRequestDto
{
	public Guid? AdmissionCycleId { get; set; }
	public Guid? AdmissionOfferingId { get; set; }
	public AdmissionInquiryStatus? Status { get; set; }
	public AdmissionInquirySource? Source { get; set; }
	public Guid? AssignedCounsellorId { get; set; }
	public string? Search { get; set; }
}

public class AdmissionInquiryDuplicateCheckInput { public Guid AdmissionCycleId { get; set; } public string GuardianPhone { get; set; } = string.Empty; public string? GuardianEmail { get; set; } public string ApplicantName { get; set; } = string.Empty; public DateTime? ApplicantDateOfBirth { get; set; } }
public class AdmissionInquiryDuplicateMatchDto { public Guid ApplicantId { get; set; } public Guid FamilyId { get; set; } public string ApplicantName { get; set; } = string.Empty; public string? GuardianPhone { get; set; } public string? GuardianEmail { get; set; } public DateTime? DateOfBirth { get; set; } }
public class AssignAdmissionCounsellorDto { public AdmissionCounsellorAssignmentMode Mode { get; set; } public Guid? CounsellorId { get; set; } public List<Guid> CandidateCounsellorIds { get; set; } = []; }
