using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Communication;

public class CommunicationAttachmentDto
{
	public string Name { get; set; } = string.Empty;
	public string Url { get; set; } = string.Empty;
	public string? ContentType { get; set; }
	public long? SizeInBytes { get; set; }
}

public class CommunicationAudienceFilterDto
{
	public string Field { get; set; } = string.Empty;
	public string Operator { get; set; } = "Equals";
	public string Value { get; set; } = string.Empty;
}

public class CommunicationExplicitRecipientDto
{
	public string RecipientType { get; set; } = "ParentGuardian";
	public Guid? RecipientId { get; set; }
	public Guid? UserId { get; set; }
	public string DisplayName { get; set; } = string.Empty;
	public string? Email { get; set; }
	public string? MobileNumber { get; set; }
	public string PreferredLanguageCode { get; set; } = "en";
	public bool IsParentDefault { get; set; } = true;
}

public class CommunicationAudienceDefinitionDto
{
	public List<Guid> GroupIds { get; set; } = new();
	public List<Guid> OrganizationIds { get; set; } = new();
	public List<Guid> ClassIds { get; set; } = new();
	public List<Guid> DivisionIds { get; set; } = new();
	public List<Guid> StudentIds { get; set; } = new();
	public List<Guid> ParentGuardianIds { get; set; } = new();
	public List<Guid> EmployeeIds { get; set; } = new();
	public List<Guid> UserIds { get; set; } = new();
	public List<string> Roles { get; set; } = new();
	public List<CommunicationAudienceFilterDto> Filters { get; set; } = new();
	public List<CommunicationExplicitRecipientDto> ExplicitRecipients { get; set; } = new();
	public bool ParentsFirst { get; set; } = true;
	public bool IncludeStudentsWhenEligible { get; set; }
}

public class CommunicationAudiencePreviewDto
{
	public int GroupRecipientCount { get; set; }
	public int ExplicitRecipientCount { get; set; }
	public int FilterMatchedCount { get; set; }
	public int TotalRecipientCount { get; set; }
	public int EligibleRecipientCount { get; set; }
	public int BlockedRecipientCount { get; set; }
	public bool ParentsFirst { get; set; }
	public bool CanPublish { get; set; } = true;
	public List<string> Issues { get; set; } = new();
	public List<string> BlockingIssues { get; set; } = new();
	public List<CommunicationRecipientSnapshotDto> SampleRecipients { get; set; } = new();
}

public class CreateUpdateCommunicationCampaignDto
{
	public Guid? AcademicMediumId { get; set; }
	public Guid? AcademicYearId { get; set; }
	public CommunicationSourceModule SourceModule { get; set; } = CommunicationSourceModule.Manual;
	[StringLength(80)]
	public string? SourceEntityType { get; set; }
	public Guid? SourceEntityId { get; set; }
	[StringLength(256)]
	public string? SourceReference { get; set; }
	public string CampaignType { get; set; } = "Notification";

	[Required]
	[StringLength(256, MinimumLength = 1)]
	public string Title { get; set; } = string.Empty;

	[StringLength(256)]
	public string? Subject { get; set; }

	public string Body { get; set; } = string.Empty;
	public CommunicationImportanceLevel ImportanceLevel { get; set; } = CommunicationImportanceLevel.Normal;
	public bool ApprovalRequired { get; set; }
	public Guid? TemplateId { get; set; }
	public Guid? AnnouncementTypeId { get; set; }
	public CommunicationAudienceDefinitionDto AudienceDefinition { get; set; } = new();
	public List<CommunicationChannelType> Channels { get; set; } = new();
	public List<CommunicationChannelType> FallbackOrder { get; set; } = new();
	public List<CommunicationAttachmentDto> Attachments { get; set; } = new();
	public string? LinkUrl { get; set; }
	public string LanguageCode { get; set; } = "en";
	public DateTimeOffset? ScheduledAt { get; set; }
	public bool RequiresAcknowledgement { get; set; }
}

