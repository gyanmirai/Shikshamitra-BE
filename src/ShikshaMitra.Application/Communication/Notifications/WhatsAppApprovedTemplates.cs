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

internal sealed record WhatsAppBoundParameters(string PhoneNumberId, string BusinessAccountId, string Mapping);

[RemoteService(false)]
public sealed class WhatsAppApprovedTemplates(ICurrentTenant tenant, IRepository<WhatsAppTemplateBinding, Guid> bindings,
	IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<IReadOnlyDictionary<Guid, WhatsAppTemplateBinding>> LoadAsync(NotificationWhatsAppProvider provider,
		Guid[] revisions, CancellationToken cancellationToken)
	{
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		var candidates = await queries.ToListAsync((await bindings.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			x.ProviderSettingId == provider.Id && !x.IsDeleted && x.ProviderStatus == NotificationProviderTemplateState.Approved &&
			x.LastSyncedAt != null && Enumerable.Contains(revisions, x.TemplateRevisionId)), cancellationToken);
		return candidates.Where(x => WhatsAppProviderClient.ValidAccountId(x.ProviderTemplateId) && ReadParameters(x, provider.Connection) != null)
			.ToDictionary(x => x.TemplateRevisionId);
	}

	internal static string Encode(WhatsAppCompiledTemplate template, WhatsAppTemplateCompiler compiler, WhatsAppProviderConnection connection) =>
		NotificationRuntimeJson.Serialize(new WhatsAppBoundParameters(connection.PhoneNumberId, connection.BusinessAccountId,
			compiler.EncodeParameters(template)), NotificationErrorCodes.TemplateInvalid);

	internal static string? ReadParameters(WhatsAppTemplateBinding binding, WhatsAppProviderConnection connection)
	{
		try
		{
			var parameters = NotificationRuntimeJson.Deserialize<WhatsAppBoundParameters>(binding.ParameterMappingsJson, NotificationErrorCodes.TemplateInvalid);
			return parameters.PhoneNumberId == connection.PhoneNumberId && parameters.BusinessAccountId == connection.BusinessAccountId &&
				!string.IsNullOrWhiteSpace(parameters.Mapping) ? parameters.Mapping : null;
		}
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.TemplateInvalid)
		{
			return null;
		}
	}

	internal static bool MatchesLanguage(WhatsAppTemplateBinding binding, string language) =>
		string.Equals(binding.LanguageCode, language.Replace('-', '_'), StringComparison.OrdinalIgnoreCase);
}
