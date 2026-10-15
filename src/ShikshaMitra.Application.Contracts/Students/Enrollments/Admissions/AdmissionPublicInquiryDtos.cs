using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionPublicInquiryLinkOptionDto
{
	public Guid? CounsellorAssignmentProfileId { get; set; }
	public Guid InquiryFormDefinitionId { get; set; }
}

public class CreateAdmissionPublicInquiryLinkDto
{
	[Required]
	public Guid AdmissionCycleId { get; set; }
	public Guid? CounsellorAssignmentProfileId { get; set; }
	public Guid? InquiryFormDefinitionId { get; set; }
	[Required, StringLength(128, MinimumLength = 1)]
	public string Name { get; set; } = string.Empty;
	public AdmissionInquirySource Source { get; set; }
	[StringLength(512)]
	public string? SourceDetail { get; set; }
	public DateTime? ExpiresAtUtc { get; set; }
}

public class AdmissionPublicInquiryLinkDto
{
	public Guid Id { get; set; }
	public Guid AdmissionCycleId { get; set; }
	public Guid? CounsellorAssignmentProfileId { get; set; }
	public Guid? InquiryFormDefinitionId { get; set; }
	public string Name { get; set; } = string.Empty;
	public AdmissionInquirySource Source { get; set; }
	public string? SourceDetail { get; set; }
	public DateTime? ExpiresAtUtc { get; set; }
	public bool IsActive { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class AdmissionPublicInquiryLinkCreatedDto : AdmissionPublicInquiryLinkDto
{
	public string Token { get; set; } = string.Empty;
}

public class SetAdmissionPublicInquiryLinkFormDto
{
	[Required]
	public Guid InquiryFormDefinitionId { get; set; }
	[Required]
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class PublicAdmissionInquiryContextDto
{
	public string? LogoUrl { get; set; }
	public string? BannerUrl { get; set; }
	public string AdmissionCycleName { get; set; } = string.Empty;
	public AdmissionInquirySource Source { get; set; }
	public PublicAdmissionInquiryFormDto? Form { get; set; }
	public List<PublicAdmissionInquiryOfferingDto> Offerings { get; set; } = [];
	public List<PublicAdmissionInquiryAcademicOptionDto> AcademicOptions { get; set; } = [];
}

public class PublicAdmissionInquiryAcademicOptionDto
{
	public Guid BoardId { get; set; }
	public string BoardName { get; set; } = string.Empty;
	public Guid MediumId { get; set; }
	public string MediumName { get; set; } = string.Empty;
	public Guid StandardId { get; set; }
	public string StandardName { get; set; } = string.Empty;
}

public class PublicAdmissionInquiryOfferingDto : PublicAdmissionInquiryAcademicOptionDto
{
	public Guid Id { get; set; }
	public Guid AdmissionCycleId { get; set; }
	public string AdmissionCycleName { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public AdmissionTrack Track { get; set; }
	public bool CanCreateInquiry { get; set; }
	public string? UnavailableReason { get; set; }
}

public class PublicAdmissionInquiryFormDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public int Version { get; set; }
	public List<PublicAdmissionInquirySectionDto> Sections { get; set; } = [];
}

public class PublicAdmissionInquirySectionDto
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public int DisplayOrder { get; set; }
	public string? VisibilityRuleJson { get; set; }
	public List<PublicAdmissionInquiryFieldDto> Fields { get; set; } = [];
}

public class PublicAdmissionInquiryFieldDto
{
	public string Code { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public AdmissionFormFieldType FieldType { get; set; }
	public bool Required { get; set; }
	public bool ReadOnly { get; set; }
	public int DisplayOrder { get; set; }
	public string? Placeholder { get; set; }
	public string? HelpText { get; set; }
	public string? DefaultValue { get; set; }
	public string? OptionsJson { get; set; }
	public string? ValidationJson { get; set; }
	public string? VisibilityRuleJson { get; set; }
	public string? RequiredRuleJson { get; set; }
	public bool IsIdentityFallback { get; set; }
}

public class SubmitPublicAdmissionInquiryDto
{
	private string? _guardianEmail;
	[Required]
	public Guid AdmissionOfferingId { get; set; }
	[Required]
	public Guid BoardId { get; set; }
	[Required]
	public Guid MediumId { get; set; }
	[Required]
	public Guid StandardId { get; set; }

	[StringLength(128)]
	public string ApplicantFirstName { get; set; } = string.Empty;
	[StringLength(128)]
	public string? ApplicantLastName { get; set; }
	[StringLength(128)]
	public string GuardianFirstName { get; set; } = string.Empty;
	[StringLength(128)]
	public string? GuardianLastName { get; set; }
	[StringLength(32)]
	public string GuardianPhone { get; set; } = string.Empty;
	[EmailAddress, StringLength(256)]
	public string? GuardianEmail
	{
		get => _guardianEmail;
		set => _guardianEmail = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}
	[StringLength(128)]
	public string? ClassInterest { get; set; }
	[Required, StringLength(16, MinimumLength = 2)]
	public string PreferredLanguageCode { get; set; } = "en";
	public bool ConsentToContact { get; set; }
	public List<AdmissionFormValueDto> FormValues { get; set; } = [];
}