public class CommunicationCampaignDto : EntityDto<Guid>
{
	public Guid? AcademicMediumId { get; set; }
	public Guid? AcademicYearId { get; set; }
	public CommunicationSourceModule SourceModule { get; set; }
	public string? SourceEntityType { get; set; }
	public Guid? SourceEntityId { get; set; }
	public string? SourceReference { get; set; }
	public string CampaignType { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string? Subject { get; set; }
	public string Body { get; set; } = string.Empty;
	public CommunicationImportanceLevel ImportanceLevel { get; set; }
	public CommunicationCampaignStatus Status { get; set; }
	public bool ApprovalRequired { get; set; }
	public Guid? ApprovedByUserId { get; set; }
	public DateTimeOffset? ApprovedAt { get; set; }
	public string? RejectionReason { get; set; }
	public Guid? TemplateId { get; set; }
	public Guid? AnnouncementTypeId { get; set; }
	public CommunicationAudienceDefinitionDto AudienceDefinition { get; set; } = new();
	public List<CommunicationChannelType> Channels { get; set; } = new();
	public List<CommunicationChannelType> FallbackOrder { get; set; } = new();
	public List<CommunicationAttachmentDto> Attachments { get; set; } = new();
	public string? LinkUrl { get; set; }
	public string LanguageCode { get; set; } = "en";
	public DateTimeOffset? ScheduledAt { get; set; }
	public DateTimeOffset? PublishedAt { get; set; }
	public bool RequiresAcknowledgement { get; set; }
	public int RecipientCount { get; set; }
	public int EligibleRecipientCount { get; set; }
	public int BlockedRecipientCount { get; set; }
	public int QueuedCount { get; set; }
	public int DeliveredCount { get; set; }
	public int FailedCount { get; set; }
	public DateTime CreationTime { get; set; }
}

public class GetCommunicationCampaignListRequestDto : PagedAndSortedResultRequestDto
{
	public string? Search { get; set; }
	public CommunicationCampaignStatus? Status { get; set; }
	public CommunicationSourceModule? SourceModule { get; set; }
	public string? SourceEntityType { get; set; }
	public Guid? SourceEntityId { get; set; }
	public string? CampaignType { get; set; }
	public Guid? AcademicMediumId { get; set; }
}

public class CalendarCommunicationRequestDto
{
	public string CampaignType { get; set; } = "Announcement";
	public Guid? TemplateId { get; set; }
	[StringLength(256)]
	public string? Subject { get; set; }
	public string? Body { get; set; }
	public CommunicationImportanceLevel ImportanceLevel { get; set; } = CommunicationImportanceLevel.Normal;
	public bool ApprovalRequired { get; set; }
	public CommunicationAudienceDefinitionDto AudienceDefinition { get; set; } = new();
	public bool IncludeSourceAudience { get; set; } = true;
	public List<CommunicationChannelType> Channels { get; set; } = new() { CommunicationChannelType.InApp };
	public List<CommunicationChannelType> FallbackOrder { get; set; } = new();
	public List<CommunicationAttachmentDto> Attachments { get; set; } = new();
	public string? LinkUrl { get; set; }
	public string LanguageCode { get; set; } = "en";
	public DateTimeOffset? ScheduledAt { get; set; }
	public bool RequiresAcknowledgement { get; set; }
	public bool PublishImmediately { get; set; } = true;
}

public class CalendarCommunicationResultDto
{
	public CommunicationCampaignDto Campaign { get; set; } = new();
	public bool Published { get; set; }
	public bool ApprovalRequired { get; set; }
	public string SourceEntityType { get; set; } = string.Empty;
	public Guid SourceEntityId { get; set; }
}

public class CommunicationApprovalDto
{
	public string? Reason { get; set; }
}

public class CommunicationTemplateDto : EntityDto<Guid>
{
	public Guid? AcademicMediumId { get; set; }
	public string TemplateName { get; set; } = string.Empty;
	public string? TargetRole { get; set; }
	public string Category { get; set; } = string.Empty;
	public string Subject { get; set; } = string.Empty;
	public string Body { get; set; } = string.Empty;
	public string Message
	{
		get => Body;
		set => Body = value;
	}
	public string LanguageCode { get; set; } = "en";
	public List<CommunicationChannelType> Channels { get; set; } = new();
	public List<string> Variables { get; set; } = new();
	public List<string> Hashtags { get; set; } = new();
	public CommunicationTemplateApprovalStatus ApprovalStatus { get; set; }
	public int Version { get; set; }
	public bool IsActive { get; set; }
}

public class CreateUpdateCommunicationTemplateDto
{
	public Guid? AcademicMediumId { get; set; }

