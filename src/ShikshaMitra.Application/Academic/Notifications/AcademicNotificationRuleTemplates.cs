using System;
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
		var ids = configuration.TemplateIds.ToArray();
		if (ids.Length == 0) return;
		var channels = configuration.Channels.ToArray();
		var kinds = NotificationTemplateSnapshotSelector.RecipientKinds(configuration, definition);
		var query = from header in await headers.GetQueryableAsync()
			join revision in await revisions.GetQueryableAsync() on header.Id equals revision.TemplateId
			where header.TenantId == tenant && revision.TenantId == tenant && !header.IsDeleted &&
				!revision.IsDeleted && !revision.IsRetired && (!enabled || revision.IsPublished) &&
				(header.AcademicMediumId == null || header.AcademicMediumId == scope.MediumId) &&
				revision.EventKey == definition.EventKey && revision.SchemaVersion == definition.SchemaVersion &&
				Enumerable.Contains(ids, header.Id) && Enumerable.Contains(kinds, revision.RecipientKind) &&
				Enumerable.Contains(channels, revision.Channel)
			select header.Id;
		if ((await queries.ToListAsync(query.Distinct(), cancellationToken)).Count != ids.Length)
			throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		if (enabled) await snapshots.PinAsync(configuration, definition, cancellationToken);
	}
}
