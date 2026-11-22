using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Communication.Notifications;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Communication;

public partial class WhatsAppWebhookApi_Tests
{
	[Fact]
	public async Task A_dispatch_transition_preserves_the_database_concurrency_precondition()
	{
		await SeedProviderAsync();
		var (dispatchId, _, leaseId) = await SeedDispatchAsync();

		await Should.ThrowAsync<AbpDbConcurrencyException>(() => WithDbAsync(async db => {
			var dispatch = await db.Set<NotificationDispatch>().SingleAsync(x => x.Id == dispatchId);
			await db.Set<NotificationDispatch>().Where(x => x.Id == dispatchId)
				.ExecuteUpdateAsync(set => set.SetProperty(x => x.ConcurrencyStamp, "another-worker"));
			dispatch.RecordOutcome(leaseId, DateTimeOffset.UtcNow, NotificationDispatchState.ProviderAccepted, "wamid.stale");
			await db.SaveChangesAsync();
		}));
	}

	[Fact]
	public async Task Receipt_received_before_send_result_is_reconciled_without_regressing_read_history()
	{
		await SeedProviderAsync();
		const string messageId = "wamid.early_callback";
		var (dispatchId, operationId, leaseId) = await SeedDispatchAsync();
		using var client = CreateAnonymousClient();
		using var early = await PostAsync(client, Body(messageId, status: "read"));
		early.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await ProcessReceiptsAsync()).ShouldBe(0);
		(await GetEventsAsync(messageId)).ShouldHaveSingleItem().ProcessedAt.ShouldBeNull();

		await WithDbAsync(async db => {
			var dispatch = await db.Set<NotificationDispatch>().SingleAsync(x => x.Id == dispatchId);
			dispatch.RecordOutcome(leaseId, DateTimeOffset.UtcNow, NotificationDispatchState.ProviderAccepted, messageId);
			await db.SaveChangesAsync();
		});
		(await ProcessReceiptsAsync()).ShouldBe(1);
		(await GetEventsAsync(messageId)).ShouldHaveSingleItem().ProcessedAt.ShouldNotBeNull();