	[Required]
	[StringLength(160)]
	public string TemplateName { get; set; } = string.Empty;

	[StringLength(80)]
	public string? TargetRole { get; set; }

	[StringLength(80)]
	public string Category { get; set; } = "General";

	[Required]
	[StringLength(256)]
	public string Subject { get; set; } = string.Empty;

	public string Body { get; set; } = string.Empty;
	public string Message
	{
		get => Body;
		set => Body = value;
	}
	public string LanguageCode { get; set; } = "en";
	public List<CommunicationChannelType> Channels { get; set; } = new();
	public List<string> Variables { get; set; } = new();
	public List<string> Hashtags { get; set; } = new();
	public CommunicationTemplateApprovalStatus ApprovalStatus { get; set; } = CommunicationTemplateApprovalStatus.Draft;
	public bool IsActive { get; set; } = true;
}

public class GetCommunicationTemplateListRequestDto : PagedAndSortedResultRequestDto
{
	public Guid? AcademicMediumId { get; set; }
	public string? Search { get; set; }
	public CommunicationChannelType? Channel { get; set; }
	public string? LanguageCode { get; set; }
	public string? Category { get; set; }
}

public class ChannelProviderSettingDto : EntityDto<Guid>
{
	public CommunicationChannelType Channel { get; set; }
	public string ProviderName { get; set; } = string.Empty;
	public string? SenderId { get; set; }
	public bool IsEnabled { get; set; }
	public bool IsDefault { get; set; }
	public string? ConfigurationJson { get; set; }
	public string? SecretReference { get; set; }
	public List<CommunicationChannelType> FallbackOrder { get; set; } = new();
	public int? DailyQuota { get; set; }
	public int? MonthlyQuota { get; set; }
	public bool ConsentRequired { get; set; }
	public bool TemplateApprovalRequired { get; set; }
	public CommunicationProviderStatus Status { get; set; }
}

public class CreateUpdateChannelProviderSettingDto
{
	public CommunicationChannelType Channel { get; set; }

	[Required]
	[StringLength(120)]
	public string ProviderName { get; set; } = string.Empty;

