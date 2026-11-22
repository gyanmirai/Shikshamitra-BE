using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;

namespace ShikshaMitra.Communication.Notifications;

internal sealed record WebPushProviderResult(NotificationChannelSendResult Outcome, bool ExpiredSubscription = false);

[RemoteService(false), DisableAuditing]
public sealed class WebPushProviderClient(WebPushEndpointValidator endpoints, IWebPushEndpointClientFactory clients, IClock clock,
	IMemoryCache tokenCache) : ITransientDependency
{
	internal const int MaxTimeToLiveSeconds = 3600;
	private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);

	internal async Task<WebPushProviderResult> SendAsync(ProtectedWebPushSubscription subscription, WebPushApplicationKeys keys,
		Guid notificationId, int timeToLive, CancellationToken cancellationToken)
	{
		if (notificationId == Guid.Empty || timeToLive is < 0 or > MaxTimeToLiveSeconds ||
			keys.Version != subscription.VapidKeyVersion) return Rejected(NotificationErrorCodes.PushSubscriptionInvalid);

		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(SendTimeout);
		ValidatedWebPushEndpoint endpoint;
		try { endpoint = await endpoints.ValidateAsync(subscription.Endpoint, timeout.Token); }
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.PushEndpointInvalid)
		{
			return Rejected(NotificationErrorCodes.PushEndpointInvalid);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return Rejected(NotificationErrorCodes.ProviderUnavailable, transient: true);
		}

		try
		{
			using var http = clients.Create(endpoint);
			using var authentication = new VapidAuthentication(keys.PublicKey, keys.PrivateKey) {
				TokenCache = new VapidTokenCache(tokenCache, keys.PublicKey, keys.Version, keys.Subject)
			};
			if (keys.Subject != null) authentication.Subject = keys.Subject;
			var target = new PushSubscription { Endpoint = endpoint.Uri.AbsoluteUri };
			target.SetKey(PushEncryptionKeyName.P256DH, subscription.Keys.P256dh);
			target.SetKey(PushEncryptionKeyName.Auth, subscription.Keys.Auth);
			var message = new PushMessage(JsonSerializer.Serialize(new { notificationId })) {
				Topic = notificationId.ToString("N"), TimeToLive = timeToLive, Urgency = PushMessageUrgency.Normal
			};
			var push = new PushServiceClient(http) { AutoRetryAfter = false };
			await push.RequestPushMessageDeliveryAsync(target, message, authentication, timeout.Token);
			return new(new(NotificationProviderAcceptance.Accepted, null, null));
		}
		catch (PushServiceClientException error)
		{
			return error.StatusCode switch {
				HttpStatusCode.NotFound or HttpStatusCode.Gone => new(new(NotificationProviderAcceptance.Rejected, null, "WebPushSubscriptionExpired"), true),
				HttpStatusCode.TooManyRequests => Rejected("WebPushRateLimited", transient: true, RetryDelay(error.Headers?.RetryAfter)),
				>= HttpStatusCode.InternalServerError => Unknown(),
				_ => Rejected("WebPushProviderRejected")
			};
		}
		catch (Exception error) when (error is ArgumentException or FormatException or InvalidOperationException or CryptographicException)
		{
			return Rejected(NotificationErrorCodes.PushSubscriptionInvalid);
		}
		catch (Exception error) when (error is HttpRequestException ||
			error is OperationCanceledException && !cancellationToken.IsCancellationRequested)
		{
			return Unknown();
		}
	}

	private TimeSpan? RetryDelay(RetryConditionHeaderValue? retry)
	{
		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		var duration = retry?.Delta ?? retry?.Date - now;
		return duration > TimeSpan.Zero ? duration : null;
	}

	private static WebPushProviderResult Rejected(string code, bool transient = false, TimeSpan? retry = null) =>
		new(new(NotificationProviderAcceptance.Rejected, null, code, transient, retry));

	private static WebPushProviderResult Unknown() =>
		new(new(NotificationProviderAcceptance.UnknownAcceptance, null, NotificationErrorCodes.UnknownAcceptance));

	private sealed class VapidTokenCache(IMemoryCache cache, string publicKey, string version, string? subject) : IVapidTokenCache
	{
		public string Get(string audience) => cache.Get<string>((publicKey, version, subject, audience))!;

		public void Put(string audience, DateTimeOffset expiration, string token) =>
			cache.Set((publicKey, version, subject, audience), token, expiration.AddMinutes(-1));
	}
}
