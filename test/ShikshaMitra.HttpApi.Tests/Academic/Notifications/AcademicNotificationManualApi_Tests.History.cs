using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public partial class AcademicNotificationManualApi_Tests
{
	[Fact]
	public async Task Staff_history_pages_attempts_and_pre_dispatch_exclusions_without_revealing_contact_data()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await PublishGuardianTemplateAsync();
		factory.ActiveSourceId = Guid.NewGuid();
		try
		{
			Permissions(Client, $"{View},{Send}");
			var review = await ReviewAsync(Request(sourceId: factory.ActiveSourceId));
			var operation = await ConfirmAsync(Client, new { reviewToken = review.Id, idempotencyKey = $"history-{review.Id:N}" },
				CommunicationCampaignStatus.Queued);
			var recipientId = AcademicNotificationPreviewApiFactory.StudentId;
			await SeedHistoryAsync(operation.OperationId, recipientId);

			factory.DenySourceSend = true;
			Permissions(Client, View);
			var detail = await GetAndReadAsync<JsonElement>($"{BasePath}/operations/{operation.OperationId}");
			detail.GetProperty("eventKey").GetString().ShouldBe(Event);
			detail.GetProperty("excludedBeforeDispatch").GetInt32().ShouldBe(1);
			detail.GetProperty("deliveries")[0].GetProperty("status").GetInt32().ShouldBe((int)NotificationDispatchState.ProviderAccepted);
			var recent = await GetAndReadAsync<JsonElement>($"{BasePath}/history/recent?eventKey={Event}");
			var recentItem = recent.GetProperty("items").EnumerateArray()
				.Single(row => row.GetProperty("operationId").GetGuid() == operation.OperationId);
			recentItem.GetProperty("canViewSummary").GetBoolean().ShouldBeTrue();

			var path = $"{BasePath}/history?eventKey={Event}&sourceType={SourceType}&sourceId={factory.ActiveSourceId}&operationId={operation.OperationId}";
			using var accepted = await Client.GetAsync(path + "&state=ProviderAccepted&maxResultCount=1");
			accepted.StatusCode.ShouldBe(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());
			accepted.Headers.CacheControl?.NoStore.ShouldBe(true);
			var acceptedPage = await accepted.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
			acceptedPage.GetProperty("totalCount").GetInt64().ShouldBe(1);
			var item = acceptedPage.GetProperty("items")[0];
			item.GetProperty("attemptId").GetGuid().ShouldNotBe(Guid.Empty);
			item.GetProperty("recipientId").GetGuid().ShouldBe(recipientId);
			item.GetProperty("acceptedAt").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddHours(-1));
			item.GetProperty("deliveredAt").ValueKind.ShouldBe(JsonValueKind.Null);
			item.GetProperty("reasonCode").GetString().ShouldBe("DeliveryDetailUnavailable");
			var raw = await accepted.Content.ReadAsStringAsync();
			raw.ShouldNotContain("private@example.invalid");
			raw.ShouldNotContain("private-provider-reference");
			raw.ShouldNotContain("private-internal-error");
			raw.ShouldNotContain("Private source text");

			var excluded = await GetAndReadAsync<JsonElement>(path + "&state=Skipped");
			excluded.GetProperty("totalCount").GetInt64().ShouldBe(1);
			excluded.GetProperty("items")[0].GetProperty("reasonCode").GetString().ShouldBe("ConsentRequired");
			excluded.GetProperty("items")[0].GetProperty("channel").GetInt32().ShouldBe((int)CommunicationChannelType.InApp);
			var secondPage = await GetAndReadAsync<JsonElement>(path + "&skipCount=1&maxResultCount=1");
			secondPage.GetProperty("totalCount").GetInt64().ShouldBe(2);
			secondPage.GetProperty("items").GetArrayLength().ShouldBe(1);

			await SeedRecentOperationsAsync(operation.OperationId);
			var recentPath = $"{BasePath}/history/recent?eventKey={Event}";
			var firstRecent = await GetAndReadAsync<AcademicNotificationRecentDeliveriesDto>(recentPath);
			firstRecent.Items.Count.ShouldBe(15);
			firstRecent.NextCursor.ShouldNotBeNullOrEmpty();
			var nextRecent = await GetAndReadAsync<AcademicNotificationRecentDeliveriesDto>(
				recentPath + "&cursor=" + Uri.EscapeDataString(firstRecent.NextCursor!));
			nextRecent.Items.Count.ShouldBe(15);
			nextRecent.Items.Select(x => x.OperationId).Intersect(firstRecent.Items.Select(x => x.OperationId)).ShouldBeEmpty();

			factory.DenySourceView = true;
			var inaccessible = await GetAndReadAsync<JsonElement>($"{BasePath}/history/recent?eventKey={Event}");
			inaccessible.GetProperty("items").GetArrayLength().ShouldBe(0);
			inaccessible.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
			using var deniedDetail = await Client.GetAsync($"{BasePath}/operations/{operation.OperationId}");
			deniedDetail.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			using var deniedHistory = await Client.GetAsync(path);
			deniedHistory.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		}
		finally
		{
			factory.ActiveSourceId = AcademicNotificationPreviewApiFactory.SourceId;
			factory.DenySourceView = false;
			factory.DenySourceSend = false;
		}
	}

	[Fact]
	public async Task Send_and_add_without_view_cannot_read_operation_status()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		Permissions(Client, $"{Send},{Add}");
		using var denied = await Client.GetAsync($"{BasePath}/operations/{Guid.NewGuid()}");
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task A_division_viewer_cannot_read_other_intents_or_full_operation_counts()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await PublishGuardianTemplateAsync();
		factory.ActiveSourceId = Guid.NewGuid();
		try
		{
			Permissions(Client, $"{View},{Send}");
			var receipt = await ReviewAsync(Request(sourceId: factory.ActiveSourceId));
			var operation = await ConfirmAsync(Client, new { reviewToken = receipt.Id, idempotencyKey = $"scope-history-{receipt.Id:N}" },
				CommunicationCampaignStatus.Queued);
			await SeedHistoryAsync(operation.OperationId, AcademicNotificationPreviewApiFactory.StudentId);

			var viewerId = Guid.NewGuid();
			await WithDatabaseAsync(async (db, _) =>
			{
				var tenant = AcademicNotificationPreviewApiFactory.TenantId;
				var first = await db.NotificationIntents.SingleAsync(x => x.CampaignId == operation.OperationId);
				var originalDispatch = await db.NotificationDispatches.SingleAsync(x => x.IntentId == first.Id);
				var otherScope = JsonNode.Parse(first.RuleConfigurationJson)!.AsObject();
				otherScope["sourceScope"] = JsonSerializer.SerializeToNode(new NotificationSourceScopeDto(), JsonOptions);
				var other = NotificationIntent.CreateReviewedManual(Guid.NewGuid(), tenant, null, 0, first.EventKey, first.SourceType,
					first.SourceId, first.SourceRevision, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, first.AffectedRecipientsJson,
					first.TemplateRevisionIdsJson, first.ExpiresAt, first.ActorUserId!.Value, operation.OperationId, otherScope.ToJsonString());
				var label = new CommunicationRecipientSnapshot(Guid.NewGuid(), tenant)
				{
					CampaignId = operation.OperationId, RecipientType = "Guardian", RecipientId = Guid.NewGuid()
				};
				var hidden = new NotificationDispatch(Guid.NewGuid(), tenant, other.Id, originalDispatch.TemplateRevisionId,
					originalDispatch.RecipientReferenceJson, "second-scope", CommunicationChannelType.InApp,
					"second-scope", "unused", "second-scope", Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
				hidden.BindPlanningEvidence(operation.OperationId, label.Id, null);
				db.AddRange(other, label, hidden);
				db.Add(new IdentityUser(viewerId, $"scope-{viewerId:N}", "division-viewer@example.invalid", tenant));
				db.Add(new UserScopeAssignment(Guid.NewGuid(), tenant, userId: viewerId, academicYearId: Factory.SeededAcademicYearId,
					educationBoardId: Factory.SeededBoardId, educationMediumId: Factory.SeededMediumId,
					educationMediumStageId: Factory.SeededMediumStageId, standardId: Factory.SeededStandardId, divisionId: Factory.SeededDivisionId));
				await db.SaveChangesAsync();
			});
			using var viewer = CreateClientAs(viewerId);
			viewer.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Staff");
			Permissions(viewer, View);
			using var detail = await viewer.GetAsync($"{BasePath}/operations/{operation.OperationId}");
			detail.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await detail.Content.ReadAsStringAsync());
			using var recent = await viewer.GetAsync($"{BasePath}/history/recent?eventKey={Event}");
			recent.StatusCode.ShouldBe(HttpStatusCode.OK, await recent.Content.ReadAsStringAsync());
			var recentPage = await recent.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
			var recentItem = recentPage.GetProperty("items").EnumerateArray()
				.Single(row => row.GetProperty("operationId").GetGuid() == operation.OperationId);
			recentItem.GetProperty("canViewSummary").GetBoolean().ShouldBeFalse();
			using var result = await viewer.GetAsync($"{BasePath}/history?eventKey={Event}&sourceType={SourceType}&sourceId={factory.ActiveSourceId}");
			result.StatusCode.ShouldBe(HttpStatusCode.OK, await result.Content.ReadAsStringAsync());
			var page = await result.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
			page.GetProperty("totalCount").GetInt64().ShouldBe(1);
			page.GetProperty("items")[0].GetProperty("status").GetInt32().ShouldBe((int)NotificationDispatchState.ProviderAccepted);
		}
		finally { factory.ActiveSourceId = AcademicNotificationPreviewApiFactory.SourceId; }
	}

	private Task SeedRecentOperationsAsync(Guid operationId) => WithDatabaseAsync(async (db, _) =>
	{
		var original = await db.NotificationIntents.SingleAsync(x => x.CampaignId == operationId);
		var originalCampaign = await db.CommunicationCampaigns.SingleAsync(x => x.Id == operationId);
		var tenant = AcademicNotificationPreviewApiFactory.TenantId;
		for (var i = 0; i < 41; i++)
		{
			var campaign = new CommunicationCampaign(Guid.NewGuid(), tenant) {
				CampaignType = NotificationCampaignTypes.Academic, SourceModule = CommunicationSourceModule.Academic,
				SourceEntityType = original.SourceType, SourceEntityId = original.SourceId,
				Title = "Recent notification fixture", Status = CommunicationCampaignStatus.Queued,
				CreationTime = originalCampaign.CreationTime.AddDays(-1)
			};
			var copy = NotificationIntent.CreateReviewedManual(Guid.NewGuid(), tenant, null, 0, original.EventKey,
				original.SourceType, original.SourceId, original.SourceRevision, Guid.NewGuid().ToString("N"),
				DateTimeOffset.UtcNow, original.AffectedRecipientsJson, original.TemplateRevisionIdsJson,
				original.ExpiresAt, original.ActorUserId!.Value, campaign.Id, original.RuleConfigurationJson);
			db.AddRange(campaign, copy);
		}
		await db.SaveChangesAsync();
	});

	private Task SeedHistoryAsync(Guid operationId, Guid recipientId) => WithDatabaseAsync(async (db, services) =>
	{
		var tenant = AcademicNotificationPreviewApiFactory.TenantId;
		var intent = await db.NotificationIntents.SingleAsync(x => x.CampaignId == operationId);
		await PlanAsync(db, services, intent);
		var dispatch = await db.NotificationDispatches.SingleAsync(x => x.IntentId == intent.Id);
		var now = DateTimeOffset.UtcNow;
		var lease = Guid.NewGuid();
		var record = db.Entry(dispatch);
		record.Property(x => x.Status).CurrentValue = NotificationDispatchState.Processing;
		record.Property(x => x.LeaseId).CurrentValue = lease;
		record.Property(x => x.LeaseExpiresAt).CurrentValue = now.AddMinutes(5);
		record.Property(x => x.AttemptNumber).CurrentValue = 1;
		var attempt = new DeliveryAttempt(Guid.NewGuid(), tenant)
		{
			CampaignId = operationId, RecipientSnapshotId = dispatch.RecipientSnapshotId, NotificationDispatchId = dispatch.Id,
			Channel = dispatch.Channel, AttemptNumber = 1, QueuedAt = now, SentAt = now, Status = CommunicationDeliveryStatus.Sent,
			Destination = "private@example.invalid", ProviderMessageId = "private-provider-reference", ErrorCode = "private-internal-error"
		};
		dispatch.BeginIo(lease, now, attempt.Id);
		dispatch.RecordOutcome(lease, now, NotificationDispatchState.ProviderAccepted, "private-provider-reference");
		db.Add(attempt);

		var source = new NotificationRecipientRefDto("Student", recipientId);
		var blocked = new NotificationRecipientRefDto("StudentAccount", recipientId, "self");
		db.Add(new CommunicationRecipientSnapshot(Guid.NewGuid(), tenant)
		{
			CampaignId = operationId, RecipientType = "StudentAccount", RecipientId = recipientId, IsEligible = false,
			DisplayName = "Private sample name", Email = "private@example.invalid", BlockedReason = "ConsentRequired",
			ChannelEligibilityJson = JsonSerializer.Serialize(new { schemaVersion = 1, intentId = intent.Id,
				sourceRecipient = source, recipient = blocked, channel = CommunicationChannelType.InApp,
				exclusionCode = "ConsentRequired" }, JsonOptions)
		});
		await db.SaveChangesAsync();
	});
}
