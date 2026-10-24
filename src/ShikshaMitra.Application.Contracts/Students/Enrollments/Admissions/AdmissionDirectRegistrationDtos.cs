using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class StartAdmissionDirectRegistrationDto
{
	[Required]
	public Guid RequestId { get; set; }

	[Required]
	public Guid AdmissionOfferingId { get; set; }

	public Guid? ExistingFamilyId { get; set; }
	public Guid? ExistingParentApplicantId { get; set; }
	[RegularExpression("^(father|mother)$")]
	public string? ParentRelationship { get; set; }
	public Guid? CounsellorId { get; set; }
	public bool CreateSeparateFamily { get; set; }
	[StringLength(512)]
	public string? SeparateFamilyReason { get; set; }

	[Required, StringLength(128, MinimumLength = 1)]
	public string ApplicantFirstName { get; set; } = string.Empty;
	[StringLength(128)]
	public string? ApplicantMiddleName { get; set; }
	[StringLength(128)]
	public string? ApplicantLastName { get; set; }
	[Required]
	public DateTime? ApplicantDateOfBirth { get; set; }
	[Required, StringLength(32)]
	public string? ApplicantGender { get; set; }

	[Required, StringLength(128, MinimumLength = 1)]
	public string GuardianFirstName { get; set; } = string.Empty;
	[StringLength(128)]
	public string? GuardianLastName { get; set; }
	[Required, StringLength(32, MinimumLength = 7)]
	public string GuardianMobile { get; set; } = string.Empty;
	[EmailAddress, StringLength(256)]
	public string? GuardianEmail { get; set; }
	public AdmissionGuardianRelationship GuardianRelationship { get; set; } = AdmissionGuardianRelationship.Parent;
}

public class AdmissionDirectFamilyMatchDto
{
	public Guid FamilyId { get; set; }
	public string FamilyNumber { get; set; } = string.Empty;
	public string PrimaryGuardianName { get; set; } = string.Empty;
	public List<string> Applicants { get; set; } = [];
	public List<AdmissionSiblingParentCandidateDto> Parents { get; set; } = [];
	public bool MatchByEmail { get; set; }
	public bool MatchByMobile { get; set; }
}

public class AdmissionSiblingParentCandidateDto
{
	public Guid SourceApplicantId { get; set; }
	public string? Relationship { get; set; }
	public string Name { get; set; } = string.Empty;
	public string FirstName { get; set; } = string.Empty;
	public string? LastName { get; set; }
	public string? Email { get; set; }
	public string? Mobile { get; set; }
}

public class AdmissionSiblingParentContactDto
{
	[RegularExpression("^(father|mother)$")]
	public string Relationship { get; set; } = string.Empty;
	[EmailAddress, StringLength(256)] public string? Email { get; set; }
	[StringLength(32)] public string? Mobile { get; set; }
}
