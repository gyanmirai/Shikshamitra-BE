using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Communication.Notifications;

public partial class NotificationTemplateRevisionService
{
	public virtual async Task<NotificationTemplateDto> GetAsync(Guid templateId, CancellationToken cancellationToken = default)
	{
		var header = await LoadHeaderAsync(templateId, cancellationToken);
		var revision = header.CurrentNotificationRevisionId.HasValue
			? await LoadRevisionAsync(templateId, header.CurrentNotificationRevisionId.Value, cancellationToken)
			: await LastRevisionAsync(templateId, cancellationToken) ?? throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		await AuthorizeAsync(revision.EventKey, header.AcademicMediumId, NotificationTemplateAction.Read, cancellationToken);
		return Map(header, revision);
	}

	public virtual async Task<IReadOnlyList<NotificationTemplateDto>> GetHistoryAsync(Guid templateId, CancellationToken cancellationToken = default)
	{
		var header = await LoadHeaderAsync(templateId, cancellationToken);
		var binding = await LastRevisionAsync(templateId, cancellationToken) ?? throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		await AuthorizeAsync(binding.EventKey, header.AcademicMediumId, NotificationTemplateAction.Read, cancellationToken);
		var rows = await executer.ToListAsync((await revisions.GetQueryableAsync()).Where(x => x.TenantId == tenant.Id &&
			x.TemplateId == templateId && !x.IsDeleted).OrderByDescending(x => x.Revision), cancellationToken);
		return rows.Select(x => Map(header, x)).ToArray();
	}

	private Guid RequireTenant()
	{
		if (tenant.Id is not Guid tenantId || !current.IsAuthenticated || current.Id == null || current.TenantId != tenantId)
			throw new AbpAuthorizationException();
		return tenantId;
	}

	private async Task<CommunicationTemplate> LoadHeaderAsync(Guid id, CancellationToken cancellationToken)
	{
		var tenantId = RequireTenant();
		return await executer.FirstOrDefaultAsync((await headers.GetQueryableAsync()).Where(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted), cancellationToken)
			?? throw new EntityNotFoundException(typeof(CommunicationTemplate), id);
	}

	private async Task<CommunicationTemplateRevision> LoadRevisionAsync(Guid templateId, Guid id, CancellationToken cancellationToken) =>
		await executer.FirstOrDefaultAsync((await revisions.GetQueryableAsync()).Where(x => x.Id == id && x.TemplateId == templateId &&
			x.TenantId == tenant.Id && !x.IsDeleted), cancellationToken) ?? throw new EntityNotFoundException(typeof(CommunicationTemplateRevision), id);

	private async Task<CommunicationTemplateRevision?> LastRevisionAsync(Guid templateId, CancellationToken cancellationToken)
	{
		using var includeTombstones = filters.Disable<ISoftDelete>();
		return await executer.FirstOrDefaultAsync((await revisions.GetQueryableAsync()).Where(x => x.TemplateId == templateId && x.TenantId == tenant.Id)
			.OrderByDescending(x => x.Revision), cancellationToken);
	}

	private async Task<(CommunicationTemplate Header, CommunicationTemplateRevision Revision)> LoadCommandAsync(Guid templateId,
		Guid revisionId, NotificationTemplateAction action, CancellationToken cancellationToken)
	{
		var header = await LoadHeaderAsync(templateId, cancellationToken);
		var revision = await LoadRevisionAsync(templateId, revisionId, cancellationToken);
		await AuthorizeAsync(revision.EventKey, header.AcademicMediumId, action, cancellationToken);
		return (header, revision);
	}

	private async Task AuthorizeAsync(string eventKey, Guid? mediumId, NotificationTemplateAction action, CancellationToken cancellationToken)
	{
		var matches = policies.Where(x => x.SupportsEvent(eventKey)).ToArray();
		if (matches.Length != 1) throw new AbpAuthorizationException();
		await matches[0].EnsureAsync(eventKey, mediumId, action, cancellationToken);
	}

	private static void CheckStamp(string actual, string? expected)
	{
		if (string.IsNullOrWhiteSpace(expected) || !string.Equals(actual, expected, StringComparison.Ordinal)) throw new AbpDbConcurrencyException();
	}
	private static void EnsureCurrent(CommunicationTemplate header, CommunicationTemplateRevision revision)
	{
		if (header.CurrentNotificationRevisionId != revision.Id) throw new AbpDbConcurrencyException();
	}
	private static void EnsureDraft(CommunicationTemplateRevision revision)
	{
		if (revision.IsPublished || revision.IsRetired) throw new BusinessException(NotificationErrorCodes.TemplateImmutable);
	}
	private void Validate(CommunicationTemplateRevision revision)
	{
		var error = validator.ValidatePublication(revision);
		if (error != null) throw new BusinessException(error);
	}
	private string NormalizeBody(CommunicationChannelType channel, string body) => channel == CommunicationChannelType.Email
		? NotificationTemplateMarkup.SanitizeEmail(body, link => link == "{{source_link}}" || validator.IsAllowedLink(link))
			?? throw new BusinessException(NotificationErrorCodes.TemplateInvalid) : body;

	private static void ApplyHeader(CommunicationTemplate header, CommunicationTemplateRevision revision, string name)
	{
		header.TemplateName = name.Trim(); header.Subject = revision.Subject; header.Body = revision.Body;
		header.LanguageCode = revision.LanguageCode; header.TargetRole = revision.RecipientKind;
		header.Category = revision.EventKey.Split('.')[0];
		header.ChannelsJson = System.Text.Json.JsonSerializer.Serialize(new[] { revision.Channel });
		header.Version = revision.Revision; header.ApprovalStatus = CommunicationTemplateApprovalStatus.Draft;
		header.SetNotificationRevision(revision.Id);
	}
	private static NotificationTemplateDto Map(CommunicationTemplate header, CommunicationTemplateRevision revision) => new(header.Id,
		revision.Id, revision.Revision, header.TemplateName, revision.EventKey, revision.RecipientKind, revision.Channel, revision.LanguageCode,
		revision.Subject, revision.Body, revision.SchemaVersion, revision.ContentHash, revision.IsPublished, revision.IsRetired,
		header.CurrentNotificationRevisionId == revision.Id, revision.ConcurrencyStamp, header.ConcurrencyStamp,
		header.AcademicMediumId, revision.PublishedAt, revision.PublishedByUserId, revision.IsDeleted);
}
