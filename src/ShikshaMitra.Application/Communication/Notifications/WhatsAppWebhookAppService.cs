using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false), DisableAuditing, ExposeServices(typeof(IWhatsAppWebhookAppService))]
public sealed class WhatsAppWebhookAppService(WhatsAppWebhookAuthenticator authenticator, IConfiguration configuration,
	ICurrentTenant tenant, IUnitOfWorkManager units, INotificationPersonalWriteLock locks, IGuidGenerator guids, IClock clock,
	IDataProtectionProvider protection, IDataFilter filters, IAsyncQueryableExecuter queries, IRepository<ChannelProviderSetting, Guid> providers,
	IRepository<NotificationProviderEvent, Guid> events) : IWhatsAppWebhookAppService, ITransientDependency
{
	public string? VerifyChallenge(string? mode, string? token, string? challenge) => authenticator.VerifyChallenge(mode, token, challenge);

	public async Task<WhatsAppWebhookReceiptResult> ReceiveAsync(string? signature, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
	{
		if (payload.Length > IWhatsAppWebhookAppService.MaximumBodyBytes) return WhatsAppWebhookReceiptResult.InvalidPayload;
		if (!authenticator.ValidateSignature(signature, payload.Span)) return WhatsAppWebhookReceiptResult.InvalidSignature;

		IReadOnlyList<WhatsAppDeliveryReceipt> receipts;
		try { receipts = WhatsAppWebhookPayload.ReadReceipts(payload); }
		catch (JsonException) { return WhatsAppWebhookReceiptResult.InvalidPayload; }

		var receivedAt = new DateTimeOffset(clock.Now.ToUniversalTime());
		foreach (var account in receipts.GroupBy(x => (x.BusinessAccountId, x.PhoneNumberId)))
		{
			var accountSettings = configuration.GetSection(
				$"{WhatsAppWebhookAuthenticator.SettingsPrefix}:Accounts:{account.Key.BusinessAccountId}:{account.Key.PhoneNumberId}");
			if (!Guid.TryParse(accountSettings["TenantId"], out var tenantId) || tenantId == Guid.Empty ||
				!Guid.TryParse(accountSettings["ProviderSettingId"], out var providerId) || providerId == Guid.Empty)
				continue;
			await StoreReceiptsAsync(tenantId, providerId, account.Key.BusinessAccountId, account.Key.PhoneNumberId, account, receivedAt, cancellationToken);
		}
		return WhatsAppWebhookReceiptResult.Accepted;
	}

	private async Task StoreReceiptsAsync(Guid tenantId, Guid providerId, string businessAccountId, string phoneNumberId,
		IEnumerable<WhatsAppDeliveryReceipt> receipts, DateTimeOffset receivedAt, CancellationToken cancellationToken)
	{
		using var owner = tenant.Change(tenantId);
		using var unit = units.Begin(requiresNew: true, isTransactional: true);
		await locks.AcquireAsync([$"whatsapp-webhook:{providerId:N}"], cancellationToken);
		var candidate = await queries.FirstOrDefaultAsync((await providers.GetQueryableAsync()).Where(x => x.Id == providerId &&
			x.TenantId == tenantId && !x.IsDeleted && x.Channel == CommunicationChannelType.WhatsApp)
			.Select(x => new { x.ProviderName, x.ConfigurationJson }), cancellationToken);
		var configured = WhatsAppProviderRoute.ReadAccount(candidate?.ConfigurationJson);
		if (candidate == null || !string.Equals(candidate.ProviderName.Trim(), WhatsAppProviderRoute.ProviderName, StringComparison.OrdinalIgnoreCase) ||
			configured?.BusinessAccountId != businessAccountId || configured.PhoneNumberId != phoneNumberId)
			return;

		var incoming = receipts.Distinct().Select(receipt => {
			var payload = NotificationRuntimeJson.Serialize(receipt, NotificationErrorCodes.StateConflict);
			return (Receipt: receipt, Payload: payload, Key: "wa:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))));
		}).ToArray();
		var keys = incoming.Select(x => x.Key).ToArray();
		List<string> previous;
		using (filters.Disable<ISoftDelete>())
			previous = await queries.ToListAsync((await events.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
				x.ProviderSettingId == providerId && Enumerable.Contains(keys, x.ProviderEventKey)).Select(x => x.ProviderEventKey), cancellationToken);
		var stored = previous.ToHashSet(StringComparer.Ordinal);
		foreach (var row in incoming)
		{
			if (!stored.Add(row.Key)) continue;
			var protector = protection.CreateProtector("Notifications.WhatsApp.Webhook.v1", tenantId.ToString("N"), providerId.ToString("N"), row.Key);
			var evt = new NotificationProviderEvent(guids.Create(), tenantId, providerId, row.Key, protector.Protect(row.Payload), receivedAt, row.Receipt.MessageId);
			await events.InsertAsync(evt, cancellationToken: cancellationToken);
		}
		await unit.CompleteAsync(cancellationToken);
	}
}
