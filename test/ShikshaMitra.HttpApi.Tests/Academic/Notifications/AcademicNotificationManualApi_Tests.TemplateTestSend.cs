using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public partial class AcademicNotificationManualApi_Tests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public async Task Template_test_send_queues_a_synthetic_draft_for_the_authenticated_staff_account(
		bool changeBeforeDelivery, bool rejectUnexpectedPilotRecipient)
	{
		var draft = await CreateTestSendDraftAsync();
		using var response = await Client.PostAsJsonAsync(TestSendRoute(draft), new
		{
			revisionId = draft.RevisionId, concurrencyStamp = draft.ConcurrencyStamp
		});
		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
		var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		var operationId = result.GetProperty("operationId").GetGuid();
		result.GetProperty("status").GetInt32().ShouldBe((int)CommunicationCampaignStatus.Queued);
		result.GetProperty("channel").GetInt32().ShouldBe((int)CommunicationChannelType.InApp);
		result.GetProperty("isSynthetic").GetBoolean().ShouldBeTrue();
		var responseBody = await response.Content.ReadAsStringAsync();
		responseBody.ShouldNotContain("Example recipient");
		responseBody.ShouldNotContain(TestAuthHandler.TestUserId);
		var operationRoute = $"{BasePath}/operations/{operationId}";
		var historyRoute = $"{BasePath}/history?sourceType={NotificationCampaignTypes.AcademicTemplateTestSource}" +
			$"&eventKey={draft.EventKey}&sourceId={draft.TemplateId}";
		Permissions(Client, View);
		var initialStatus = await GetAndReadAsync<JsonElement>(operationRoute);
		initialStatus.GetProperty("isSynthetic").GetBoolean().ShouldBeTrue();
		var initialHistory = await GetAndReadAsync<PagedResultDto<AcademicNotificationHistoryItemDto>>($"{historyRoute}&operationId={operationId}");
		initialHistory.Items.ShouldHaveSingleItem().Status.ShouldBe(NotificationDispatchState.Pending);
		using var missingOperation = await Client.GetAsync(historyRoute);
		missingOperation.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		if (!changeBeforeDelivery)
		{
			await SeedApproverAsync();
			using var colleague = CreateClientAs(AcademicNotificationPreviewApiFactory.ApproverId);
			Permissions(colleague, $"{View},{Add}");
			using var otherOperation = await colleague.GetAsync(operationRoute);
			otherOperation.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			using var otherHistory = await colleague.GetAsync($"{historyRoute}&operationId={operationId}");
			otherHistory.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			Permissions(Client, $"{View},{Add}");
			using var cancel = await Client.PostAsync($"{operationRoute}/cancel", null);
			cancel.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		}
		var lease = Guid.NewGuid();
		Guid dispatchId = Guid.Empty;
		await WithDatabaseAsync(async (db, services) =>
		{
			var operation = await db.CommunicationCampaigns.SingleAsync(x => x.Id == operationId);
			operation.SourceEntityId.ShouldBe(draft.TemplateId);
			operation.EligibleRecipientCount.ShouldBe(1);
			var row = await db.NotificationDispatches.SingleAsync(x => x.CampaignId == operationId);
			row.TemplateRevisionId.ShouldBe(draft.RevisionId);
			row.Channel.ShouldBe(CommunicationChannelType.InApp);
			var message = services.GetRequiredService<NotificationDispatchPayloadProtector>().Unprotect(row);
			message.Recipient.UserId.ShouldBe(Guid.Parse(TestAuthHandler.TestUserId));
			message.Recipient.StudentId.ShouldBeNull();
			message.Subject.ShouldStartWith("[Test]");
			message.Body.ShouldContain("Example recipient");
			message.Body.ShouldNotContain("Private source text");
			var current = await db.Set<CommunicationTemplateRevision>().Where(x => x.Id == draft.RevisionId).SingleAsync();
			current.IsPublished.ShouldBeFalse();
			var intent = await db.NotificationIntents.SingleAsync(x => x.Id == row.IntentId);
			intent.Origin.ShouldBe(NotificationIntentOrigin.ReviewedManual);
			intent.RuleId.ShouldBeNull();
			intent.SourceType.ShouldBe(NotificationCampaignTypes.AcademicTemplateTestSource);
			intent.SourceId.ShouldBe(current.TemplateId);
			intent.EventKey.ShouldBe(current.EventKey);
			intent.SourceRevision.ShouldBe(current.ContentHash);
			intent.ActorUserId.ShouldBe(Guid.Parse(TestAuthHandler.TestUserId));
			(intent.ExpiresAt > DateTimeOffset.UtcNow).ShouldBeTrue();
			NotificationRuntimeJson.Deserialize<Guid[]>(intent.TemplateRevisionIdsJson, NotificationErrorCodes.TemplateInvalid)
				.ShouldBe([current.Id]);
			dispatchId = row.Id;
			var claim = db.Entry(row);
			claim.Property(x => x.Status).CurrentValue = NotificationDispatchState.Processing;
			claim.Property(x => x.LeaseId).CurrentValue = lease;
			claim.Property(x => x.LeaseExpiresAt).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(5);
			claim.Property(x => x.AttemptNumber).CurrentValue = 1;
		});

		if (changeBeforeDelivery)
		{
			Permissions(Client, $"{View},{Add}");
			var current = await GetAndReadAsync<NotificationTemplateDto>($"{BasePath}/templates/{draft.TemplateId}");
			using var change = await Client.PutAsJsonAsync($"{BasePath}/templates/{draft.TemplateId}/draft",
				new UpdateNotificationTemplateDraftDto(current.RevisionId, "Edited homework reminder", current.Body, current.ConcurrencyStamp));
			change.StatusCode.ShouldBe(HttpStatusCode.OK, await change.Content.ReadAsStringAsync());
		}

		using (var scope = Factory.Services.CreateScope())
		{
			var services = scope.ServiceProvider;
			using var tenant = services.GetRequiredService<ICurrentTenant>().Change(AcademicNotificationPreviewApiFactory.TenantId);
			await services.GetRequiredService<INotificationDispatcher>().DispatchAsync(dispatchId, lease, CancellationToken.None,
				pilot: rejectUnexpectedPilotRecipient
					? new(AcademicNotificationPreviewApiFactory.TenantId, operationId, Guid.NewGuid()) : null);
		}
		var expected = changeBeforeDelivery || rejectUnexpectedPilotRecipient
			? NotificationDispatchState.Skipped : NotificationDispatchState.Delivered;
		await WithDatabaseAsync(async (db, _) =>
		{
			var row = await db.NotificationDispatches.SingleAsync(x => x.Id == dispatchId);
			row.Status.ShouldBe(expected, row.ErrorCode);
			if (changeBeforeDelivery) row.ErrorCode.ShouldBe("SourceRevisionChanged");
			if (rejectUnexpectedPilotRecipient) row.ErrorCode.ShouldBe(NotificationErrorCodes.RecipientIneligible);
		});
		var finalHistory = await GetAndReadAsync<PagedResultDto<AcademicNotificationHistoryItemDto>>($"{historyRoute}&operationId={operationId}");
		finalHistory.Items.ShouldHaveSingleItem().Status.ShouldBe(expected);
		finalHistory.Items.Single().CanRequestRetry.ShouldBeFalse();
		if (expected != NotificationDispatchState.Delivered) return;
		var inbox = await GetAndReadAsync<PagedResultDto<NotificationInboxItemDto>>("/api/communication/notifications/me/inbox");
		inbox.Items.ShouldContain(x => x.Title == "[Test] Homework reminder" && x.Body.Contains("Example recipient", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Template_test_send_requires_add_and_rejects_arbitrary_destinations_and_stale_revisions()
	{
		var draft = await CreateTestSendDraftAsync();
		var route = TestSendRoute(draft);
		Permissions(Client, View);
		using var forbidden = await Client.PostAsJsonAsync(route, new { revisionId = draft.RevisionId, concurrencyStamp = draft.ConcurrencyStamp });
		forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await forbidden.Content.ReadAsStringAsync());

		Permissions(Client, Add);
		using var invalid = await Client.PostAsJsonAsync(route, new
		{
			revisionId = draft.RevisionId, concurrencyStamp = draft.ConcurrencyStamp, destination = "unapproved@example.invalid"
		});
		invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await invalid.Content.ReadAsStringAsync());
		using var wrongChannel = await Client.PostAsJsonAsync(route, new
		{
			revisionId = draft.RevisionId, concurrencyStamp = draft.ConcurrencyStamp, webPushSubscriptionId = Guid.NewGuid()
		});
		wrongChannel.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await wrongChannel.Content.ReadAsStringAsync());
		var wrongChannelDetails = await wrongChannel.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		wrongChannelDetails.GetProperty("errorCode").GetString().ShouldBe(NotificationErrorCodes.TemplateInvalid);
		using var stale = await Client.PostAsJsonAsync(route, new { revisionId = draft.RevisionId, concurrencyStamp = "stale" });
		stale.StatusCode.ShouldBe(HttpStatusCode.Conflict, await stale.Content.ReadAsStringAsync());
		await WithDatabaseAsync(async (db, _) =>
		{
			(await db.CommunicationCampaigns.AnyAsync(x => x.SourceEntityId == draft.TemplateId)).ShouldBeFalse();
		});
	}

	[Fact]
	public async Task Template_test_send_is_rate_limited_for_the_whole_school_channel()
	{
		var draft = await CreateTestSendDraftAsync();
		var tenant = AcademicNotificationPreviewApiFactory.TenantId;
		var fixtureRows = Enumerable.Range(0, 5).Select(_ => new CommunicationCampaign(Guid.NewGuid(), tenant)
		{
			CampaignType = NotificationCampaignTypes.Academic, SourceModule = CommunicationSourceModule.Academic,
			SourceEntityType = NotificationCampaignTypes.AcademicTemplateTestSource, SourceEntityId = Guid.NewGuid(),
			Title = "Rate limit fixture", Body = string.Empty, Status = CommunicationCampaignStatus.Queued,
			ChannelsJson = NotificationRuntimeJson.Serialize(new[] { CommunicationChannelType.InApp }, NotificationErrorCodes.TemplateInvalid),
			CreationTime = DateTime.UtcNow
		}).ToArray();
		var ids = fixtureRows.Select(x => x.Id).ToArray();
		await WithDatabaseAsync((db, _) => { db.AddRange(fixtureRows); return Task.CompletedTask; });
		try
		{
			using var response = await Client.PostAsJsonAsync(TestSendRoute(draft), new { revisionId = draft.RevisionId, concurrencyStamp = draft.ConcurrencyStamp });
			response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests, await response.Content.ReadAsStringAsync());
			var details = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
			details.GetProperty("errorCode").GetString().ShouldBe(NotificationErrorCodes.TemplateTestRateLimited);
			await WithDatabaseAsync(async (db, _) =>
			{
				(await db.CommunicationCampaigns.AnyAsync(x => x.SourceEntityId == draft.TemplateId)).ShouldBeFalse();
			});
		}
		finally
		{
			await WithDatabaseAsync(async (db, _) => db.RemoveRange(await db.CommunicationCampaigns.Where(x => ids.Contains(x.Id)).ToListAsync()));
		}
	}

	[Fact]
	public async Task Template_test_send_requires_a_designated_account_for_whatsapp()
	{
		var draft = await CreateTestSendDraftAsync(CommunicationChannelType.WhatsApp);
		using var response = await Client.PostAsJsonAsync(TestSendRoute(draft), new { revisionId = draft.RevisionId, concurrencyStamp = draft.ConcurrencyStamp });
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		var details = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		details.GetProperty("errorCode").GetString().ShouldBe(NotificationErrorCodes.TemplateTestAccountUnavailable);
	}

	[Fact]
	public async Task Web_push_template_test_pins_one_active_device_for_the_designated_account()
	{
		const string version = "template-test-v1";
		var tenantId = AcademicNotificationPreviewApiFactory.TenantId;
		var actorId = Guid.Parse(TestAuthHandler.TestUserId);
		var recipientId = AcademicNotificationPreviewApiFactory.ApproverId;
		var chosenDevice = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
		var otherDevice = Guid.NewGuid();
		var actorDevice = Guid.NewGuid();
		var oldDevice = Guid.NewGuid();
		var configuration = Factory.Services.GetRequiredService<IConfiguration>();
		var pair = NotificationSelfPushApiFactory.CreateParameters();
		var settings = new Dictionary<string, string?> {
			["Notifications:WebPush:PublicKey"] = NotificationSelfPushApiFactory.PublicKey(pair),
			["Notifications:WebPush:PrivateKey"] = NotificationSelfPushApiFactory.Base64(pair.D!),
			["Notifications:WebPush:KeyVersion"] = version,
			["Notifications:WebPush:AllowedOrigins:0"] = "https://push.example.test",
			[$"Notifications:Academic:Tenants:{tenantId:N}:TemplateTest:WebPush:UserId"] = recipientId.ToString()
		};
		var original = settings.Keys.ToDictionary(key => key, key => configuration[key]);
		foreach (var (key, value) in settings) configuration[key] = value;
		try
		{
			await SeedApproverAsync();
			var draft = await CreateTestSendDraftAsync(CommunicationChannelType.WebPush);
			Permissions(Client, $"{View},{Add}");
			await WithDatabaseAsync((db, _) =>
			{
				db.AddRange(Subscription(actorDevice, actorId, version), Subscription(oldDevice, recipientId, "old-version"));
				return Task.CompletedTask;
			});
			await ExpectRejectionAsync(await LegacyRequestAsync(), "WebPushSubscriptionUnavailable");
			await ExpectRejectionAsync(await DeviceRequestAsync(actorDevice), "WebPushSubscriptionUnavailable");
			await ExpectRejectionAsync(await DeviceRequestAsync(oldDevice), "WebPushSubscriptionUnavailable");
			await WithDatabaseAsync((db, _) =>
			{
				db.Add(Subscription(chosenDevice, recipientId, version));
				return Task.CompletedTask;
			});
			var legacy = await ReadQueuedAsync(await LegacyRequestAsync());
			draft = await GetAndReadAsync<NotificationTemplateDto>($"{BasePath}/templates/{draft.TemplateId}");
			await WithDatabaseAsync((db, _) =>
			{
				db.Add(Subscription(otherDevice, recipientId, version));
				return Task.CompletedTask;
			});
			await ExpectRejectionAsync(await LegacyRequestAsync(), "WebPushTestDeviceSelectionRequired");
			await ExpectRejectionAsync(await DeviceRequestAsync(Guid.Empty), "WebPushSubscriptionUnavailable");
			var selected = await ReadQueuedAsync(await DeviceRequestAsync(chosenDevice));
			draft = await GetAndReadAsync<NotificationTemplateDto>($"{BasePath}/templates/{draft.TemplateId}");

			await WithDatabaseAsync(async (db, services) =>
			{
				var readiness = services.GetRequiredService<NotificationDispatchReadiness>();
				var testDelivery = services.GetRequiredService<AcademicNotificationTemplateTestDelivery>();
				foreach (var queued in new[] { legacy, selected })
				{
					var row = await db.NotificationDispatches.SingleAsync(x => x.CampaignId == queued.OperationId);
					var intent = await db.NotificationIntents.SingleAsync(x => x.Id == row.IntentId);
					var now = DateTimeOffset.UtcNow;
					var ready = await readiness.CheckAsync(intent, row, now, default);
					ready.ExclusionCode.ShouldBeNull();
					var payload = ready.Payload.ShouldNotBeNull();
					payload.Recipient.UserId.ShouldBe(recipientId);
					payload.Destination.ShouldBe(chosenDevice.ToString("N"));
					foreach (var invalidDestination in new[] { null, "invalid", actorDevice.ToString("N") })
					{
						var rejected = await testDelivery.CheckAsync(intent, row, payload with { Destination = invalidDestination }, now, default);
						rejected.ExclusionCode.ShouldBe("WebPushSubscriptionUnavailable");
						rejected.Payload.ShouldBeNull();
					}
				}
			});

			await WithDatabaseAsync(async (db, _) =>
				(await db.Set<WebPushSubscription>().SingleAsync(x => x.Id == chosenDevice)).Disable());
			await ExpectRejectionAsync(await DeviceRequestAsync(chosenDevice), "WebPushSubscriptionUnavailable");
			await WithDatabaseAsync(async (db, services) =>
			{
				var row = await db.NotificationDispatches.SingleAsync(x => x.CampaignId == selected.OperationId);
				var intent = await db.NotificationIntents.SingleAsync(x => x.Id == row.IntentId);
				var readiness = await services.GetRequiredService<NotificationDispatchReadiness>().CheckAsync(intent, row,
					DateTimeOffset.UtcNow, default);
				readiness.ExclusionCode.ShouldBe("WebPushSubscriptionUnavailable");
				readiness.Payload.ShouldBeNull();
			});

			Task<HttpResponseMessage> LegacyRequestAsync() => Client.PostAsJsonAsync(TestSendRoute(draft),
				new { revisionId = draft.RevisionId, concurrencyStamp = draft.ConcurrencyStamp });
			Task<HttpResponseMessage> DeviceRequestAsync(Guid deviceId) => Client.PostAsJsonAsync(TestSendRoute(draft),
				new { revisionId = draft.RevisionId, concurrencyStamp = draft.ConcurrencyStamp, webPushSubscriptionId = deviceId });
		}
		finally
		{
			foreach (var (key, value) in original) configuration[key] = value;
			var fixtureIds = new[] { chosenDevice, otherDevice, actorDevice, oldDevice };
			await WithDatabaseAsync(async (db, _) => db.RemoveRange(await db.Set<WebPushSubscription>()
				.Where(x => Enumerable.Contains(fixtureIds, x.Id)).ToListAsync()));
		}

		WebPushSubscription Subscription(Guid id, Guid userId, string key) =>
			new(id, tenantId, userId, Guid.NewGuid(), id.ToString("N"), "fixture", key, DateTimeOffset.UtcNow);

		static async Task<AcademicNotificationTemplateTestSendDto> ReadQueuedAsync(HttpResponseMessage response)
		{
			using (response)
			{
				response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
				return (await response.Content.ReadFromJsonAsync<AcademicNotificationTemplateTestSendDto>(JsonOptions))!;
			}
		}

		static async Task ExpectRejectionAsync(HttpResponseMessage response, string code)
		{
			using (response)
			{
				response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
				var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
				problem.GetProperty("errorCode").GetString().ShouldBe(code);
			}
		}
	}

	private async Task<NotificationTemplateDto> CreateTestSendDraftAsync(CommunicationChannelType channel = CommunicationChannelType.InApp)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		Permissions(Client, Add);
		return await PostAndReadAsync<NotificationTemplateDto>(BasePath + "/templates",
			new CreateNotificationTemplateDto(AcademicNotificationEventKeys.HomeworkDue, "Guardian",
				channel, "en", "Homework test-send sample", "Homework reminder",
				channel == CommunicationChannelType.WebPush ? "A homework update is available." : "Hello {{recipient_name}}. Details: {{source_link}}"));
	}

	private static string TestSendRoute(NotificationTemplateDto draft) => $"{BasePath}/templates/{draft.TemplateId}/test-send";
}
