using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record AcademicNotificationManualTemplateSet(IReadOnlyList<Guid> Headers, IReadOnlyList<Guid> Revisions,
	IReadOnlyList<AcademicNotificationPreviewSampleDto> Samples);

[RemoteService(false), DisableAuditing]
public sealed class AcademicNotificationManualTemplates(IRepository<CommunicationTemplate, Guid> headers,
	IRepository<CommunicationTemplateRevision, Guid> revisions, IAsyncQueryableExecuter queries,
	NotificationTemplatePreviewService preview) : ITransientDependency
{
	private sealed record Variant(Guid TemplateId, Guid RevisionId, Guid? MediumId, string RecipientKind,
		CommunicationChannelType Channel, string Language);

	internal async Task<AcademicNotificationManualTemplateSet> SelectAsync(Guid tenantId, Guid? mediumId,
		NotificationEventDefinition definition, NotificationRuleRuntimeConfiguration configuration, string[] kinds,
		Guid? requestedHeader, CancellationToken cancellationToken)
	{
		var channels = configuration.Channels.ToArray();
		var query = from header in await headers.GetQueryableAsync()
			join version in await revisions.GetQueryableAsync() on header.Id equals version.TemplateId
			where header.TenantId == tenantId && version.TenantId == tenantId && !header.IsDeleted && !version.IsDeleted &&
				version.IsPublished && !version.IsRetired && (header.AcademicMediumId == null || header.AcademicMediumId == mediumId) &&
				version.EventKey == definition.EventKey && version.SchemaVersion == definition.SchemaVersion &&
				Enumerable.Contains(channels, version.Channel) && Enumerable.Contains(kinds, version.RecipientKind)
			select new Variant(header.Id, version.Id, header.AcademicMediumId, version.RecipientKind, version.Channel, version.LanguageCode);
		var available = await queries.ToListAsync(query.Take(1025), cancellationToken);
		if (available.Count > 1024 || requestedHeader.HasValue && !available.Any(x => x.TemplateId == requestedHeader))
			throw new BusinessException(NotificationErrorCodes.TemplateInvalid);

		var configured = configuration.TemplateIds.ToHashSet();
		var chosen = new List<Variant>();
		foreach (var group in available.GroupBy(x => (x.RecipientKind, x.Channel, x.Language)))
		{
			var ranked = group.GroupBy(x => x.TemplateId == requestedHeader ? 3 :
				configured.Contains(x.TemplateId) ? 2 : x.MediumId.HasValue ? 1 : 0);
			var preferred = ranked.MaxBy(x => x.Key)!;
			if (preferred.Count() > 1) throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
			chosen.Add(preferred.Single());
		}
		if (chosen.Count > 256) throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		var sampleIds = chosen.GroupBy(x => (x.RecipientKind, x.Channel))
			.Select(group => group.OrderByDescending(x => x.TemplateId == requestedHeader)
				.ThenByDescending(x => x.Language == configuration.FallbackLanguage)
				.ThenBy(x => x.Language, StringComparer.Ordinal).First().RevisionId).ToArray();
		var sampleRevisions = sampleIds.Length == 0 ? [] : await queries.ToListAsync((await revisions.GetQueryableAsync()).Where(x =>
			x.TenantId == tenantId && x.IsPublished && !x.IsDeleted && !x.IsRetired && Enumerable.Contains(sampleIds, x.Id)), cancellationToken);
		var samples = sampleRevisions.OrderBy(x => x.Channel).ThenBy(x => x.RecipientKind, StringComparer.Ordinal)
			.Select(x => new AcademicNotificationPreviewSampleDto(x.TemplateId, x.Id, x.RecipientKind, x.Channel, x.LanguageCode,
				true, preview.RenderSynthetic(x, definition))).ToArray();
		return new(chosen.Select(x => x.TemplateId).Distinct().Order().ToArray(), chosen.Select(x => x.RevisionId).Order().ToArray(), samples);
	}
}
