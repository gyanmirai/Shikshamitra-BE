using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Communication.Notifications;

public enum NotificationTemplateAction { Read, Write, Delete }

public sealed record CreateNotificationTemplateDto(string EventKey, string RecipientKind,
	CommunicationChannelType Channel, string LanguageCode, string TemplateName, string Subject, string Body,
	Guid? AcademicMediumId = null, Guid? ExistingTemplateId = null, string? HeaderConcurrencyStamp = null);
public sealed record UpdateNotificationTemplateDraftDto(Guid RevisionId, string Subject, string Body, string ConcurrencyStamp);
public sealed record NotificationTemplateRevisionCommandDto(Guid RevisionId, string ConcurrencyStamp);
public sealed record RestoreNotificationTemplateDto(string HeaderConcurrencyStamp);

public sealed class GetNotificationTemplateListInput : PagedResultRequestDto
{
	public string EventKey { get; set; } = string.Empty;
	public Guid? AcademicMediumId { get; set; }
	public string? Search { get; set; }
}

public sealed record NotificationTemplateDto(Guid TemplateId, Guid RevisionId, int Revision, string TemplateName,
	string EventKey, string RecipientKind, CommunicationChannelType Channel, string LanguageCode,
	string Subject, string Body, int SchemaVersion, string ContentHash, bool IsPublished, bool IsRetired,
	bool IsCurrent, string ConcurrencyStamp, string HeaderConcurrencyStamp, Guid? AcademicMediumId,
	DateTimeOffset? PublishedAt, Guid? PublishedByUserId, bool IsDeleted = false);

public sealed record NotificationTemplatePreviewDto(bool IsSynthetic, NotificationRenderResult Rendered);
