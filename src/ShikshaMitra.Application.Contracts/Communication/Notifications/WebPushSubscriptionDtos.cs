using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Communication.Notifications;

[DisableAuditing, JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WebPushSubscriptionKeysDto(string P256dh, string Auth);

[DisableAuditing, JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterWebPushSubscriptionDto(Guid BrowserInstallationId, string Endpoint,
	WebPushSubscriptionKeysDto Keys, string VapidKeyVersion);

public sealed record WebPushPublicKeyDto(bool IsConfigured, string? PublicKey, string? VapidKeyVersion, string? ReasonCode = null);
public sealed record WebPushSubscriptionDto(Guid Id, Guid BrowserInstallationId, bool IsEnabled,
	string VapidKeyVersion, DateTimeOffset LastActivityAt);

public interface IWebPushSubscriptionAppService : IApplicationService
{
	Task<WebPushPublicKeyDto> GetPublicKeyAsync(CancellationToken cancellationToken = default);
	Task<WebPushSubscriptionDto> RegisterAsync(RegisterWebPushSubscriptionDto input, CancellationToken cancellationToken = default);
	Task DisableAsync(Guid id, CancellationToken cancellationToken = default);
}
