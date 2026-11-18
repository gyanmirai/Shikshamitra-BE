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
public sealed class NotificationTemplateSnapshotSelector(ICurrentTenant tenant,
	IRepository<CommunicationTemplateRevision, Guid> revisions, IAsyncQueryableExecuter executer,
	NotificationRuleConfigurationCodec codec) : ITransientDependency
{
	public Task<IReadOnlyList<Guid>> PinAsync(NotificationRuleRuntimeConfiguration configuration,
		NotificationEventDefinition definition, CancellationToken cancellationToken = default) => PinCoreAsync(configuration, definition, cancellationToken);
	private async Task<IReadOnlyList<Guid>> PinCoreAsync(NotificationRuleRuntimeConfiguration configuration,
		NotificationEventDefinition definition, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		_ = codec.Serialize(configuration, definition);
		var headers = configuration.TemplateIds.ToArray(); var channels = configuration.Channels.ToArray();
		var kinds = RecipientKinds(configuration, definition);
		var candidates = await executer.ToListAsync((await revisions.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			!x.IsDeleted && x.IsPublished && !x.IsRetired && x.EventKey == definition.EventKey && x.SchemaVersion == definition.SchemaVersion &&
			Enumerable.Contains(headers, x.TemplateId) && Enumerable.Contains(channels, x.Channel) && Enumerable.Contains(kinds, x.RecipientKind)), cancellationToken);
		if (candidates.GroupBy(x => (x.RecipientKind, x.Channel, x.LanguageCode)).Any(x => x.Count() != 1) ||
			kinds.Any(kind => channels.Any(channel => !candidates.Any(x => x.RecipientKind == kind && x.Channel == channel && x.LanguageCode == configuration.FallbackLanguage))))
			throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		return candidates.OrderBy(x => x.RecipientKind, StringComparer.Ordinal).ThenBy(x => x.Channel)
			.ThenBy(x => x.LanguageCode, StringComparer.Ordinal).Select(x => x.Id).ToArray();
	}
	internal static string[] RecipientKinds(NotificationRuleRuntimeConfiguration configuration, NotificationEventDefinition definition)
	{
		var kinds = new List<string>(configuration.StaffAudienceSelectors);
		if (definition.RecipientSelectors.Contains("PrimaryGuardian") || configuration.GuardianMode == NotificationGuardianMode.PrimaryAndSelected) kinds.Add("Guardian");
		if (configuration.IncludeStudentAccount) kinds.Add("StudentAccount");
		return kinds.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
	}
}
