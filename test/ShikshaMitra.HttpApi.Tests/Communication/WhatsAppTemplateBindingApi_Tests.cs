using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication;

[CollectionDefinition(Name)]
public class WhatsAppTemplateBindingApiCollection : ICollectionFixture<WhatsAppTemplateBindingApiFactory>
{
	public const string Name = "WhatsApp template binding";
}

public class WhatsAppTemplateBindingApiFactory : ShikshaMitraApiTestFactory
{
	internal static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	internal const string TestSecret = "local-whatsapp-template-secret";

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?> {
			["Notifications:WhatsApp:GraphApiVersion"] = "v26.0",
			[$"Notifications:WhatsApp:Tenants:{TenantId:N}:AccessTokens:binding"] = TestSecret
		}));
		builder.ConfigureTestServices(services => services.AddHttpClient(WhatsAppProviderClient.HttpClientName)
			.ConfigurePrimaryHttpMessageHandler(services => new GraphHandler(services.GetRequiredService<IUnitOfWorkManager>())));
	}

	private sealed class GraphHandler(IUnitOfWorkManager units) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			units.Current.ShouldBeNull();
			request.Method.ShouldBe(HttpMethod.Get);
			request.RequestUri!.AbsolutePath.ShouldBe("/v26.0/2001/message_templates");
			request.Headers.Authorization?.Parameter.ShouldBe(TestSecret);
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""
				{"data":[{"id":"3001","name":"academic_homework","language":"en_US","category":"UTILITY","status":"APPROVED",
				"components":[{"type":"BODY","text":"Homework for {{1}}. View {{2}}."}]}]}
				""") });
		}
	}
}

[Collection(WhatsAppTemplateBindingApiCollection.Name)]
public class WhatsAppTemplateBindingApi_Tests(WhatsAppTemplateBindingApiFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Route = "/api/communication/notifications/providers/whatsapp/templates";
	private const string ProviderView = ShikshaMitraSubmodulePermissions.Communication.Settings_ProviderSettings.View;
	private const string ProviderAdd = ShikshaMitraSubmodulePermissions.Communication.Settings_ProviderSettings.Add;
	private const string AcademicView = ShikshaMitraSubmodulePermissions.Academics.Notification.View;
	private const string AcademicAdd = ShikshaMitraSubmodulePermissions.Academics.Notification.Add;

	[Fact]
	public async Task Staff_can_create_submit_and_sync_a_binding_without_exposing_account_configuration()
	{
		var input = await SeedAsync();
		using var beforeContext = await Client.GetAsync(ContextUrl(input));
		beforeContext.StatusCode.ShouldBe(HttpStatusCode.OK, await beforeContext.Content.ReadAsStringAsync());
		beforeContext.Headers.CacheControl?.NoStore.ShouldBe(true);
		var initial = await beforeContext.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		initial.GetProperty("providerSettingId").GetGuid().ShouldBe(input.ProviderSettingId);
		initial.GetProperty("binding").ValueKind.ShouldBe(JsonValueKind.Null);
		using var missing = await Client.GetAsync(GetUrl(input));
		missing.StatusCode.ShouldBe(HttpStatusCode.NoContent, await missing.Content.ReadAsStringAsync());
		using var create = await Client.PostAsJsonAsync(Route, input);
		create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
		var draft = (await create.Content.ReadFromJsonAsync<WhatsAppTemplateBindingDto>(JsonOptions))!;
		draft.ProviderStatus.ShouldBe(NotificationProviderTemplateState.Draft);
		using var submitted = await Client.PostAsync($"{Route}/{draft.Id}/submit", null);
		submitted.StatusCode.ShouldBe(HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());
		using var synced = await Client.PostAsync($"{Route}/{draft.Id}/sync", null);
		synced.StatusCode.ShouldBe(HttpStatusCode.OK, await synced.Content.ReadAsStringAsync());
		using var get = await Client.GetAsync(GetUrl(input));
		get.StatusCode.ShouldBe(HttpStatusCode.OK, await get.Content.ReadAsStringAsync());
		get.Headers.CacheControl?.NoStore.ShouldBe(true);
		var binding = (await get.Content.ReadFromJsonAsync<WhatsAppTemplateBindingDto>(JsonOptions))!;
		binding.Id.ShouldBe(draft.Id);
		binding.ProviderStatus.ShouldBe(NotificationProviderTemplateState.Approved);
		binding.ProviderTemplateId.ShouldBe("3001");
		using var context = await Client.GetAsync(ContextUrl(input));
		context.StatusCode.ShouldBe(HttpStatusCode.OK, await context.Content.ReadAsStringAsync());
		var selected = await context.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		selected.GetProperty("bindingMatchesProvider").GetBoolean().ShouldBeTrue();
		selected.GetProperty("binding").GetProperty("revisionId").GetGuid().ShouldBe(input.RevisionId);
		selected.GetProperty("binding").GetProperty("providerStatus").GetInt32().ShouldBe((int)NotificationProviderTemplateState.Approved);
		var payload = await context.Content.ReadAsStringAsync();
		payload.ShouldNotContain(WhatsAppTemplateBindingApiFactory.TestSecret);
		payload.ShouldNotContain("phoneNumberId");
		payload.ShouldNotContain("businessAccountId");
		payload.ShouldNotContain("parameterMappingsJson");
		await ChangeProviderPhoneAsync(input.ProviderSettingId);
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{ProviderView},{AcademicView}");
		using var changed = await Client.GetAsync(ContextUrl(input));
		changed.StatusCode.ShouldBe(HttpStatusCode.OK, await changed.Content.ReadAsStringAsync());
		var changedContext = await changed.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		changedContext.GetProperty("bindingMatchesProvider").GetBoolean().ShouldBeFalse();
		changedContext.GetProperty("binding").GetProperty("id").GetGuid().ShouldBe(draft.Id);
	}

	[Theory]
	[InlineData(ProviderView, ProviderAdd)]
	[InlineData(AcademicView, AcademicAdd)]
	public async Task One_permission_group_alone_does_not_authorize_binding_operations(string view, string add)
	{
		var input = await SeedAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{view},{add}");
		using var read = await Client.GetAsync(GetUrl(input));
		read.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await read.Content.ReadAsStringAsync());
		using var context = await Client.GetAsync(ContextUrl(input));
		context.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await context.Content.ReadAsStringAsync());
		using var create = await Client.PostAsJsonAsync(Route, input);
		create.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await create.Content.ReadAsStringAsync());
	}

	private static string GetUrl(CreateWhatsAppTemplateBindingDto input) =>
		$"{Route}?templateId={input.TemplateId}&revisionId={input.RevisionId}&providerSettingId={input.ProviderSettingId}";

	private static string ContextUrl(CreateWhatsAppTemplateBindingDto input) =>
		$"{Route}/context?templateId={input.TemplateId}&revisionId={input.RevisionId}";

	private async Task ChangeProviderPhoneAsync(Guid providerId)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(WhatsAppTemplateBindingApiFactory.TenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var provider = await db.Set<ChannelProviderSetting>().SingleAsync(x => x.Id == providerId);
		provider.ConfigurationJson = """{"schemaVersion":1,"phoneNumberId":"1002","businessAccountId":"2001"}""";
		await unit.CompleteAsync();
	}

	private async Task<CreateWhatsAppTemplateBindingDto> SeedAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = WhatsAppTemplateBindingApiFactory.TenantId;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var header = new CommunicationTemplate(Guid.NewGuid(), tenantId) {
			TemplateName = "Homework", LanguageCode = "en-US", TargetRole = "Guardian"
		};
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), tenantId, header.Id, 1, "Academic.Homework.Published", "Guardian",
			CommunicationChannelType.WhatsApp, "en-US", "", "Homework for {{student_name}}. View {{source_link}}.", 1);
		revision.Publish(Guid.Parse(TestAuthHandler.TestUserId), DateTimeOffset.UtcNow);
		var provider = new ChannelProviderSetting(Guid.NewGuid(), tenantId) {
			Channel = CommunicationChannelType.WhatsApp, ProviderName = WhatsAppProviderRoute.ProviderName,
			IsEnabled = true, IsDefault = true, Status = CommunicationProviderStatus.Enabled, SecretReference = "binding",
			ConfigurationJson = """{"schemaVersion":1,"phoneNumberId":"1001","businessAccountId":"2001"}"""
		};
		db.AddRange(header, revision, provider);
		await unit.CompleteAsync();
		return new(header.Id, revision.Id, provider.Id, "academic_homework");
	}
}
