using System;
using ShikshaMitra.Communication.AnnouncementTypes;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication;

public class CommunicationCampaign : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid? AcademicMediumId { get; set; }
	public Guid? AcademicYearId { get; set; }
	public CommunicationSourceModule SourceModule { get; set; }
	public string? SourceEntityType { get; set; }
	public Guid? SourceEntityId { get; set; }
	public string? SourceReference { get; set; }
	public string CampaignType { get; set; } = "Notification";
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
	public AnnouncementType? AnnouncementType { get; set; }
	public string AudienceDefinitionJson { get; set; } = "{}";
	public string ChannelsJson { get; set; } = "[]";
	public string FallbackOrderJson { get; set; } = "[]";
	public string AttachmentsJson { get; set; } = "[]";
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

	protected CommunicationCampaign()
	{
	}

	public CommunicationCampaign(Guid id, Guid? tenantId) : base(id)
	{
		TenantId = tenantId;
		Status = CommunicationCampaignStatus.Draft;
		ImportanceLevel = CommunicationImportanceLevel.Normal;
		SourceModule = CommunicationSourceModule.Manual;
	}
}
