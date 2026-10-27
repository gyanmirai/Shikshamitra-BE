using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Communication;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryDto : FullAuditedEntityDto<Guid>
{
	public AdmissionSchoolContactDto? MainSchoolContact { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public string InquiryNumber { get; set; } = string.Empty;
	public Guid AdmissionCycleId { get; set; }
	public Guid? AdmissionOfferingId { get; set; }
	public Guid? FamilyId { get; set; }
	public Guid? ApplicantId { get; set; }
	public Guid? AssignedCounsellorId { get; set; }
	public string? AssignedCounsellorName { get; set; }
	public string AdmissionCycleName { get; set; } = string.Empty;
	public string? AdmissionOfferingCode { get; set; }
	public string ApplicantName { get; set; } = string.Empty;
	public string NormalizedPhone { get; set; } = string.Empty;
	public string? Email { get; set; }
	public string PreferredLanguageCode { get; set; } = "en";
	public AdmissionInquirySource Source { get; set; }
	public string? SourceDetail { get; set; }
	public Guid? FormResponseDefinitionId { get; set; }
	public List<AdmissionInquiryFormAnswerDto> FormAnswers { get; set; } = [];
	public List<AdmissionFormValueDto> FormValues { get; set; } = [];
	public AdmissionFormDto? FormDefinition { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public Guid? StandardId { get; set; }
	public string? StandardName { get; set; }
	public string? GuardianFirstName { get; set; }
	public string? GuardianLastName { get; set; }
	public string? ApplicantFirstName { get; set; }
	public string? ApplicantLastName { get; set; }
	public DateTime? ApplicantDateOfBirth { get; set; }
	public AdmissionInquiryStatus Status { get; set; }
	public AdmissionInquiryStatus LegacyStatus { get; set; }
	public bool IsTransferred { get; set; }
	public bool IsDuplicate { get; set; }
	public Guid? ApplicationId { get; set; }
	public Guid? DuplicateOfId { get; set; }
	public DateTime? ContactedAt { get; set; }
	public DateTime? LastActivityAt { get; set; }
	public bool Managed { get; set; }
	public bool NeedsFirstContact { get; set; }
	public bool NeedsQualification { get; set; }
	public DateTime? LastContactAt { get; set; }
	public DateTime? FirstContactDueAt { get; set; }
	public DateTime? QualificationDueAt { get; set; }
	public AdmissionInquiryQualificationOutcome? QualificationOutcome { get; set; }
	public DateTime? NextFollowUpAt { get; set; }
	public int OpenFollowUpCount { get; set; }
	public bool IsFirstContactOverdue { get; set; }
	public bool IsQualificationOverdue { get; set; }
	public bool HasOverdueFollowUp { get; set; }
	public bool IsOverdue => IsFirstContactOverdue || IsQualificationOverdue || HasOverdueFollowUp;
}

public class CreateAdmissionInquiryDto
{
	public AdmissionFamilyContactsDto? FamilyContacts { get; set; }
	public Guid? ExistingFamilyId { get; set; }
	public Guid? ExistingParentApplicantId { get; set; }
	[RegularExpression("^(father|mother)$")]
	public string? ParentRelationship { get; set; }
	public bool CreateSeparateFamily { get; set; }
	public string? DuplicateOverrideReason { get; set; }
	public Guid AdmissionCycleId { get; set; }
	public Guid? AdmissionOfferingId { get; set; }
	public Guid BoardId { get; set; }
	public Guid MediumId { get; set; }
	public Guid StandardId { get; set; }
	public string ApplicantName { get; set; } = string.Empty;
	public string Phone { get; set; } = string.Empty;
	public string? Email { get; set; }
	[Required, StringLength(16, MinimumLength = 2)]
	public string PreferredLanguageCode { get; set; } = "en";
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
	[System.ComponentModel.DataAnnotations.MaxLength(250)] public string? StandardName { get; set; }
	public string? StandardNameFilterType { get; set; }
	public bool DashboardFollowUpsDue { get; set; }
	public Guid? DivisionId { get; set; }
	public Guid? AcademicYearId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public Guid? StandardId { get; set; }
	public List<Guid> StandardIds { get; set; } = [];
	public Guid? AdmissionCycleId { get; set; }
	public Guid? AdmissionOfferingId { get; set; }
	[StringLength(64)]
	public string? InquiryNumber { get; set; }
	[StringLength(16)]
	public string? InquiryNumberFilterType { get; set; }
	[StringLength(256)]
	public string? ApplicantName { get; set; }
	[StringLength(16)]
	public string? ApplicantNameFilterType { get; set; }
	[StringLength(32)]
	public string? Phone { get; set; }
	[StringLength(16)]
	public string? PhoneFilterType { get; set; }
	[StringLength(256)]
	public string? Email { get; set; }
	[StringLength(16)]
	public string? EmailFilterType { get; set; }
	public AdmissionInquiryStatus? Status { get; set; }
	[StringLength(16)]
	public string? PipelineGroup { get; set; }
	public AdmissionInquirySource? Source { get; set; }
	public Guid? AssignedCounsellorId { get; set; }
	public bool? IsAssigned { get; set; }
	public bool ExcludePostApplication { get; set; }
	public bool? IsTransferred { get; set; }
	public AdmissionInquiryWorkQueue? WorkQueue { get; set; }
	public string? Search { get; set; }
}

public class UpdateAdmissionInquiryDto
{
	public Guid? AdmissionOfferingId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public Guid? StandardId { get; set; }
	public List<AdmissionFormValueDto>? FormValues { get; set; }
	public AdmissionInquiryStatus? Status { get; set; }
	[StringLength(1024)] public string? StatusReason { get; set; }
	[Required, StringLength(256, MinimumLength = 1)]
	public string ApplicantName { get; set; } = string.Empty;
	[Required, StringLength(32, MinimumLength = 7)]
	public string Phone { get; set; } = string.Empty;
	[EmailAddress, StringLength(256)]
	public string? Email { get; set; }
	public AdmissionInquirySource Source { get; set; }
	[StringLength(512)]
	public string? SourceDetail { get; set; }
	[Required, StringLength(16, MinimumLength = 2)]
	public string PreferredLanguageCode { get; set; } = "en";
	[Required, StringLength(40)]
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class AdmissionInquiryFormAnswerDto
{
	public string FieldCode { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public string Value { get; set; } = string.Empty;
}

public class ConfirmAdmissionInquiryOfferingDto
{
	[Required]
	public Guid OfferingId { get; set; }
	[Required, StringLength(40)]
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class AdmissionInquiryTimelineItemDto
{
	public string? ActorName { get; set; }
	public Guid Id { get; set; }
	public string ActivityType { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public DateTime OccurredAt { get; set; }
	public Guid? CreatorId { get; set; }
	public string? MetadataJson { get; set; }
}

public class BulkAssignAdmissionInquiriesDto
{
	[MinLength(1), MaxLength(100)]
	public List<Guid> InquiryIds { get; set; } = [];
	public AssignAdmissionCounsellorDto Assignment { get; set; } = new();
}

public class BulkAdmissionInquiryResultDto
{
	public int RequestedCount { get; set; }
	public int UpdatedCount { get; set; }
	public List<Guid> FailedInquiryIds { get; set; } = [];
}

public class AdmissionInquiryDuplicateCheckInput { public Guid AdmissionCycleId { get; set; } public string GuardianPhone { get; set; } = string.Empty; public string? GuardianEmail { get; set; } public string ApplicantName { get; set; } = string.Empty; public DateTime? ApplicantDateOfBirth { get; set; } [MaxLength(2)] public List<AdmissionSiblingParentContactDto> ParentContacts { get; set; } = []; }
public class AdmissionInquiryDuplicateMatchDto { public Guid ApplicantId { get; set; } public Guid FamilyId { get; set; } public Guid? ExistingInquiryId { get; set; } public string? ExistingInquiryNumber { get; set; } public string ApplicantName { get; set; } = string.Empty; public string? GuardianPhone { get; set; } public string? GuardianEmail { get; set; } public DateTime? DateOfBirth { get; set; } public bool CanLinkFamily { get; set; } public List<AdmissionSiblingParentCandidateDto> Parents { get; set; } = []; public bool MatchByEmail { get; set; } public bool MatchByMobile { get; set; } }
public class AssignAdmissionCounsellorDto { public AdmissionCounsellorAssignmentMode Mode { get; set; } public Guid? CounsellorId { get; set; } public List<Guid> CandidateCounsellorIds { get; set; } = []; [StringLength(1024)] public string? Reason { get; set; } }

public class CreateAdmissionInquiryCommunicationDto
{
	[Required]
	[StringLength(256, MinimumLength = 1)]
	public string Title { get; set; } = string.Empty;

	[StringLength(256)]
	public string? Subject { get; set; }

	[Required]
	[StringLength(4000, MinimumLength = 1)]
	public string Body { get; set; } = string.Empty;

	public List<CommunicationChannelType> Channels { get; set; } = [];
	public CommunicationImportanceLevel ImportanceLevel { get; set; } = CommunicationImportanceLevel.Normal;
	public bool RequiresAcknowledgement { get; set; }
	public bool PublishImmediately { get; set; }
}

public class AdmissionInquiryCommunicationResultDto
{
	public Guid CampaignId { get; set; }
	public CommunicationCampaignStatus Status { get; set; }
	public bool DeliveryStarted { get; set; }
	public int RecipientCount { get; set; }
	public int EligibleRecipientCount { get; set; }
	public int BlockedRecipientCount { get; set; }
	public string Message { get; set; } = string.Empty;
	public string? DeliveryErrorCode { get; set; }
}
