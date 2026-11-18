using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public partial class WebPushNotificationChannel_Tests : IDisposable
{
	private const string Endpoint = "https://push.example.test/private-subscription";
	private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
	private readonly MemoryCache _cache = new(new MemoryCacheOptions());
	private readonly ECParameters _senderKeys = CreateKeys();
	private readonly IWebPushEndpointDnsResolver _dns = Substitute.For<IWebPushEndpointDnsResolver>();
	private readonly WebPushEndpointValidator _endpoints;
	private readonly ProviderHandler _handler = new();
	private readonly WebPushProviderClient _client;
	private readonly ProtectedWebPushSubscription _subscription;

	public WebPushNotificationChannel_Tests()
	{
		var config = new ConfigurationBuilder().AddInMemoryCollection(
			new System.Collections.Generic.Dictionary<string, string?> {
				["Notifications:WebPush:AllowedOrigins:0"] = "https://push.example.test"
			}).Build();
		_dns.ResolveAsync("push.example.test", Arg.Any<CancellationToken>()).Returns(new[] { IPAddress.Parse("8.8.8.8") });
		_endpoints = new(config, _dns);
		var factory = Substitute.For<IWebPushEndpointClientFactory>();
		factory.Create(Arg.Any<ValidatedWebPushEndpoint>()).Returns(call => {
			_handler.Validated = call.Arg<ValidatedWebPushEndpoint>();
			return new HttpClient(_handler, disposeHandler: false);
		});
		var clock = Substitute.For<IClock>();
		clock.Now.Returns(Now.UtcDateTime);
		_client = new(_endpoints, factory, clock, _cache);
		_subscription = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Endpoint,
			new(PublicKey(CreateKeys()), Encode(RandomNumberGenerator.GetBytes(16))), "local-v1");
	}

	[Fact]
	public async Task Sends_a_signed_aes128gcm_request_and_reuses_the_origin_token()
	{
		var id = Guid.NewGuid();
		(await SendAsync(id)).Outcome.Acceptance.ShouldBe(NotificationProviderAcceptance.Accepted);
		var first = _handler.Authorization.ShouldNotBeNull();
		(await SendAsync(id)).Outcome.Acceptance.ShouldBe(NotificationProviderAcceptance.Accepted);
		_handler.Authorization.ShouldBe(first);
		_handler.ContentEncoding.ShouldBe("aes128gcm");
		_handler.Payload!.Length.ShouldBeGreaterThan(86);
		_handler.Ttl.ShouldBe("120");
		_handler.Topic.ShouldBe(id.ToString("N"));
		_handler.Validated!.Addresses.ShouldBe([IPAddress.Parse("8.8.8.8")]);

		var header = AuthenticationHeaderValue.Parse(first);
		header.Scheme.ShouldBe("vapid");
		var jwt = header.Parameter!.Split(',').Single(x => x.TrimStart().StartsWith("t=", StringComparison.Ordinal)).Trim()[2..].Split('.');
		using var claims = JsonDocument.Parse(Decode(jwt[1]));
		claims.RootElement.GetProperty("aud").GetString().ShouldBe("https://push.example.test");
		claims.RootElement.GetProperty("sub").GetString().ShouldBe("mailto:support@example.test");
		using var signer = ECDsa.Create(new ECParameters { Curve = _senderKeys.Curve, Q = _senderKeys.Q });
		signer.VerifyData(Encoding.UTF8.GetBytes(jwt[0] + "." + jwt[1]), Decode(jwt[2]), HashAlgorithmName.SHA256).ShouldBeTrue();
	}

	[Theory]
	[InlineData(HttpStatusCode.Gone)]
	[InlineData(HttpStatusCode.NotFound)]
	public async Task Returns_an_expired_subscription_without_disclosing_response_details(HttpStatusCode status)
	{
		_handler.Status = status;
		var result = await SendAsync(Guid.NewGuid());

		result.ExpiredSubscription.ShouldBeTrue();
		result.Outcome.Acceptance.ShouldBe(NotificationProviderAcceptance.Rejected);
		result.Outcome.ErrorCode.ShouldBe("WebPushSubscriptionExpired");
	}

	[Fact]
	public async Task Rate_limit_defers_the_queue_without_retrying_inside_the_provider_call()
	{
		_handler.Status = HttpStatusCode.TooManyRequests;
		_handler.RetryAfter = new(Now.AddMinutes(3));
		var result = await SendAsync(Guid.NewGuid());

		result.Outcome.Acceptance.ShouldBe(NotificationProviderAcceptance.Rejected);
		result.Outcome.IsTransient.ShouldBeTrue();
		result.Outcome.RetryAfter.ShouldBe(TimeSpan.FromMinutes(3));
		_handler.Requests.ShouldBe(1);
	}

	[Fact]
	public async Task Revalidates_the_dns_at_delivery_and_blocks_a_changed_address()
	{
		await _endpoints.ValidateAsync(Endpoint, default);
		_dns.ResolveAsync("push.example.test", Arg.Any<CancellationToken>()).Returns(new[] { IPAddress.Loopback });
		var result = await SendAsync(Guid.NewGuid());

		result.Outcome.ErrorCode.ShouldBe(NotificationErrorCodes.PushEndpointInvalid);
		_handler.Requests.ShouldBe(0);
	}

	[Fact]
	public async Task A_redirect_is_rejected_and_the_production_handler_disables_redirection_and_proxies()
	{
		_handler.Status = HttpStatusCode.TemporaryRedirect;
		var result = await SendAsync(Guid.NewGuid());

		result.Outcome.Acceptance.ShouldBe(NotificationProviderAcceptance.Rejected);
		_handler.Requests.ShouldBe(1);
		using var handler = WebPushEndpointClientFactory.CreateHandler(_handler.Validated!);
		handler.AllowAutoRedirect.ShouldBeFalse();
		handler.UseProxy.ShouldBeFalse();
		using var http = new HttpClient(handler, disposeHandler: false);
		await Should.ThrowAsync<HttpRequestException>(() => http.GetAsync("https://different.example.test/"));
	}

	[Fact]
	public async Task A_transport_failure_preserves_unknown_acceptance()
	{
		_handler.FailConnection = true;
		var result = await SendAsync(Guid.NewGuid());

		result.Outcome.Acceptance.ShouldBe(NotificationProviderAcceptance.UnknownAcceptance);
		result.Outcome.IsTransient.ShouldBeFalse();
	}

	private Task<WebPushProviderResult> SendAsync(Guid id) =>
		_client.SendAsync(_subscription, new(PublicKey(_senderKeys), Encode(_senderKeys.D!), "local-v1", "mailto:support@example.test"), id, 120, default);

	private static ECParameters CreateKeys()
	{
		using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		return ecdsa.ExportParameters(true);
	}

	private static string PublicKey(ECParameters key) => Encode([4, .. key.Q.X!, .. key.Q.Y!]);
	private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
	private static byte[] Decode(string encoded) => Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/').PadRight((encoded.Length + 3) / 4 * 4, '='));

	public void Dispose()
	{
		_handler.Dispose();
		_cache.Dispose();
	}

	private sealed class ProviderHandler : HttpMessageHandler
	{
		public ValidatedWebPushEndpoint? Validated { get; set; }
		public HttpStatusCode Status { get; set; } = HttpStatusCode.Created;
		public RetryConditionHeaderValue? RetryAfter { get; set; }
		public bool FailConnection { get; set; }
		public Action? BeforeSend { get; set; }
		public string? Authorization { get; private set; }
		public string? ContentEncoding { get; private set; }
		public byte[]? Payload { get; private set; }
		public string? Ttl { get; private set; }
		public string? Topic { get; private set; }
		public int Requests { get; private set; }

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Requests++;
			BeforeSend?.Invoke();
			if (FailConnection) throw new HttpRequestException("synthetic connection failure");
			request.RequestUri!.AbsoluteUri.ShouldBe(Endpoint);
			Authorization = request.Headers.Authorization?.ToString();
			ContentEncoding = request.Content!.Headers.ContentEncoding.Single();
			Payload = await request.Content.ReadAsByteArrayAsync(cancellationToken);
			Ttl = request.Headers.GetValues("TTL").Single();
			Topic = request.Headers.GetValues("Topic").Single();
			var response = new HttpResponseMessage(Status) { Content = new StringContent("fixture provider body") };
			response.Headers.RetryAfter = RetryAfter;
			response.Headers.Location = new("https://different.example.test/");
			return response;
		}
	}
}
