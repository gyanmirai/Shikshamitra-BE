using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDocumentRequirementDto
{
	[Required, MaxLength(80)]
	public required string Key { get; set; }
	public Guid? DocumentTypeId { get; set; }
	[Required, MaxLength(120)]
	public required string Name { get; set; }
	public AdmissionCrmDocumentRequirementLevel RequirementLevel { get; set; }
	[MaxLength(80)]
	public string? ConditionalFieldKey { get; set; }
	[MaxLength(160)]
	public string? ConditionalFieldValue { get; set; }
	public bool BlocksAdmissionReadiness { get; set; } = true;
	public bool RequiresOriginal { get; set; }
	public bool ValidityDateRequired { get; set; }
	[Range(0.1, 25)]
	public decimal MaximumSizeMb { get; set; } = 5;
	public IReadOnlyList<string> AcceptedExtensions { get; set; } = ["pdf", "jpg", "jpeg", "png"];
	[MaxLength(500)]
	public string? UploadInstructions { get; set; }
	public int DisplayOrder { get; set; }
}

public class AdmissionCrmDocumentChecklistTemplateUpsertDto
{
	[Required, MaxLength(AdmissionCrmConsts.MaxTemplateNameLength)]
	public required string Name { get; set; }
	public bool IsFallback { get; set; }
	public AdmissionCrmPlaybookApplicabilityDto Applicability { get; set; } = new();
	[MinLength(1)]
	public IReadOnlyList<AdmissionCrmDocumentRequirementDto> Requirements { get; set; } = [];
	[Range(1, 2)]
	public int ReadinessApprovalCount { get; set; } = 1;
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? Notes { get; set; }
}

public class AdmissionCrmDocumentChecklistTemplateDto : AdmissionCrmDocumentChecklistTemplateUpsertDto
{
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public int Version { get; set; }
	public AdmissionCrmDocumentChecklistTemplateStatus Status { get; set; }
	public DateTime? PublishedAt { get; set; }
	public DateTime? RetiredAt { get; set; }
}

public class GetAdmissionCrmDocumentChecklistTemplateListInput : PagedAndSortedResultRequestDto
{
	public string? Filter { get; set; }
	public AdmissionCrmDocumentChecklistTemplateStatus? Status { get; set; }
}

public class AssignAdmissionCrmDocumentChecklistDto
{
	public Guid ChecklistTemplateId { get; set; }
}

public class AdmissionCrmApplicantDocumentDto
{
	public Guid? Id { get; set; }
	public string RequirementKey { get; set; } = string.Empty;
	public string RequirementName { get; set; } = string.Empty;
	public AdmissionCrmDocumentRequirementLevel RequirementLevel { get; set; }
	public bool IsCurrentlyRequired { get; set; }
	public bool BlocksAdmissionReadiness { get; set; }
	public bool RequiresOriginal { get; set; }
	public bool ValidityDateRequired { get; set; }
	public bool OriginalSeen { get; set; }
	public AdmissionCrmApplicantDocumentStatus? Status { get; set; }
	public int Version { get; set; }
	public string? FileName { get; set; }
	public string? ContentType { get; set; }
	public long? FileSize { get; set; }
	public DateTime? ValidUntil { get; set; }
	public DateTime? UploadedAt { get; set; }
	public string? DecisionReason { get; set; }
	public DateTime? DecisionAt { get; set; }
	public IReadOnlyList<string> AcceptedExtensions { get; set; } = [];
	public decimal MaximumSizeMb { get; set; }
	public string? UploadInstructions { get; set; }
}

public class AdmissionCrmApplicantDocumentChecklistDto
{
	public Guid AssignmentId { get; set; }
	public Guid RegistrationApplicationId { get; set; }
	public Guid ChecklistTemplateId { get; set; }
	public int ChecklistTemplateVersion { get; set; }
	public string ChecklistName { get; set; } = string.Empty;
	public decimal CompletionPercentage { get; set; }
	public bool AdmissionReady { get; set; }
	public bool ReadinessConfirmed { get; set; }
	public bool ReadinessOverridden { get; set; }
	public int RequiredReadinessApprovalCount { get; set; }
	public int ReceivedReadinessApprovalCount { get; set; }
	public bool AwaitingSecondReadinessApproval { get; set; }
	public IReadOnlyList<string> BlockingReasons { get; set; } = [];
	public IReadOnlyList<AdmissionCrmApplicantDocumentDto> Documents { get; set; } = [];
}

public class ProcessedAdmissionCrmApplicantDocumentUploadDto
{
	[Required]
	public required string RequirementKey { get; set; }
	[Required]
	public required string FileName { get; set; }
	[Required]
	public required string ContentType { get; set; }
	public long Size { get; set; }
	[Required]
	public required byte[] Content { get; set; }
	public DateTime? ValidUntil { get; set; }
}

public class ReviewAdmissionCrmApplicantDocumentDto
{
	public bool Approve { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? Reason { get; set; }
}

public class AdmissionCrmDocumentReasonDto
{
	[Required, MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public required string Reason { get; set; }
}

public class ConfirmAdmissionCrmReadinessDto
{
	public bool OverrideBlockingRequirements { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? Reason { get; set; }
}

public class AdmissionCrmApplicantDocumentDownloadDto
{
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public byte[] Content { get; set; } = [];
}
