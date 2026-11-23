using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
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
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

public sealed partial class AcademicNotificationEndToEndApi_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task Paused_queue_runs_only_one_selected_email_or_push_template_test()
	{
		await using var mailbox = new LoopbackSmtpMailbox();
		await using var fixture = new DeliveryApiFactory(mailbox.Configuration);
		using var staff = fixture.CreateClient();
		staff.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(",",
			ShikshaMitraSubmodulePermissions.Academics.Notification.View,
			ShikshaMitraSubmodulePermissions.Academics.Notification.Add));
		var sample = new SchoolSample();
		await InSchoolAsync(fixture, services => SeedHomeworkAsync(fixture, services, sample));
		using var primary = PersonalClient(fixture, sample.Primary, "Guardian");
		var settings = fixture.Services.GetRequiredService<IConfiguration>();
		settings["Notifications:WebPush:PilotTenantId"] = School.ToString();
		settings[$"Notifications:Academic:Tenants:{School:N}:TemplateTest:Email:UserId"] = sample.Primary.ToString();
		settings[$"Notifications:Academic:Tenants:{School:N}:TemplateTest:WebPush:UserId"] = sample.Primary.ToString();
		settings["Notifications:QueueWorker:Enabled"] = "false";
		const string pilotPrefix = "Notifications:QueueWorker:ExternalPilot:";

		var browserKeys = NotificationSelfPushApiFactory.CreateParameters();
		var subscription = await PostAsync<WebPushSubscriptionDto>(primary, $"{Self}/push/subscriptions",
			new RegisterWebPushSubscriptionDto(Guid.NewGuid(), PushEndpoint, new(
				NotificationSelfPushApiFactory.PublicKey(browserKeys),
				NotificationSelfPushApiFactory.Base64(RandomNumberGenerator.GetBytes(16))), DeliveryApiFactory.PushVersion));
		subscription.IsEnabled.ShouldBeTrue();
		var email = await QueueAsync(CommunicationChannelType.Email);
		var push = await QueueAsync(CommunicationChannelType.WebPush);
		var heldEmail = await QueueAsync(CommunicationChannelType.Email);

		SelectPilot(CommunicationChannelType.Email, email.OperationId);
		using (var scope = fixture.Services.CreateScope())
		{
			var worker = scope.ServiceProvider.GetRequiredService<AcademicNotificationDispatchWorker>();
			await worker.StartAsync();
			try
			{
				using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(65));
				while (await StatusAsync(email) is NotificationDispatchState.Pending or NotificationDispatchState.Processing)
					await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
			}
			finally
			{
				await worker.StopAsync();
			}
		}
		(await StatusAsync(email)).ShouldBe(NotificationDispatchState.ProviderAccepted);
		(await StatusAsync(push)).ShouldBe(NotificationDispatchState.Pending);
		(await StatusAsync(heldEmail)).ShouldBe(NotificationDispatchState.Pending);
		var message = mailbox.Accepted.ShouldHaveSingleItem();
		message.Recipient.ShouldBe(PrimaryEmail);
		message.Message.Subject.ShouldBe("[Test] Homework reminder");
		fixture.Push.Requests.ShouldBeEmpty();

		await RunPilotAsync(CommunicationChannelType.WebPush, push.OperationId);
		(await StatusAsync(push)).ShouldBe(NotificationDispatchState.ProviderAccepted);
		(await StatusAsync(heldEmail)).ShouldBe(NotificationDispatchState.Pending);
		var request = fixture.Push.Requests.ShouldHaveSingleItem();
		request.Endpoint.ShouldBe(new Uri(PushEndpoint));
		request.ContentEncoding.ShouldBe("aes128gcm");
		await RunPilotAsync(CommunicationChannelType.WebPush, push.OperationId);
		await RunPilotAsync(CommunicationChannelType.Email, email.OperationId);
		mailbox.RecipientRejections[PrimaryEmail] = "450 Temporary mailbox rejection";
		await RunPilotAsync(CommunicationChannelType.Email, heldEmail.OperationId);
		(await StatusAsync(heldEmail)).ShouldBe(NotificationDispatchState.Failed);
		await RunPilotAsync(CommunicationChannelType.Email, heldEmail.OperationId);
		(await StatusAsync(heldEmail)).ShouldBe(NotificationDispatchState.Failed);
		mailbox.Accepted.Count.ShouldBe(1);
		fixture.Push.Requests.Count.ShouldBe(1);
		fixture.WhatsApp.Requests.ShouldBeEmpty();

		async Task<(NotificationTemplateDto Draft, Guid OperationId)> QueueAsync(CommunicationChannelType channel)
		{
			var draft = await PostAsync<NotificationTemplateDto>(staff, $"{Root}/templates",
				new CreateNotificationTemplateDto(Event, "Guardian", channel, "en",
					$"Pilot {channel} {Guid.NewGuid():N}", "Homework reminder",
					channel == CommunicationChannelType.WebPush ? "A homework update is available." : "Hello {{recipient_name}}. Details: {{source_link}}"));
			var queued = await PostAsync<AcademicNotificationTemplateTestSendDto>(staff,
				$"{Root}/templates/{draft.TemplateId}/test-send",
				new { revisionId = draft.RevisionId, concurrencyStamp = draft.ConcurrencyStamp }, HttpStatusCode.Accepted);
			return (draft, queued.OperationId);
		}

		async Task<NotificationDispatchState> StatusAsync((NotificationTemplateDto Draft, Guid OperationId) queued)
		{
			var history = await staff.GetFromJsonAsync<PagedResultDto<AcademicNotificationHistoryItemDto>>(
				$"{Root}/history?sourceType={NotificationCampaignTypes.AcademicTemplateTestSource}&eventKey={Event}" +
				$"&sourceId={queued.Draft.TemplateId}&operationId={queued.OperationId}");
			return history!.Items.Single().Status;
		}

		NotificationTemplateTestPilot SelectPilot(CommunicationChannelType channel, Guid operationId)
		{
			settings[pilotPrefix + "TenantId"] = School.ToString();
			settings[pilotPrefix + "OperationId"] = operationId.ToString();
			settings[pilotPrefix + "RecipientUserId"] = sample.Primary.ToString();
			settings[pilotPrefix + "Channel"] = channel.ToString();
			NotificationQueueWorkerConfiguration.IsEnabled(settings).ShouldBeFalse();
			return NotificationQueueWorkerConfiguration.ReadTemplateTestPilot(settings).ShouldNotBeNull();
		}

		async Task RunPilotAsync(CommunicationChannelType channel, Guid operationId)
		{
			var pilot = SelectPilot(channel, operationId);
			using var scope = fixture.Services.CreateScope();
			var services = scope.ServiceProvider;
			using var tenant = services.GetRequiredService<ICurrentTenant>().Change(School);
			await services.GetRequiredService<NotificationTenantQueueRunner>().RunTemplateTestPilotAsync(pilot, default);
		}
	}
}
