using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

internal sealed record NotificationWhatsAppProvider(Guid Id, WhatsAppProviderConnection Connection);

internal sealed record WhatsAppProviderAccount(int SchemaVersion, string PhoneNumberId, string BusinessAccountId);

[RemoteService(false), DisableAuditing]
public sealed class WhatsAppProviderRoute(ICurrentTenant tenant, IRepository<ChannelProviderSetting, Guid> providers,
	IAsyncQueryableExecuter queries, IConfiguration configuration) : ITransientDependency
{
	public const string ProviderName = "Meta WhatsApp Cloud API";
	private const string SettingsPrefix = "Notifications:WhatsApp";

	internal async Task<NotificationWhatsAppProvider?> FindAsync(CancellationToken cancellationToken, Guid? providerSettingId = null)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		var current = await queries.ToListAsync((await providers.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			x.Channel == CommunicationChannelType.WhatsApp && !x.IsDeleted && x.IsEnabled && x.Status == CommunicationProviderStatus.Enabled &&
			(!providerSettingId.HasValue || x.Id == providerSettingId))
			.OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.CreationTime).ThenBy(x => x.Id)
			.Select(x => new { x.Id, x.ProviderName, x.ConfigurationJson, x.SecretReference }), cancellationToken);
		var selected = current.FirstOrDefault(x => string.Equals(x.ProviderName.Trim(), ProviderName, StringComparison.OrdinalIgnoreCase));
		if (selected == null || !ValidSecretReference(selected.SecretReference)) return null;
		var reference = selected.SecretReference!;
		var account = ReadAccount(selected.ConfigurationJson);
		if (account == null) return null;
		var version = configuration[$"{SettingsPrefix}:GraphApiVersion"];
		var accessToken = configuration[$"{SettingsPrefix}:Tenants:{tenantId:N}:AccessTokens:{reference}"];
		if (version == null || accessToken == null) return null;
		var connection = new WhatsAppProviderConnection(version, account.PhoneNumberId, account.BusinessAccountId, accessToken);
		return WhatsAppProviderClient.ValidConnection(connection) ? new(selected.Id, connection) : null;
	}

	internal static WhatsAppProviderAccount? ReadAccount(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return null;
		try
		{
			var current = NotificationRuntimeJson.Deserialize<WhatsAppProviderAccount>(json, NotificationErrorCodes.ProviderUnavailable);
			return current.SchemaVersion == 1 && WhatsAppProviderClient.ValidAccountId(current.PhoneNumberId) &&
				WhatsAppProviderClient.ValidAccountId(current.BusinessAccountId) ? current : null;
		}
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.ProviderUnavailable)
		{
			return null;
		}
	}

	private static bool ValidSecretReference(string? value) => value is { Length: > 0 and <= 128 } &&
		char.IsAsciiLetterOrDigit(value[0]) && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
