using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.AuditLogging;
using Volo.Abp.Auditing;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication;

[CollectionDefinition(Name)]
public class NotificationSelfPushApiCollection : ICollectionFixture<NotificationSelfPushApiFactory>
{
	public const string Name = "Notification personal push";
}

public class NotificationSelfPushApiFactory : ShikshaMitraApiTestFactory
{
	internal static readonly ECParameters Vapid = CreateParameters();
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?> {
			["Notifications:WebPush:PublicKey"] = PublicKey(Vapid),
			["Notifications:WebPush:PrivateKey"] = Base64(Vapid.D!),
			["Notifications:WebPush:KeyVersion"] = "test-v1",
			["Notifications:WebPush:AllowedOrigins:0"] = "https://push.example.test",
			["Notifications:WebPush:AllowedOrigins:1"] = "https://private.push.example.test"
		}));
		builder.ConfigureTestServices(services => {
			services.AddSingleton<IWebPushEndpointDnsResolver, NotificationTestPushDns>();
			services.PostConfigure<AbpAuditingOptions>(options => options.IsEnabled = true);
			services.PostConfigure<AbpUnitOfWorkDefaultOptions>(options => options.TransactionBehavior = UnitOfWorkTransactionBehavior.Disabled);
		});
	}
	internal static ECParameters CreateParameters() { using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); return key.ExportParameters(true); }
	internal static string PublicKey(ECParameters key) => Base64([4, .. key.Q.X!, .. key.Q.Y!]);
	internal static string Base64(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public class NotificationTestPushDns : IWebPushEndpointDnsResolver
{
	public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) => Task.FromResult(new[] {
		IPAddress.Parse(host == "private.push.example.test" ? "169.254.169.254" : "8.8.8.8")
	});
}

