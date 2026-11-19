using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionParentCommand
{
	[Required, StringLength(128)] public string ConcurrencyStamp { get; set; } = "";
	[Required, StringLength(128)] public string IdempotencyKey { get; set; } = "";
}
public class AdmissionParentSendInput : AdmissionParentCommand
{
	[Required, RegularExpression("^(father|mother|guardian)$")] public string RecipientRelationship { get; set; } = "";
}
public class AdmissionParentEmailInput : AdmissionParentSendInput
{
	[Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
	[StringLength(256)] public string? ExpectedEmail { get; set; }
	[StringLength(512)] public string? Reason { get; set; }
}
public class AdmissionParentCorrectionInput : AdmissionParentSendInput
{
	[Required, StringLength(1024)] public string Notes { get; set; } = "";
	public List<string> FieldCodes { get; set; } = [];
	public List<AdmissionParentDocumentSelection> Documents { get; set; } = [];
}
public class AdmissionParentApplyInput : AdmissionParentCommand
{
	[Required, StringLength(1024)] public string Reason { get; set; } = "";
}
public class AdmissionParentPublicCommand
{
	[Required, StringLength(256)] public string SnapshotToken { get; set; } = "";
	[Required, StringLength(128)] public string IdempotencyKey { get; set; } = "";
}
public class AdmissionParentDraftInput : AdmissionParentPublicCommand
{
	public List<AdmissionFormValueDto> Values { get; set; } = [];
}
public class AdmissionParentUploadInput : AdmissionParentPublicCommand
{
	[Required] public IRemoteStreamContent File { get; set; } = null!;
	[StringLength(1024)] public string? Notes { get; set; }
}
public class AdmissionParentRecipientDto
{
	public string Relationship { get; set; } = "";
	public string? RelationshipLabel { get; set; }
	public string FullName { get; set; } = "";
	public string? Email { get; set; }
	public bool IsMainContact { get; set; }
	public bool CanSend { get; set; }
	public bool CanChangeEmail { get; set; }
	public string? EmailChangeBlockedReason { get; set; }
}
public class AdmissionParentDocumentSelection
{
	public Guid StageInstanceId { get; set; }
	public string RequirementCode { get; set; } = "";
	public int BaselineVersionNumber { get; set; }
}
public class AdmissionParentFieldDto
{
	public string Code { get; set; } = "";
	public string Label { get; set; } = "";
	public List<string> AllowedRecipientRelationships { get; set; } = [];
}
public class AdmissionParentRequestDto
{
	public Guid Id { get; set; }
	public string Purpose { get; set; } = "initial";
	public string Status { get; set; } = "";
	public AdmissionParentRecipientDto Recipient { get; set; } = new();
	public DateTime ExpiresAt { get; set; }
	public DateTime? LastSavedAt { get; set; }
	public DateTime? HandedInAt { get; set; }
	public string DeliveryStatus { get; set; } = "Pending";
	public string? DeliveryError { get; set; }
	public string? Notes { get; set; }
	public List<string> FieldCodes { get; set; } = [];
	public List<AdmissionParentDocumentSelection> Documents { get; set; } = [];
	public List<AdmissionFormValueDto> ProposedValues { get; set; } = [];
}
public class AdmissionParentStaffContextDto
{
	public Guid ApplicationId { get; set; }
	public string ConcurrencyStamp { get; set; } = "";
	public List<AdmissionParentRecipientDto> Recipients { get; set; } = [];
	public AdmissionParentRequestDto? Request { get; set; }
	public bool CanSend { get; set; }
	public bool CanRequestCorrections { get; set; }
	public bool CanRequestDocumentCorrections { get; set; }
	public bool CanRevoke { get; set; }
	public bool CanTakeover { get; set; }
	public bool CanApply { get; set; }
	public bool CanRetryDelivery { get; set; }
	public string? BlockedReason { get; set; }
	public string? EmailDeliveryBlockedReason { get; set; }
	public List<AdmissionParentFieldDto> AvailableFields { get; set; } = [];
	public List<AdmissionParentDocumentDto> AvailableDocuments { get; set; } = [];
}
public class AdmissionParentEmailHistoryDto
{
	public Guid RegistrationId { get; set; }
	public string Purpose { get; set; } = "initial";
	public string LinkStatus { get; set; } = "";
	public string RecipientName { get; set; } = "";
	public string RecipientRelationship { get; set; } = "";
	public string? RecipientRelationshipLabel { get; set; }
	public string? RecipientEmail { get; set; }
	public DateTime ExpiresAt { get; set; }
	public DateTime? LastSavedAt { get; set; }
	public DateTime? HandedInAt { get; set; }
	public string? Notes { get; set; }
	public List<AdmissionParentEmailAttemptDto> Attempts { get; set; } = [];
}
public class AdmissionParentEmailAttemptDto
{
	public string Status { get; set; } = "";
	public int AttemptNumber { get; set; }
	public DateTimeOffset? AttemptedAt { get; set; }
	public string? Destination { get; set; }
	public string? SafeMessage { get; set; }
}
public class AdmissionParentPublicContextDto
{
	public string SchoolName { get; set; } = "School admission";
	public string ApplicantName { get; set; } = "";
	public string ApplicationNumber { get; set; } = "";
	public string Purpose { get; set; } = "";
	public string Status { get; set; } = "";
	public DateTime ExpiresAt { get; set; }
	public string SnapshotToken { get; set; } = "";
	public bool CanEdit { get; set; }
	public bool CanHandIn { get; set; }
	public bool ProposalOnly { get; set; }
	public string? Notes { get; set; }
	public AdmissionParentDefinitionDto Definition { get; set; } = new();
	public List<AdmissionFormValueDto> Values { get; set; } = [];
	public List<string> EditableFieldCodes { get; set; } = [];
	public List<AdmissionParentDocumentDto> Documents { get; set; } = [];
}
public class AdmissionParentDefinitionDto
{
	public string Name { get; set; } = "";
	public string? Description { get; set; }
	public AdmissionFormLayoutType LayoutType { get; set; }
	public int FieldsPerRow { get; set; }
	public string DefaultLanguage { get; set; } = "en";
	public List<string> SupportedLanguages { get; set; } = [];
	public List<AdmissionParentSectionDto> Sections { get; set; } = [];
}
public class AdmissionParentSectionDto
{
	public Guid Id { get; set; }
	public string Code { get; set; } = "";
	public string Name { get; set; } = "";
	public int DisplayOrder { get; set; }
	public int? FieldsPerRow { get; set; }
	public string? VisibilityRuleJson { get; set; }
	public Guid? StepId { get; set; }
	public string? StepCode { get; set; }
	public string? StepName { get; set; }
	public int? StepDisplayOrder { get; set; }
	public string? StepVisibilityRuleJson { get; set; }
	public bool IsRepeatable { get; set; }
	public int MinEntries { get; set; }
	public int MaxEntries { get; set; }
	public string? EntryLabel { get; set; }
	public Dictionary<string, AdmissionFormTranslationDto> Translations { get; set; } = [];
	public List<AdmissionParentFieldDefinitionDto> Fields { get; set; } = [];
}
public class AdmissionParentFieldDefinitionDto
{
	public string? RuntimeBindingKey { get; set; }
	public string? OptionsSourceJson { get; set; }
	public Guid Id { get; set; }
	public Guid SectionId { get; set; }
	public string Code { get; set; } = "";
	public string Label { get; set; } = "";
	public AdmissionFormFieldType FieldType { get; set; }
	public string? Placeholder { get; set; }
	public string? HelpText { get; set; }
	public string? DefaultValue { get; set; }
	public string? OptionsJson { get; set; }
	public bool Required { get; set; }
	public bool ReadOnly { get; set; }
	public int DisplayOrder { get; set; }
	public string? ValidationJson { get; set; }
	public string? VisibilityRuleJson { get; set; }
	public string? RequiredRuleJson { get; set; }
	public Dictionary<string, AdmissionFormTranslationDto> Translations { get; set; } = [];
}
public class AdmissionParentDocumentDto
{
	public Guid StageInstanceId { get; set; }
	public string Code { get; set; } = "";
	public string Name { get; set; } = "";
	public string? Description { get; set; }
	public bool IsRequired { get; set; }
	public string Status { get; set; } = "missing";
	public string[] AllowedExtensions { get; set; } = [];
	public double MaxFileSizeMb { get; set; }
	public bool CanUpload { get; set; }
	public List<AdmissionParentFileDto> Evidence { get; set; } = [];
}
public class AdmissionParentFileDto
{
	public Guid Id { get; set; }
	public int VersionNumber { get; set; }
	public string? FileName { get; set; }
	public string? ContentType { get; set; }
	public long? Size { get; set; }
	public DateTime SubmittedAt { get; set; }
	public string Status { get; set; } = "";
	public bool CanDownload { get; set; }
}
public interface IAdmissionParentRegistrationAppService : IApplicationService
{
	Task<AdmissionParentStaffContextDto> GetAsync(Guid applicationId);
	Task<List<AdmissionParentEmailHistoryDto>> GetEmailHistoryAsync(Guid applicationId);
	Task<AdmissionParentStaffContextDto> SendAsync(Guid applicationId, AdmissionParentSendInput input);
	Task<AdmissionParentStaffContextDto> ChangeEmailAsync(Guid applicationId, AdmissionParentEmailInput input);
	Task<AdmissionParentStaffContextDto> RequestCorrectionsAsync(Guid applicationId, AdmissionParentCorrectionInput input);
	Task<AdmissionParentStaffContextDto> RevokeAsync(Guid applicationId, AdmissionParentCommand input);
	Task<AdmissionParentStaffContextDto> TakeoverAsync(Guid applicationId, AdmissionParentCommand input);
	Task<AdmissionParentStaffContextDto> ApplyAsync(Guid applicationId, AdmissionParentApplyInput input);
	Task<AdmissionParentStaffContextDto> RetryDeliveryAsync(Guid applicationId, AdmissionParentCommand input);
}
public interface IAdmissionParentRegistrationPublicAppService : IApplicationService
{
	Task<AdmissionParentPublicContextDto> GetAsync(string token);
	Task<AdmissionParentPublicContextDto> SaveAsync(string token, AdmissionParentDraftInput input);
	Task<AdmissionParentPublicContextDto> HandInAsync(string token, AdmissionParentPublicCommand input);
	Task<AdmissionParentPublicContextDto> UploadAsync(string token, Guid stageId, string code, AdmissionParentUploadInput input);
	Task<AdmissionDocumentDownloadDto> DownloadAsync(string token, Guid evidenceId);
}