		using var lateSent = await PostAsync(client, Body(messageId, status: "sent"));
		lateSent.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await ProcessReceiptsAsync()).ShouldBe(1);
		(await ProcessReceiptsAsync()).ShouldBe(0);
		await WithDbAsync(async db => {
			(await db.Set<NotificationDispatch>().SingleAsync(x => x.Id == dispatchId)).Status.ShouldBe(NotificationDispatchState.Read);
			var attempt = await db.Set<DeliveryAttempt>().SingleAsync(x => x.NotificationDispatchId == dispatchId);
			attempt.Status.ShouldBe(CommunicationDeliveryStatus.Read);
			attempt.ProviderMessageId.ShouldBe(messageId);
			attempt.ReadAt.ShouldNotBeNull();
			attempt.DeliveredAt.ShouldNotBeNull();
			var operation = await db.Set<CommunicationCampaign>().SingleAsync(x => x.Id == operationId);
			operation.DeliveredCount.ShouldBe(1);
		});
	}

	private async Task<int> ProcessReceiptsAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var school = services.GetRequiredService<ICurrentTenant>().Change(WhatsAppWebhookApiFactory.TenantId);
		var processed = await services.GetRequiredService<WhatsAppProviderEventProcessor>().ProcessBatchAsync(10, default);
		await services.GetRequiredService<NotificationOutcomeProjector>().RefreshBatchAsync(10, default);
		return processed;
	}

	private async Task<(Guid DispatchId, Guid OperationId, Guid LeaseId)> SeedDispatchAsync()
	{
		var tenant = WhatsAppWebhookApiFactory.TenantId;
		var provider = WhatsAppWebhookApiFactory.ProviderId;
		var now = DateTimeOffset.UtcNow;
		const string eventKey = "Academic.Homework.Published";
		var template = new CommunicationTemplate(Guid.NewGuid(), tenant) { TemplateName = "Homework", Body = "Template" };
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), tenant, template.Id, 1, eventKey, "Guardian",
			CommunicationChannelType.WhatsApp, "en-US", "", "Homework for {{student_name}}. View {{source_link}}.", 1);
		revision.Publish(Guid.NewGuid(), now);
		var binding = new WhatsAppTemplateBinding(Guid.NewGuid(), tenant, revision.Id, provider, "academic_homework", "en_US",
			NotificationRuntimeJson.Serialize(new WhatsAppBoundParameters("1001", "2001", "fixture-mapping"), NotificationErrorCodes.TemplateInvalid));
		var rule = new NotificationRule(Guid.NewGuid(), tenant, eventKey, NotificationRuleScope.Create(yearId: Guid.NewGuid()));
		var campaign = new CommunicationCampaign(Guid.NewGuid(), tenant) {
			CampaignType = NotificationCampaignTypes.Academic, Status = CommunicationCampaignStatus.Sent,
			SourceEntityId = Guid.NewGuid(), SourceEntityType = "Academic.Homework"
		};
		var recipient = new CommunicationRecipientSnapshot(Guid.NewGuid(), tenant) {
			CampaignId = campaign.Id, RecipientType = "Guardian", DisplayName = "Test guardian"
		};
		var intent = new NotificationIntent(Guid.NewGuid(), tenant, rule.Id, rule.Revision, eventKey, campaign.SourceEntityType,
			campaign.SourceEntityId.Value, "revision", Guid.NewGuid().ToString("N"), now, now, "[]", "[]");
		intent.BindRuntimeSnapshot("""{"schemaVersion":1,"channels":[2],"fallbackLanguage":"en"}""", NotificationIntentOrigin.Automatic);
		intent.BindAutomaticOperation(campaign.Id);
		var intentLease = Guid.NewGuid();
		Set(intent, nameof(NotificationIntent.Status), NotificationIntentState.Processing);
		Set(intent, nameof(NotificationIntent.LeaseId), intentLease);
		Set(intent, nameof(NotificationIntent.LeaseExpiresAt), now.AddMinutes(5));
		intent.CompletePlanning(intentLease, now);
		intent.CompleteDelivery(NotificationIntentState.Completed);

		var dispatch = new NotificationDispatch(Guid.NewGuid(), tenant, intent.Id, revision.Id, "{}", "context",
			CommunicationChannelType.WhatsApp, "fingerprint", "protected", "hash", Guid.NewGuid().ToString("N"), now);
		dispatch.BindPlanningEvidence(campaign.Id, recipient.Id, provider);
		var attempt = new DeliveryAttempt(Guid.NewGuid(), tenant) {
			CampaignId = campaign.Id, RecipientSnapshotId = recipient.Id, NotificationProviderSettingId = provider,
			NotificationDispatchId = dispatch.Id, Channel = CommunicationChannelType.WhatsApp, Destination = "Hidden", QueuedAt = now
		};
		var lease = Guid.NewGuid();
		Set(dispatch, nameof(NotificationDispatch.Status), NotificationDispatchState.Processing);
		Set(dispatch, nameof(NotificationDispatch.LeaseId), lease);
		Set(dispatch, nameof(NotificationDispatch.LeaseExpiresAt), now.AddMinutes(5));
		Set(dispatch, nameof(NotificationDispatch.AttemptNumber), 1);
		dispatch.BeginIo(lease, now, attempt.Id);
		await WithDbAsync(async db => {
			db.AddRange(template, revision, binding, rule, campaign, recipient, intent, dispatch, attempt);
			await db.SaveChangesAsync();
		});
		return (dispatch.Id, campaign.Id, lease);
	}

	private static void Set<T>(T row, string property, object value) => typeof(T).GetProperty(property)!.SetValue(row, value);
}