	[StringLength(120)]
	public string? SenderId { get; set; }
	public bool IsEnabled { get; set; }
	public bool IsDefault { get; set; }
	public string? ConfigurationJson { get; set; }
	public string? SecretReference { get; set; }
	public List<CommunicationChannelType> FallbackOrder { get; set; } = new();
	public int? DailyQuota { get; set; }
	public int? MonthlyQuota { get; set; }
	public bool ConsentRequired { get; set; } = true;
	public bool TemplateApprovalRequired { get; set; }
	public CommunicationProviderStatus Status { get; set; } = CommunicationProviderStatus.Disabled;
}

public class GetChannelProviderSettingListRequestDto : PagedAndSortedResultRequestDto
{
	public string? Search { get; set; }
	public CommunicationChannelType? Channel { get; set; }
	public CommunicationProviderStatus? Status { get; set; }
	public bool? IsEnabled { get; set; }
}

public class ConsentRecordDto : EntityDto<Guid>
{
	public string RecipientType { get; set; } = string.Empty;
	public Guid? RecipientId { get; set; }
	public string DisplayName { get; set; } = string.Empty;
	public CommunicationChannelType Channel { get; set; }
	public string Destination { get; set; } = string.Empty;
	public CommunicationConsentStatus Status { get; set; }
	public string Source { get; set; } = string.Empty;
	public DateTimeOffset? ConsentCapturedAt { get; set; }
	public DateTimeOffset? RevokedAt { get; set; }
	public string? Notes { get; set; }
}

public class CreateUpdateConsentRecordDto
{
	public string RecipientType { get; set; } = string.Empty;
	public Guid? RecipientId { get; set; }
	public string DisplayName { get; set; } = string.Empty;
	public CommunicationChannelType Channel { get; set; }
	public string Destination { get; set; } = string.Empty;
	public CommunicationConsentStatus Status { get; set; }
	public string Source { get; set; } = "Manual";
	public string? Notes { get; set; }
}

public class GetConsentRecordListRequestDto : PagedAndSortedResultRequestDto
{
	public string? Search { get; set; }
	public CommunicationChannelType? Channel { get; set; }
	public CommunicationConsentStatus? Status { get; set; }
	public string? RecipientType { get; set; }
}

public class CommunicationApprovalRuleDto : EntityDto<Guid>
{
	public string RuleName { get; set; } = string.Empty;
	public bool IsEnabled { get; set; } = true;
	public string? CampaignType { get; set; }
	public CommunicationSourceModule? SourceModule { get; set; }
	public CommunicationChannelType? Channel { get; set; }
	public int? MinimumAudienceSize { get; set; }
	public CommunicationImportanceLevel? ImportanceLevel { get; set; }
	public bool RequiresApproval { get; set; } = true;
	public DateTime CreationTime { get; set; }
}

public class CreateUpdateCommunicationApprovalRuleDto
{
	[Required]
	[StringLength(160)]
	public string RuleName { get; set; } = string.Empty;
	public bool IsEnabled { get; set; } = true;
	[StringLength(64)]
	public string? CampaignType { get; set; }
	public CommunicationSourceModule? SourceModule { get; set; }
	public CommunicationChannelType? Channel { get; set; }
	public int? MinimumAudienceSize { get; set; }
	public CommunicationImportanceLevel? ImportanceLevel { get; set; }
	public bool RequiresApproval { get; set; } = true;
}

public class GetCommunicationApprovalRuleListRequestDto : PagedAndSortedResultRequestDto
{
	public string? Search { get; set; }
	public bool? IsEnabled { get; set; }
	public string? CampaignType { get; set; }
	public CommunicationSourceModule? SourceModule { get; set; }
	public CommunicationChannelType? Channel { get; set; }
}

public class CommunicationRecipientSnapshotDto : EntityDto<Guid>
{
	public Guid CampaignId { get; set; }
	public string RecipientType { get; set; } = string.Empty;
	public Guid? RecipientId { get; set; }
	public Guid? UserId { get; set; }
	public string DisplayName { get; set; } = string.Empty;
	public string? Email { get; set; }
	public string? MobileNumber { get; set; }
	public string PreferredLanguageCode { get; set; } = "en";
	public bool IsParentDefault { get; set; }
	public bool IsEligible { get; set; }
	public string? BlockedReason { get; set; }
}

public class DeliveryAttemptDto : EntityDto<Guid>
{
	public Guid CampaignId { get; set; }
	public Guid? RecipientSnapshotId { get; set; }
	public CommunicationChannelType Channel { get; set; }
	public string Destination { get; set; } = string.Empty;
	public CommunicationDeliveryStatus Status { get; set; }
	public string? ProviderName { get; set; }
	public string? ProviderMessageId { get; set; }
	public int AttemptNumber { get; set; }
	public string? ErrorCode { get; set; }
	public string? ErrorMessage { get; set; }
	public DateTimeOffset? QueuedAt { get; set; }
	public DateTimeOffset? SentAt { get; set; }
	public DateTimeOffset? DeliveredAt { get; set; }
	public DateTimeOffset? ReadAt { get; set; }
	public decimal? EstimatedCost { get; set; }
	public string CurrencyCode { get; set; } = "INR";
}

public class GetDeliveryAttemptListRequestDto : PagedAndSortedResultRequestDto
{
	public Guid? CampaignId { get; set; }
	public CommunicationChannelType? Channel { get; set; }
	public CommunicationDeliveryStatus? Status { get; set; }
}

public class CommunicationReplyDto : EntityDto<Guid>
{
	public Guid? CampaignId { get; set; }
	public Guid? DeliveryAttemptId { get; set; }
	public Guid? RecipientSnapshotId { get; set; }
	public CommunicationChannelType Channel { get; set; }
	public string FromAddress { get; set; } = string.Empty;
	public string Body { get; set; } = string.Empty;
	public DateTimeOffset ReceivedAt { get; set; }
	public Guid? AssignedToUserId { get; set; }
	public string? AssignedDepartment { get; set; }
	public CommunicationReplyStatus Status { get; set; }
	public string? InternalNotes { get; set; }
}

public class CaptureCommunicationReplyDto
{
	public Guid? CampaignId { get; set; }
	public Guid? DeliveryAttemptId { get; set; }
	public Guid? RecipientSnapshotId { get; set; }
	public CommunicationChannelType Channel { get; set; }
	public string FromAddress { get; set; } = string.Empty;
	public string Body { get; set; } = string.Empty;
}

public class AssignCommunicationReplyDto
{
	public Guid? AssignedToUserId { get; set; }
	public string? AssignedDepartment { get; set; }
	public CommunicationReplyStatus Status { get; set; } = CommunicationReplyStatus.Assigned;
	public string? InternalNotes { get; set; }
}

public class GetCommunicationReplyListRequestDto : PagedAndSortedResultRequestDto
{
	public Guid? CampaignId { get; set; }
	public CommunicationReplyStatus? Status { get; set; }
	public CommunicationChannelType? Channel { get; set; }
}

public class UsageLedgerEntryDto : EntityDto<Guid>
{
	public Guid? CampaignId { get; set; }
	public CommunicationChannelType Channel { get; set; }
	public string ProviderName { get; set; } = string.Empty;
	public DateOnly UsageDate { get; set; }
	public int AttemptCount { get; set; }
	public int DeliveredCount { get; set; }
	public int FailedCount { get; set; }
	public decimal EstimatedCost { get; set; }
	public string CurrencyCode { get; set; } = "INR";
}

public class GetUsageLedgerListRequestDto : PagedAndSortedResultRequestDto
{
	public Guid? CampaignId { get; set; }
	public CommunicationChannelType? Channel { get; set; }
	public DateOnly? FromDate { get; set; }
	public DateOnly? ToDate { get; set; }
}

public class CommunicationDashboardDto
{
	public int TotalCampaigns { get; set; }
	public int DraftCampaigns { get; set; }
	public int PendingApprovalCampaigns { get; set; }
	public int ScheduledCampaigns { get; set; }
	public int QueuedCampaigns { get; set; }
	public int SentCampaigns { get; set; }
	public int FailedCampaigns { get; set; }
	public int OpenReplies { get; set; }
	public int EnabledProviders { get; set; }
	public int ConsentOptInCount { get; set; }
	public int ConsentOptOutCount { get; set; }
	public int DeliveryAttemptCount { get; set; }
	public int DeliveryFailureCount { get; set; }
	public decimal EstimatedCost { get; set; }
	public List<UsageLedgerEntryDto> RecentUsage { get; set; } = new();
}

public class CommunicationOptionDto
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
}
