using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

internal sealed record ProtectedWebPushSubscription(Guid TenantId, Guid UserId, Guid BrowserInstallationId,
	string Endpoint, WebPushSubscriptionKeysDto Keys, string VapidKeyVersion);

public sealed class WebPushSubscriptionProtector(IDataProtectionProvider protection) : ITransientDependency
{
	internal string Protect(NotificationSelfContext owner, RegisterWebPushSubscriptionDto input) =>
		Protector(owner.TenantId).Protect(JsonSerializer.Serialize(new ProtectedWebPushSubscription(owner.TenantId, owner.UserId,
			input.BrowserInstallationId, input.Endpoint, input.Keys, input.VapidKeyVersion)));

	internal ProtectedWebPushSubscription Unprotect(WebPushSubscription row)
	{
		try {
			if (row.TenantId is not Guid tenantId) throw new CryptographicException();
			var data = JsonSerializer.Deserialize<ProtectedWebPushSubscription>(Protector(tenantId).Unprotect(row.ProtectedSubscription));
			if (data == null || data.TenantId != tenantId || data.UserId != row.UserId || data.BrowserInstallationId != row.BrowserInstallationId ||
				data.VapidKeyVersion != row.VapidKeyVersion || HashEndpoint(data.Endpoint) != row.EndpointHash) throw new CryptographicException();
			return data;
		}
		catch (Exception error) when (error is CryptographicException or JsonException or ArgumentException)
		{
			throw new BusinessException(NotificationErrorCodes.PushSubscriptionInvalid);
		}
	}
	internal static string HashEndpoint(string endpoint) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)));
	private IDataProtector Protector(Guid tenantId) => protection.CreateProtector("Notifications.WebPush.Subscription.v1", tenantId.ToString("N"));
}
