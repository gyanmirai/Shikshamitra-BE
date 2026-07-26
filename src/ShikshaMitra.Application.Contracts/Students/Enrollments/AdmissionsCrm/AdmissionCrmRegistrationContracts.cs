using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationFieldDto
{
	[Required, MaxLength(80)]
	public required string Key { get; set; }
	[Required, MaxLength(120)]
	public required string Label { get; set; }
	public AdmissionCrmFormFieldType FieldType { get; set; }
	[MaxLength(120)]
	public string? SystemField { get; set; }
	[MaxLength(300)]
	public string? HelpText { get; set; }
	public bool Required { get; set; }
	public bool ParentEditable { get; set; } = true;
	public int DisplayOrder { get; set; }
	[MaxLength(80)]
	public string? VisibleWhenFieldKey { get; set; }
	[MaxLength(160)]
	public string? VisibleWhenValue { get; set; }
	[MaxLength(80)]
	public string? RequiredWhenFieldKey { get; set; }
	[MaxLength(160)]
	public string? RequiredWhenValue { get; set; }
	[MaxLength(500)]
	public string? ValidationPattern { get; set; }
	public int? MinimumLength { get; set; }
	public int? MaximumLength { get; set; }
	public decimal? MinimumValue { get; set; }
	public decimal? MaximumValue { get; set; }
	public IReadOnlyList<string> Options { get; set; } = [];
	public IReadOnlyDictionary<string, string> LocalizedLabels { get; set; } =
		new Dictionary<string, string>();
	public IReadOnlyDictionary<string, string> LocalizedHelpTexts { get; set; } =
		new Dictionary<string, string>();
	public IReadOnlyDictionary<string, IReadOnlyList<string>> LocalizedOptions { get; set; } =
		new Dictionary<string, IReadOnlyList<string>>();
}

public class AdmissionCrmRegistrationSectionDto
{
	[Required, MaxLength(80)]
	public required string Key { get; set; }
	[Required, MaxLength(120)]
	public required string Title { get; set; }
	[MaxLength(500)]
	public string? Description { get; set; }
	public int DisplayOrder { get; set; }
	public bool ParentVisible { get; set; } = true;
	public IReadOnlyList<AdmissionCrmRegistrationFieldDto> Fields { get; set; } = [];
	public IReadOnlyDictionary<string, string> LocalizedTitles { get; set; } =
		new Dictionary<string, string>();
	public IReadOnlyDictionary<string, string> LocalizedDescriptions { get; set; } =
		new Dictionary<string, string>();
}

public class AdmissionCrmRegistrationTemplateUpsertDto
{
	[Required, MaxLength(AdmissionCrmConsts.MaxTemplateNameLength)]
	public required string Name { get; set; }
	public bool IsFallback { get; set; }
	public AdmissionCrmPlaybookApplicabilityDto Applicability { get; set; } = new();
	[MinLength(1)]
	public IReadOnlyList<AdmissionCrmRegistrationSectionDto> Sections { get; set; } = [];
	[Range(1, 365)]
	public int LinkExpiryDays { get; set; } = 14;
	public bool AllowPartialSave { get; set; } = true;
	public bool FeeRequired { get; set; }
	[Range(0, 999999.99)]
	public decimal FeeAmount { get; set; }
	public AdmissionInquiryStage? AutoIssueAtStage { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? Notes { get; set; }
}

public class CreateAdmissionCrmRegistrationTemplateDto : AdmissionCrmRegistrationTemplateUpsertDto;
public class UpdateAdmissionCrmRegistrationTemplateDto : AdmissionCrmRegistrationTemplateUpsertDto;

public class AdmissionCrmRegistrationTemplateDto : AdmissionCrmRegistrationTemplateUpsertDto
{
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public int Version { get; set; }
	public AdmissionCrmRegistrationTemplateStatus Status { get; set; }
	public DateTime? PublishedAt { get; set; }
	public DateTime? RetiredAt { get; set; }
}

public class GetAdmissionCrmRegistrationTemplateListInput : PagedAndSortedResultRequestDto
{
	public string? Filter { get; set; }
	public AdmissionCrmRegistrationTemplateStatus? Status { get; set; }
}

public class AdmissionCrmFormNumberSeriesUpsertDto
{
	[Required, MaxLength(AdmissionCrmConsts.MaxTemplateNameLength)]
	public required string Name { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxFormNumberPatternLength)]
	public string Prefix { get; set; } = string.Empty;
	[MaxLength(AdmissionCrmConsts.MaxFormNumberPatternLength)]
	public string Suffix { get; set; } = string.Empty;
	[Range(1, 12)]
	public int Padding { get; set; } = 5;
	[Range(1, long.MaxValue)]
	public long NextNumber { get; set; } = 1;
	public AdmissionCrmPlaybookApplicabilityDto Applicability { get; set; } = new();
	public bool IsActive { get; set; } = true;
}

