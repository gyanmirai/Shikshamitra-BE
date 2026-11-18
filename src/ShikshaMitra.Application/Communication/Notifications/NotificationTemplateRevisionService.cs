using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false), UnitOfWork(true), DisableAuditing, ExposeServices(typeof(NotificationTemplateRevisionService), typeof(INotificationTemplateReadService))]
public partial class NotificationTemplateRevisionService(ICurrentTenant tenant, ICurrentUser current,
	IRepository<CommunicationTemplate, Guid> headers, IRepository<CommunicationTemplateRevision, Guid> revisions,
	IAsyncQueryableExecuter executer, IGuidGenerator guids, IDataFilter filters,
	IEnumerable<INotificationTemplateAccessPolicy> policies, NotificationTemplateVariableValidator validator,
	NotificationDefaultTemplateFactory defaults, INotificationPersonalWriteLock writeLock) : INotificationTemplateReadService, ITransientDependency
{
	public virtual async Task<NotificationTemplateDto> CreateAsync(CreateNotificationTemplateDto input, CancellationToken cancellationToken = default)
	{
		var tenantId = RequireTenant();
		var definition = validator.GetDefinition(input.EventKey);
		CommunicationTemplate header;
		CommunicationTemplateRevision? previous = null;
		if (input.ExistingTemplateId is Guid existingId)
		{
			header = await LoadHeaderAsync(existingId, cancellationToken);
			previous = await LastRevisionAsync(header.Id, cancellationToken);
		}
		else header = new(guids.Create(), tenantId) { AcademicMediumId = input.AcademicMediumId };
		await AuthorizeAsync(previous?.EventKey ?? input.EventKey, header.AcademicMediumId, NotificationTemplateAction.Write, cancellationToken);
		if (input.ExistingTemplateId.HasValue)
		{
			CheckStamp(header.ConcurrencyStamp, input.HeaderConcurrencyStamp);
			if (header.AcademicMediumId != input.AcademicMediumId || previous != null &&
				(previous.EventKey != input.EventKey || previous.RecipientKind != input.RecipientKind || previous.Channel != input.Channel ||
				 !string.Equals(previous.LanguageCode, input.LanguageCode, StringComparison.OrdinalIgnoreCase)))
				throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		}
		if (string.IsNullOrWhiteSpace(input.TemplateName) || input.TemplateName.Length > 160 ||
			string.IsNullOrWhiteSpace(input.Body) || input.Body.Length > 65536 || input.Subject == null || input.Subject.Length > 256 ||
			!Enum.IsDefined(input.Channel) || input.Channel == CommunicationChannelType.Sms || string.IsNullOrWhiteSpace(input.LanguageCode) || input.LanguageCode.Length > 16)
			throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		var revision = new CommunicationTemplateRevision(guids.Create(), tenantId, header.Id, (previous?.Revision ?? 0) + 1,
			input.EventKey, input.RecipientKind, input.Channel, input.LanguageCode, input.Subject, NormalizeBody(input.Channel, input.Body), definition.SchemaVersion);
		Validate(revision);
		if (!input.ExistingTemplateId.HasValue) await headers.InsertAsync(header, autoSave: true, cancellationToken: cancellationToken);
		await revisions.InsertAsync(revision, autoSave: true, cancellationToken: cancellationToken);
		ApplyHeader(header, revision, input.TemplateName);
		await headers.UpdateAsync(header, autoSave: true, cancellationToken: cancellationToken);
		return Map(header, revision);
	}

	public virtual async Task<NotificationTemplateDto> UpdateDraftAsync(Guid templateId, UpdateNotificationTemplateDraftDto input, CancellationToken cancellationToken = default)
	{
		var (header, revision) = await LoadCommandAsync(templateId, input.RevisionId, NotificationTemplateAction.Write, cancellationToken);
		EnsureDraft(revision); CheckStamp(revision.ConcurrencyStamp, input.ConcurrencyStamp); EnsureCurrent(header, revision);
		var candidate = new CommunicationTemplateRevision(guids.Create(), RequireTenant(), header.Id, revision.Revision,
			revision.EventKey, revision.RecipientKind, revision.Channel, revision.LanguageCode, input.Subject,
			NormalizeBody(revision.Channel, input.Body), revision.SchemaVersion);
		Validate(candidate);
		revision.UpdateDraft(candidate.Subject, candidate.Body);
		await revisions.UpdateAsync(revision, autoSave: true, cancellationToken: cancellationToken);
		ApplyHeader(header, revision, header.TemplateName);
		await headers.UpdateAsync(header, autoSave: true, cancellationToken: cancellationToken);
		return Map(header, revision);
	}

	public virtual async Task<NotificationTemplateDto> PublishAsync(Guid templateId, NotificationTemplateRevisionCommandDto input, CancellationToken cancellationToken = default)
	{
		var (header, revision) = await LoadCommandAsync(templateId, input.RevisionId, NotificationTemplateAction.Write, cancellationToken);
		EnsureDraft(revision); CheckStamp(revision.ConcurrencyStamp, input.ConcurrencyStamp); EnsureCurrent(header, revision); Validate(revision);
		var old = await executer.ToListAsync((await revisions.GetQueryableAsync()).Where(x => x.TenantId == tenant.Id &&
			x.TemplateId == templateId && x.Id != revision.Id && x.IsPublished && !x.IsRetired && !x.IsDeleted), cancellationToken);
		foreach (var prior in old) { prior.Retire(); await revisions.UpdateAsync(prior, cancellationToken: cancellationToken); }
		revision.Publish(current.Id!.Value, DateTimeOffset.UtcNow);
		await revisions.UpdateAsync(revision, autoSave: true, cancellationToken: cancellationToken);
		header.ApprovalStatus = CommunicationTemplateApprovalStatus.Approved;
		await headers.UpdateAsync(header, autoSave: true, cancellationToken: cancellationToken);
		return Map(header, revision);
	}

	public virtual async Task<NotificationTemplateDto> RestoreDefaultAsync(Guid templateId, string headerConcurrencyStamp, CancellationToken cancellationToken = default)
	{
		var header = await LoadHeaderAsync(templateId, cancellationToken);
		var previous = await LastRevisionAsync(templateId, cancellationToken) ?? throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		await AuthorizeAsync(previous.EventKey, header.AcademicMediumId, NotificationTemplateAction.Write, cancellationToken);
		CheckStamp(header.ConcurrencyStamp, headerConcurrencyStamp);
		var content = defaults.Create(validator.GetDefinition(previous.EventKey), previous.RecipientKind, previous.Channel, previous.LanguageCode);
		return await CreateAsync(new(previous.EventKey, previous.RecipientKind, previous.Channel, previous.LanguageCode,
			header.TemplateName, content.Subject, content.Body, header.AcademicMediumId, header.Id, header.ConcurrencyStamp), cancellationToken);
	}

	public virtual async Task RetireAsync(Guid templateId, NotificationTemplateRevisionCommandDto input, CancellationToken cancellationToken = default)
	{
		var (_, revision) = await LoadCommandAsync(templateId, input.RevisionId, NotificationTemplateAction.Write, cancellationToken);
		CheckStamp(revision.ConcurrencyStamp, input.ConcurrencyStamp); revision.Retire();
		await revisions.UpdateAsync(revision, autoSave: true, cancellationToken: cancellationToken);
	}

	public virtual async Task DeleteDraftAsync(Guid templateId, NotificationTemplateRevisionCommandDto input, CancellationToken cancellationToken = default)
	{
		var (header, revision) = await LoadCommandAsync(templateId, input.RevisionId, NotificationTemplateAction.Delete, cancellationToken);
		EnsureDraft(revision); CheckStamp(revision.ConcurrencyStamp, input.ConcurrencyStamp);
		if (header.CurrentNotificationRevisionId == revision.Id)
		{
			var published = await executer.FirstOrDefaultAsync((await revisions.GetQueryableAsync()).Where(x => x.TenantId == tenant.Id &&
				x.TemplateId == templateId && x.IsPublished && !x.IsRetired && !x.IsDeleted).OrderByDescending(x => x.Revision), cancellationToken);
			header.SetNotificationRevision(published?.Id);
			await headers.UpdateAsync(header, autoSave: true, cancellationToken: cancellationToken);
		}
		await revisions.DeleteAsync(revision, autoSave: true, cancellationToken: cancellationToken);
	}
}
