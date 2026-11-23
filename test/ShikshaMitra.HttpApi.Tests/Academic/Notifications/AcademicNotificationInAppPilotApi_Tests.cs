using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Performance;
using ShikshaMitra.BackgroundWorkers;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

[Collection(ApiTestCollection.Name)]
public class AcademicNotificationInAppPilotApi_Tests(AcademicInAppPilotApiFactory factory)
	: ShikshaMitraApiTestBase(factory), IClassFixture<AcademicInAppPilotApiFactory>
{
	private const string Root = "/api/academic/notifications";

	[AcademicPerformancePostgresFact]
	public async Task Paused_worker_delivers_only_the_selected_synthetic_test_to_its_designated_inbox()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(",",
			ShikshaMitraSubmodulePermissions.Academics.Notification.View,
			ShikshaMitraSubmodulePermissions.Academics.Notification.Add));
		var heldDraft = await CreateDraftAsync(AcademicNotificationEventKeys.HomeworkPublished);
		var held = await QueueTestAsync(heldDraft);
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var recipientId = Guid.NewGuid();
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using (services.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			await services.GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(
				new IdentityUser(recipientId, "inapp-pilot-recipient", "inapp-pilot@example.invalid", tenantId), autoSave: true);
			await unit.CompleteAsync();
		}
		var configuration = services.GetRequiredService<IConfiguration>();
		configuration[$"Notifications:Academic:Tenants:{tenantId:N}:TemplateTest:InApp:UserId"] = recipientId.ToString();
		var draft = await CreateDraftAsync(AcademicNotificationEventKeys.HomeworkDue);
		var selected = await QueueTestAsync(draft);
		held.OperationId.ShouldNotBe(selected.OperationId);
		const string queue = "Notifications:QueueWorker:";
		configuration[queue + "Enabled"] = "false";
		configuration[queue + "InAppPilot:TenantId"] = TestAuthHandler.TestTenantId;
		configuration[queue + "InAppPilot:OperationId"] = selected.OperationId.ToString();
		configuration[queue + "InAppPilot:RecipientUserId"] = recipientId.ToString();
		NotificationQueueWorkerConfiguration.IsEnabled(configuration).ShouldBeFalse();
		var worker = scope.ServiceProvider.GetRequiredService<AcademicNotificationDispatchWorker>();
		await worker.StartAsync();
		try
		{
			using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(65));
			while (await ReadOperationStatusAsync(selected.OperationId) != CommunicationCampaignStatus.Sent)
				await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
			(await ReadDeliveryAsync(draft, selected.OperationId)).ShouldBe(NotificationDispatchState.Delivered);
			(await ReadDeliveryAsync(heldDraft, held.OperationId)).ShouldBe(NotificationDispatchState.Pending);
			const string inboxPath = "/api/communication/notifications/me/inbox";
			using var recipient = CreateClientAs(recipientId);
			var inbox = await recipient.GetFromJsonAsync<PagedResultDto<NotificationInboxItemDto>>(inboxPath);
			var item = inbox!.Items.ShouldHaveSingleItem();
			item.Title.ShouldBe("[Test] Homework reminder");
			item.Body.ShouldContain("Example recipient");
			(await GetAndReadAsync<PagedResultDto<NotificationInboxItemDto>>(inboxPath)).Items.ShouldBeEmpty();
		}
		finally
		{
			await worker.StopAsync();
		}
	}

	private Task<NotificationTemplateDto> CreateDraftAsync(string eventKey) =>
		PostAndReadAsync<NotificationTemplateDto>(Root + "/templates",
			new CreateNotificationTemplateDto(eventKey, "Guardian", CommunicationChannelType.InApp, "en",
				"In-App pilot sample", "Homework reminder", "Hello {{recipient_name}}. Details: {{source_link}}"));

	private async Task<CommunicationCampaignStatus> ReadOperationStatusAsync(Guid operationId)
	{
		var operation = await GetAndReadAsync<JsonElement>($"{Root}/operations/{operationId}");
		return (CommunicationCampaignStatus)operation.GetProperty("status").GetInt32();
	}

	private async Task<AcademicNotificationTemplateTestSendDto> QueueTestAsync(NotificationTemplateDto draft)
	{
		using var response = await Client.PostAsJsonAsync($"{Root}/templates/{draft.TemplateId}/test-send", new {
			revisionId = draft.RevisionId, concurrencyStamp = draft.ConcurrencyStamp
		});
		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AcademicNotificationTemplateTestSendDto>(JsonOptions))!;
	}

	private async Task<NotificationDispatchState> ReadDeliveryAsync(NotificationTemplateDto draft, Guid operationId)
	{
		var history = await GetAndReadAsync<PagedResultDto<AcademicNotificationHistoryItemDto>>($"{Root}/history?" +
			$"sourceType={NotificationCampaignTypes.AcademicTemplateTestSource}&eventKey={draft.EventKey}&sourceId={draft.TemplateId}&operationId={operationId}");
		return history.Items.ShouldHaveSingleItem().Status;
	}
}

public sealed class AcademicInAppPilotApiFactory : AcademicPerformancePostgresTestFactory
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection([
			new("App:ClientUrl", "https://school.example.invalid")
		]));
	}
}