public class AdmissionCrmFormNumberSeriesDto : AdmissionCrmFormNumberSeriesUpsertDto
{
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public string NextPreview { get; set; } = string.Empty;
}

public class IssueAdmissionCrmRegistrationApplicationDto
{
	public Guid RegistrationTemplateId { get; set; }
	public Guid? FormNumberSeriesId { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxRegistrationFormNumberLength)]
	public string? ManualFormNumber { get; set; }
	public Guid? PlaybookId { get; set; }
	public int? LinkExpiryDays { get; set; }
	public bool WaiveFee { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? WaiverReason { get; set; }
}

public class CreateDirectAdmissionCrmRegistrationDto : IValidatableObject
{
	[Required]
	public required CreateAdmissionCrmLeadDto Applicant { get; set; }
	[Required]
	public required IssueAdmissionCrmRegistrationApplicationDto Registration { get; set; }

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (string.IsNullOrWhiteSpace(Applicant.FirstName))
		{
			yield return new ValidationResult(
				"Student name is required for a direct registration.",
				[$"{nameof(Applicant)}.{nameof(Applicant.FirstName)}"]);
		}
		if (!Applicant.TargetStandardId.HasValue)
		{
			yield return new ValidationResult(
				"Target class is required for a direct registration.",
				[$"{nameof(Applicant)}.{nameof(Applicant.TargetStandardId)}"]);
		}
	}
}

public class AdmissionCrmDirectRegistrationResultDto
{
	public required AdmissionCrmLeadDto Applicant { get; set; }
	public required AdmissionCrmRegistrationApplicationDto Registration { get; set; }
}

public class SaveAdmissionCrmRegistrationResponsesDto
{
	public Dictionary<string, JsonElement> Responses { get; set; } = new();
}

public class AdmissionCrmRegistrationApplicationDto
{
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid LeadId { get; set; }
	public Guid RegistrationTemplateId { get; set; }
	public int RegistrationTemplateVersion { get; set; }
	public Guid? PlaybookId { get; set; }
	public string FormNumber { get; set; } = string.Empty;
	public AdmissionCrmRegistrationStatus Status { get; set; }
	public IReadOnlyList<AdmissionCrmRegistrationSectionDto> Sections { get; set; } = [];
	public IReadOnlyDictionary<string, JsonElement> Prefill { get; set; } = new Dictionary<string, JsonElement>();
	public IReadOnlyDictionary<string, JsonElement> Responses { get; set; } = new Dictionary<string, JsonElement>();
	public IReadOnlyList<string> CorrectionFieldKeys { get; set; } = [];
	public decimal CompletionPercentage { get; set; }
	public DateTime IssuedAt { get; set; }
	public DateTime LinkExpiresAt { get; set; }
	public DateTime? OpenedAt { get; set; }
	public DateTime? LastSavedAt { get; set; }
	public DateTime? SubmittedAt { get; set; }
	public DateTime? VerifiedAt { get; set; }
	public decimal FeeAmount { get; set; }
	public AdmissionCrmRegistrationFeeStatus FeeStatus { get; set; }
	public string? PaymentReference { get; set; }
	public string? LifecycleReason { get; set; }
	public string? ApplicantStatusPath { get; set; }
}

public class SavePublicAdmissionCrmRegistrationDto
{
	[Required]
	public required string AccessSessionToken { get; set; }
	public Dictionary<string, JsonElement> Responses { get; set; } = new();
}

public class ReturnAdmissionCrmRegistrationForCorrectionDto
{
	[MinLength(1)]
	public IReadOnlyList<string> FieldKeys { get; set; } = [];
	[Required, MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public required string Reason { get; set; }
}

public class ExtendAdmissionCrmRegistrationLinkDto
{
	public DateTime NewExpiry { get; set; }
	[Required, MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public required string Reason { get; set; }
}

public class AdmissionCrmRegistrationReasonDto
{
	[Required, MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public required string Reason { get; set; }
}

public class AdmissionCrmRegistrationReissueDto
{
	[Range(1, 365)]
	public int LinkExpiryDays { get; set; } = 14;
	[Required, MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public required string Reason { get; set; }
}

public class RecordAdmissionCrmRegistrationPaymentReferenceDto
{
	[Required, MaxLength(AdmissionCrmConsts.MaxPaymentReferenceLength)]
	public required string PaymentReference { get; set; }
}
