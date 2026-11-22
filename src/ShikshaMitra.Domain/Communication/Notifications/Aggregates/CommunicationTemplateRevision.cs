using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

public class CommunicationTemplateRevision : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid TemplateId { get; private set; }
	public int Revision { get; private set; }
	public string EventKey { get; private set; } = string.Empty;
	public string RecipientKind { get; private set; } = string.Empty;
	public CommunicationChannelType Channel { get; private set; }
	public string LanguageCode { get; private set; } = "en";
	public string Subject { get; private set; } = string.Empty;
	public string Body { get; private set; } = string.Empty;
	public int SchemaVersion { get; private set; }
	public string ContentHash { get; private set; } = string.Empty;
	public bool IsPublished { get; private set; }
	public bool IsRetired { get; private set; }
	public Guid? PublishedByUserId { get; private set; }
	public DateTimeOffset? PublishedAt { get; private set; }
	protected CommunicationTemplateRevision() { }
	public CommunicationTemplateRevision(Guid id, Guid tenantId, Guid templateId, int revision,
		string eventKey, string recipientKind, CommunicationChannelType channel, string languageCode,
		string subject, string body, int schemaVersion) : base(id)
	{
		TenantId = NotificationDataGuard.Identifier(tenantId, nameof(tenantId));
		TemplateId = NotificationDataGuard.Identifier(templateId, nameof(templateId));
		if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
		if (schemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
		Revision = revision;
		EventKey = NotificationDataGuard.Text(eventKey, nameof(eventKey), 160);
		RecipientKind = NotificationDataGuard.Text(recipientKind, nameof(recipientKind), 64);
		if (!Enum.IsDefined(channel) || channel == CommunicationChannelType.Sms)
			throw new ArgumentOutOfRangeException(nameof(channel));
		Channel = channel;
		LanguageCode = NotificationDataGuard.Text(languageCode, nameof(languageCode), 16).ToLowerInvariant();
		SchemaVersion = schemaVersion;
		UpdateDraft(subject, body);
	}

	public void UpdateDraft(string subject, string body)
	{
		if (IsPublished || IsRetired) throw new BusinessException(NotificationErrorCodes.TemplateImmutable);
		Subject = NotificationDataGuard.Text(subject, nameof(subject), 256, allowEmpty: true);
		Body = NotificationDataGuard.Text(body, nameof(body), 65536);
		ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
		{
			EventKey, RecipientKind, Channel, LanguageCode, Subject, Body, SchemaVersion
		}))));
	}

	public void Publish(Guid actorId, DateTimeOffset now)
	{
		if (IsPublished || IsRetired) throw new BusinessException(NotificationErrorCodes.TemplateImmutable);
		PublishedByUserId = NotificationDataGuard.Identifier(actorId, nameof(actorId));
		PublishedAt = now.ToUniversalTime();
		IsPublished = true;
	}

	public void Retire()
	{
		if (!IsPublished) throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		IsRetired = true;
	}
}
