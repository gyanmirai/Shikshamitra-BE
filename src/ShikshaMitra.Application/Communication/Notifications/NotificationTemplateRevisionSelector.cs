using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false)]
public sealed class NotificationTemplateRevisionSelector(ICurrentTenant tenant,
	IRepository<CommunicationTemplateRevision, Guid> revisions, IAsyncQueryableExecuter executer) : ITransientDependency
{
	public Task<CommunicationTemplateRevision?> SelectAsync(IReadOnlyList<Guid> templateIds, string eventKey,
		string recipientKind, CommunicationChannelType channel, string requestedLanguage, string fallbackLanguage,
		CancellationToken cancellationToken = default) => SelectCoreAsync(templateIds, eventKey, recipientKind, channel,
			requestedLanguage, fallbackLanguage, cancellationToken);

	private async Task<CommunicationTemplateRevision?> SelectCoreAsync(IReadOnlyList<Guid> templateIds, string eventKey,
		string recipientKind, CommunicationChannelType channel, string requestedLanguage, string fallbackLanguage, CancellationToken cancellationToken)
	{
		if (tenant.Id is not Guid tenantId) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (templateIds.Count > 256 || string.IsNullOrWhiteSpace(requestedLanguage) || string.IsNullOrWhiteSpace(fallbackLanguage))
			throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		if (templateIds.Count == 0) return null;
		var ids = templateIds.Distinct().ToArray();
		var requested = requestedLanguage.ToLowerInvariant(); var fallback = fallbackLanguage.ToLowerInvariant();
		var candidates = await executer.ToListAsync((await revisions.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			Enumerable.Contains(ids, x.TemplateId) && x.EventKey == eventKey && x.RecipientKind == recipientKind && x.Channel == channel &&
			x.IsPublished && !x.IsRetired && !x.IsDeleted && (x.LanguageCode == requested || x.LanguageCode == fallback)), cancellationToken);
		var selected = candidates.Where(x => x.LanguageCode == requested).ToArray();
		if (selected.Length == 0) selected = candidates.Where(x => x.LanguageCode == fallback).ToArray();
		if (selected.Length > 1) throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		return selected.SingleOrDefault();
	}
}
