using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class AcademicNotificationRuleTemplates(IRepository<CommunicationTemplate, Guid> headers,
	IRepository<CommunicationTemplateRevision, Guid> revisions, IAsyncQueryableExecuter queries,
	NotificationTemplateSnapshotSelector snapshots) : ITransientDependency
{
	internal async Task EnsureAsync(Guid tenant, NotificationRuleScope scope, NotificationEventDefinition definition,
		NotificationRuleRuntimeConfiguration configuration, bool enabled, CancellationToken cancellationToken)
	{
		if (configuration.TemplateIds.Count == 0) return;
		var query = await GetMatchingRevisionsAsync(tenant, scope, definition, configuration, enabled, currentOnly: false);
		var ids = await queries.ToListAsync(query.Select(x => x.TemplateId).Distinct(), cancellationToken);
		if (ids.Count != configuration.TemplateIds.Count) throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		if (enabled) await snapshots.PinAsync(configuration, definition, cancellationToken);
	}

	internal async Task<IReadOnlyList<CommunicationTemplateRevision>> LoadSampleRevisionsAsync(Guid tenant,
		NotificationRuleScope scope, NotificationEventDefinition definition, NotificationRuleRuntimeConfiguration configuration,
		CancellationToken cancellationToken)
	{
		if (configuration.TemplateIds.Count == 0) return [];
		var query = await GetMatchingRevisionsAsync(tenant, scope, definition, configuration, publishedOnly: false, currentOnly: true);
		return await queries.ToListAsync(query.OrderBy(x => x.RecipientKind).ThenBy(x => x.Channel)
			.ThenBy(x => x.LanguageCode).ThenBy(x => x.Id), cancellationToken);
	}

	private async Task<IQueryable<CommunicationTemplateRevision>> GetMatchingRevisionsAsync(Guid tenant, NotificationRuleScope scope,
		NotificationEventDefinition definition, NotificationRuleRuntimeConfiguration configuration, bool publishedOnly, bool currentOnly)
	{
		var ids = configuration.TemplateIds.ToArray();
		var channels = configuration.Channels.ToArray();
		var kinds = NotificationTemplateSnapshotSelector.RecipientKinds(configuration, definition);
		return from header in await headers.GetQueryableAsync()
			join revision in await revisions.GetQueryableAsync() on header.Id equals revision.TemplateId
			where header.TenantId == tenant && revision.TenantId == tenant && !header.IsDeleted &&
				!revision.IsDeleted && !revision.IsRetired && (!publishedOnly || revision.IsPublished) &&
				(!currentOnly || header.CurrentNotificationRevisionId == revision.Id) &&
				(header.AcademicMediumId == null || header.AcademicMediumId == scope.MediumId) &&
				revision.EventKey == definition.EventKey && revision.SchemaVersion == definition.SchemaVersion &&
				Enumerable.Contains(ids, header.Id) && Enumerable.Contains(kinds, revision.RecipientKind) &&
				Enumerable.Contains(channels, revision.Channel)
			select revision;
	}
}