[Collection(NotificationSelfPushApiCollection.Name)]
public class NotificationSelfApi_Tests_Push(NotificationSelfPushApiFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Route = "/api/communication/notifications/me/push";
	private static readonly ECParameters BrowserKey = NotificationSelfPushApiFactory.CreateParameters();
	private static readonly string BrowserPublic = NotificationSelfPushApiFactory.PublicKey(BrowserKey);
	private static readonly string BrowserAuth = NotificationSelfPushApiFactory.Base64(RandomNumberGenerator.GetBytes(16));

	[Fact]
	public async Task Rebinding_requires_the_existing_browser_keys_and_does_not_accept_a_guessed_endpoint()
	{
		var firstUserId = await CreateUserAsync(); using var firstUser = CreateClientAs(firstUserId);
		using var other = CreateClientAs(await CreateUserAsync()); var installation = Guid.NewGuid(); var endpoint = Endpoint();
		using var first = await firstUser.PostAsJsonAsync($"{Route}/subscriptions", Input(installation, endpoint));
		first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
		using var forged = await other.PostAsJsonAsync($"{Route}/subscriptions", Input(installation, endpoint,
			auth: NotificationSelfPushApiFactory.Base64(RandomNumberGenerator.GetBytes(16))));
		forged.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await forged.Content.ReadAsStringAsync());
		await WithDbAsync(async db => (await db.Set<WebPushSubscription>().SingleAsync(x => x.BrowserInstallationId == installation)).UserId.ShouldBe(firstUserId));
	}

	[Fact]
	public async Task Rotating_a_subscriptions_endpoint_disables_its_previous_registration_without_disabling_the_replacement()
	{
		var user = await CreateUserAsync(); using var client = CreateClientAs(user); var installation = Guid.NewGuid();
		using var first = await client.PostAsJsonAsync($"{Route}/subscriptions", Input(installation, Endpoint()));
		first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
		var oldId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
		using var second = await client.PostAsJsonAsync($"{Route}/subscriptions", Input(installation, Endpoint()));
		second.StatusCode.ShouldBe(HttpStatusCode.OK, await second.Content.ReadAsStringAsync());
		var currentId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
		currentId.ShouldNotBe(oldId);
		using var oldDelete = await client.DeleteAsync($"{Route}/subscriptions/{oldId}"); oldDelete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		await WithDbAsync(async db => {
			(await db.Set<WebPushSubscription>().SingleAsync(x => x.Id == oldId)).IsEnabled.ShouldBeFalse();
			(await db.Set<WebPushSubscription>().SingleAsync(x => x.Id == currentId)).IsEnabled.ShouldBeTrue();
		});
	}

	[Theory]
	[InlineData("p256dh")]
	[InlineData("auth")]
	public async Task Malformed_encryption_keys_are_rejected_without_a_subscription_row(string field)
	{
		var user = await CreateUserAsync(); using var client = CreateClientAs(user);
		using var response = await client.PostAsJsonAsync($"{Route}/subscriptions", Input(Guid.NewGuid(), Endpoint(),
			publicKey: field == "p256dh" ? "bad-key" : null, auth: field == "auth" ? "bad-auth" : null));
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		await WithDbAsync(async db => (await db.Set<WebPushSubscription>().CountAsync(x => x.UserId == user)).ShouldBe(0));
	}

	[Fact]
	public async Task Public_key_is_authenticated_no_store_and_never_contains_private_vapid_material()
	{
		using var client = CreateClientAs(await CreateUserAsync());
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "none");
		using var response = await client.GetAsync($"{Route}/public-key");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		(response.Headers.CacheControl?.NoStore).ShouldBe(true);
		var key = await response.Content.ReadFromJsonAsync<JsonElement>();
		key.GetProperty("publicKey").GetString().ShouldBe(NotificationSelfPushApiFactory.PublicKey(NotificationSelfPushApiFactory.Vapid));
		key.GetProperty("vapidKeyVersion").GetString().ShouldBe("test-v1");
		(await response.Content.ReadAsStringAsync()).ShouldNotContain(NotificationSelfPushApiFactory.Base64(NotificationSelfPushApiFactory.Vapid.D!));
		using var anonymous = CreateAnonymousClient();
		using var denied = await anonymous.GetAsync($"{Route}/public-key");
		denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Duplicate_registration_is_idempotent_owned_encrypted_and_secret_free_in_responses()
	{
		var user = await CreateUserAsync(); using var client = CreateClientAs(user);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "none");
		var installation = Guid.NewGuid(); var endpoint = Endpoint();
		using var first = await client.PostAsJsonAsync($"{Route}/subscriptions", Input(installation, endpoint));
		first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
		var saved = await first.Content.ReadFromJsonAsync<JsonElement>();
		using var second = await client.PostAsJsonAsync($"{Route}/subscriptions", Input(installation, endpoint));
		second.StatusCode.ShouldBe(HttpStatusCode.OK, await second.Content.ReadAsStringAsync());
		(await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid().ShouldBe(saved.GetProperty("id").GetGuid());
		var body = await second.Content.ReadAsStringAsync();
		body.ShouldNotContain(endpoint); body.ShouldNotContain(BrowserPublic); body.ShouldNotContain(BrowserAuth);
		await WithDbAsync(async db => {
			var rows = await db.Set<WebPushSubscription>().Where(x => x.UserId == user).ToListAsync();
			rows.Count.ShouldBe(1); rows[0].IsEnabled.ShouldBeTrue(); rows[0].BrowserInstallationId.ShouldBe(installation);
			rows[0].ProtectedSubscription.ShouldNotContain(endpoint);
			rows[0].ProtectedSubscription.ShouldNotContain(BrowserPublic); rows[0].ProtectedSubscription.ShouldNotContain(BrowserAuth);
		});
	}

	[Fact]
	public async Task Another_user_cannot_delete_a_subscription_but_its_owner_can_disable_it_idempotently()
	{
		var owner = await CreateUserAsync(); using var client = CreateClientAs(owner);
		using var response = await client.PostAsJsonAsync($"{Route}/subscriptions", Input(Guid.NewGuid(), Endpoint()));
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
		using var other = CreateClientAs(await CreateUserAsync());
		using var denied = await other.DeleteAsync($"{Route}/subscriptions/{id}");
		denied.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		using var deleted = await client.DeleteAsync($"{Route}/subscriptions/{id}"); deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		using var repeated = await client.DeleteAsync($"{Route}/subscriptions/{id}"); repeated.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		await WithDbAsync(async db => (await db.Set<WebPushSubscription>().SingleAsync(x => x.Id == id)).IsEnabled.ShouldBeFalse());
	}

	[Fact]
	public async Task A_shared_browser_can_rebind_the_same_subscription_to_the_current_account_and_old_owner_loses_control()
	{
		var installation = Guid.NewGuid(); var endpoint = Endpoint();
		using var firstUser = CreateClientAs(await CreateUserAsync()); var nextUserId = await CreateUserAsync();
		using var secondUser = CreateClientAs(nextUserId);
		using var first = await firstUser.PostAsJsonAsync($"{Route}/subscriptions", Input(installation, endpoint));
		first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
		var id = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
		using var rebound = await secondUser.PostAsJsonAsync($"{Route}/subscriptions", Input(installation, endpoint));
		rebound.StatusCode.ShouldBe(HttpStatusCode.OK, await rebound.Content.ReadAsStringAsync());
		using var oldOwner = await firstUser.DeleteAsync($"{Route}/subscriptions/{id}"); oldOwner.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		await WithDbAsync(async db => {
			var row = await db.Set<WebPushSubscription>().SingleAsync(x => x.Id == id);
			row.UserId.ShouldBe(nextUserId); row.IsEnabled.ShouldBeTrue();
		});
	}

	[Theory]
	[InlineData("http://push.example.test/token")]
	[InlineData("https://127.0.0.1/token")]
	[InlineData("https://unconfigured.example.test/token")]
	[InlineData("https://push.example.test@127.0.0.1/token")]
	[InlineData("https://private.push.example.test/token")]
	public async Task Registration_rejects_unsafe_endpoints_without_echoing_subscription_secrets(string endpoint)
	{
		using var client = CreateClientAs(await CreateUserAsync());
		using var response = await client.PostAsJsonAsync($"{Route}/subscriptions", Input(Guid.NewGuid(), endpoint));
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadAsStringAsync();
		body.ShouldContain("Communication:Notification:PushEndpointInvalid");
		body.ShouldNotContain(endpoint); body.ShouldNotContain(BrowserAuth); body.ShouldNotContain(BrowserPublic);
	}

	[Fact]
	public async Task Caller_cannot_supply_authoritative_owner_fields_or_a_stale_application_key_version()
	{
		using var client = CreateClientAs(await CreateUserAsync());
		using var forged = await client.PostAsJsonAsync($"{Route}/subscriptions", new {
			browserInstallationId = Guid.NewGuid(), endpoint = Endpoint(), keys = new { p256dh = BrowserPublic, auth = BrowserAuth },
			vapidKeyVersion = "test-v1", userId = Guid.NewGuid(), tenantId = Guid.NewGuid()
		});
		forged.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await forged.Content.ReadAsStringAsync());
		using var stale = await client.PostAsJsonAsync($"{Route}/subscriptions", Input(Guid.NewGuid(), Endpoint(), "old-key"));
		stale.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await stale.Content.ReadAsStringAsync());
		(await stale.Content.ReadAsStringAsync()).ShouldContain("Communication:Notification:PushKeyChanged");
	}

	[Fact]
	public async Task Actual_audit_storage_records_a_safe_control_request_but_contains_no_subscription_endpoint_or_keys()
	{
		var user = await CreateUserAsync(); using var client = CreateClientAs(user);
		using var control = await client.PostAsJsonAsync("/api/communication/templates", new {
			templateName = "Audit control " + Guid.NewGuid().ToString("N"), subject = "Control", body = "Safe control", channels = new[] { 2 }, languageCode = "en"
		});
		control.StatusCode.ShouldBe(HttpStatusCode.OK, await control.Content.ReadAsStringAsync());
		var endpoint = Endpoint();
		using var registration = await client.PostAsJsonAsync($"{Route}/subscriptions", Input(Guid.NewGuid(), endpoint));
		registration.StatusCode.ShouldBe(HttpStatusCode.OK, await registration.Content.ReadAsStringAsync());
		await WithDbAsync(async db => {
			var logs = await db.Set<AuditLog>().AsNoTracking().Include(x => x.Actions)
				.Include(x => x.EntityChanges).ThenInclude(x => x.PropertyChanges).Where(x => x.UserId == user).ToListAsync();
			logs.ShouldNotBeEmpty();
			logs.SelectMany(x => x.Actions).ShouldContain(x => x.MethodName.Contains("CreateTemplate", StringComparison.Ordinal));
			var stored = JsonSerializer.Serialize(logs);
			stored.ShouldNotContain(endpoint); stored.ShouldNotContain(BrowserAuth); stored.ShouldNotContain(BrowserPublic);
			stored.ShouldNotContain(NotificationSelfPushApiFactory.Base64(NotificationSelfPushApiFactory.Vapid.D!));
		});
	}

	private static string Endpoint() => "https://push.example.test/private-" + Guid.NewGuid().ToString("N");
	private static object Input(Guid installation, string endpoint, string keyVersion = "test-v1", string? publicKey = null, string? auth = null) => new {
		browserInstallationId = installation, endpoint, keys = new { p256dh = publicKey ?? BrowserPublic, auth = auth ?? BrowserAuth }, vapidKeyVersion = keyVersion
	};
	private async Task<Guid> CreateUserAsync()
	{
		var id = Guid.NewGuid(); await WithDbAsync(db => {
			db.Add(new IdentityUser(id, $"push-{id:N}", $"{id:N}@example.test", Guid.Parse(TestAuthHandler.TestTenantId)));
			return Task.CompletedTask;
		}); return id;
	}
	private async Task WithDbAsync(Func<ShikshaMitraDbContext, Task> action)
	{
		using var scope = Factory.Services.CreateScope(); var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		await action(db); await uow.CompleteAsync();
	}
}
