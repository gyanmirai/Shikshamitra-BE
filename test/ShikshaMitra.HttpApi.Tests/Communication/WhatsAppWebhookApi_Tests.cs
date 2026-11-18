using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication;

[CollectionDefinition(Name)]
public class WhatsAppWebhookApiCollection : ICollectionFixture<WhatsAppWebhookApiFactory>
{
	public const string Name = "WhatsApp provider callback";
}

public class WhatsAppWebhookApiFactory : ShikshaMitraApiTestFactory
{
	internal static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	internal static readonly Guid ProviderId = Guid.NewGuid();
	internal const string Route = "/api/communication/notifications/providers/whatsapp/webhook";
	internal const string VerifyToken = "local-whatsapp-verification-token";
	internal const string Secret = "local-whatsapp-app-secret";

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?> {
			["Notifications:WhatsApp:Webhook:VerificationToken"] = VerifyToken,
			["Notifications:WhatsApp:Webhook:AppSecret"] = Secret,
			["Notifications:WhatsApp:Webhook:Accounts:2001:1001:TenantId"] = TenantId.ToString(),
			["Notifications:WhatsApp:Webhook:Accounts:2001:1001:ProviderSettingId"] = ProviderId.ToString(),
			["Notifications:WhatsApp:Webhook:Accounts:2001:9999:TenantId"] = TenantId.ToString(),
			["Notifications:WhatsApp:Webhook:Accounts:2001:9999:ProviderSettingId"] = ProviderId.ToString()
		}));
	}
}

[Collection(WhatsAppWebhookApiCollection.Name)]
public class WhatsAppWebhookApi_Tests(WhatsAppWebhookApiFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Route = WhatsAppWebhookApiFactory.Route;

	[Fact]
	public async Task Verification_echoes_the_challenge_only_with_the_exact_token()
	{
		using var client = CreateAnonymousClient();
		using var accepted = await client.GetAsync($"{Route}?hub.mode=subscribe&hub.challenge=12345&hub.verify_token={WhatsAppWebhookApiFactory.VerifyToken}");
		accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await accepted.Content.ReadAsStringAsync()).ShouldBe("12345");
		accepted.Content.Headers.ContentType?.MediaType.ShouldBe("text/plain");
		using var denied = await client.GetAsync($"{Route}?hub.mode=subscribe&hub.challenge=12345&hub.verify_token=wrong");
		denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Invalid_signature_payload_and_oversized_body_do_not_store_an_event()
	{
		using var client = CreateAnonymousClient();
		using var unsigned = await PostAsync(client, Body("wamid.invalid"), sign: false);
		unsigned.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		using var invalid = await PostAsync(client, "{");
		invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		using var oversized = await PostAsync(client, new string('x', 300_000));
		oversized.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
		(await GetEventsAsync("wamid.invalid")).ShouldBeEmpty();
	}

	[Fact]
	public async Task Signed_delivery_receipts_require_the_current_configured_account_and_are_deduplicated()
	{
		await SeedProviderAsync();
		using var client = CreateAnonymousClient();
		const string id = "wamid.delivery_test";
		using var wrongAccount = await PostAsync(client, Body(id, businessAccount: "9999"));
		wrongAccount.StatusCode.ShouldBe(HttpStatusCode.OK);
		using var wrongPhone = await PostAsync(client, Body(id, phone: "9999"));
		wrongPhone.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await GetEventsAsync(id)).ShouldBeEmpty();
		using var first = await PostAsync(client, Body(id));
		first.StatusCode.ShouldBe(HttpStatusCode.OK);
		using var duplicate = await PostAsync(client, " " + Body(id));
		duplicate.StatusCode.ShouldBe(HttpStatusCode.OK);
		var row = (await GetEventsAsync(id)).ShouldHaveSingleItem();
		row.TenantId.ShouldBe(WhatsAppWebhookApiFactory.TenantId);
		row.ProviderSettingId.ShouldBe(WhatsAppWebhookApiFactory.ProviderId);
		row.ProtectedPayload.ShouldNotContain(id);
		row.ProtectedPayload.ShouldNotContain("15550000100");
	}

	private static string Body(string id, string businessAccount = "2001", string phone = "1001") => $$$"""
		{"object":"whatsapp_business_account","entry":[{"id":"{{{businessAccount}}}","changes":[{"field":"messages",
		"value":{"messaging_product":"whatsapp","metadata":{"phone_number_id":"{{{phone}}}"},"statuses":[
		{"id":"{{{id}}}","status":"delivered","timestamp":"1795000000","recipient_id":"15550000100"}]}}]}]}
		""";

	private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string body, bool sign = true)
	{
		using var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
		if (sign)
			request.Headers.Add("X-Hub-Signature-256", "sha256=" + Convert.ToHexString(HMACSHA256.HashData(
				Encoding.UTF8.GetBytes(WhatsAppWebhookApiFactory.Secret), Encoding.UTF8.GetBytes(body))));
		return await client.SendAsync(request);
	}

	private Task SeedProviderAsync() => WithDbAsync(async db => {
		db.Add(new ChannelProviderSetting(WhatsAppWebhookApiFactory.ProviderId, WhatsAppWebhookApiFactory.TenantId) {
			Channel = CommunicationChannelType.WhatsApp, ProviderName = WhatsAppProviderRoute.ProviderName,
			IsEnabled = true, Status = CommunicationProviderStatus.Enabled,
			ConfigurationJson = """{"schemaVersion":1,"phoneNumberId":"1001","businessAccountId":"2001"}"""
		});
		await db.SaveChangesAsync();
	});

	private async Task<List<NotificationProviderEvent>> GetEventsAsync(string id)
	{
		List<NotificationProviderEvent> rows = [];
		await WithDbAsync(async db => rows = await db.Set<NotificationProviderEvent>().Where(x => x.ProviderMessageId == id).ToListAsync());
		return rows;
	}

	private async Task WithDbAsync(Func<ShikshaMitraDbContext, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(WhatsAppWebhookApiFactory.TenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		await action(db);
		await unit.CompleteAsync();
	}
}
