using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.AuditLogging;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public partial class AcademicNotificationManualApi_Tests
{
	[Fact]
	public async Task Retry_preserves_attempt_history_limits_the_retry_to_the_failed_channel_and_records_metadata()
	{
		await PrepareDeliveryActionsAsync();
		factory.ActiveSourceId = Guid.NewGuid();
		try
		{
			var operation = await ConfirmEmailAndInboxAsync();
			Guid retried = Guid.Empty;
			var heldUntil = DateTimeOffset.UtcNow.AddMinutes(20);
			await WithDatabaseAsync(async (db, services) =>
			{
				var intent = await db.NotificationIntents.SingleAsync(x => x.CampaignId == operation.OperationId);
				await PlanAsync(db, services, intent);
				var rows = await db.NotificationDispatches.Where(x => x.IntentId == intent.Id).ToListAsync();
				var email = rows.Single(x => x.Channel == CommunicationChannelType.Email);
				retried = email.Id;
				SeedOutcome(db, email, NotificationDispatchState.Failed, "SmtpTemporaryRejection",
					NotificationDispatch.MaximumAutomaticAttempts);
				email.PreserveRetryWindow(heldUntil);
				SeedOutcome(db, rows.Single(x => x.Channel == CommunicationChannelType.InApp), NotificationDispatchState.Delivered);
				intent.CompleteDelivery(NotificationIntentState.PartiallyFailed, "DeliveryFailed");
				await db.SaveChangesAsync();
			});

			factory.DenySourceSend = true;
			var route = $"{BasePath}/dispatches/{retried}/retry";
			var history = $"{BasePath}/history?eventKey={Event}&sourceType={SourceType}&sourceId={factory.ActiveSourceId}&operationId={operation.OperationId}";
			Permissions(Client, View);
			var readOnly = await GetAndReadAsync<JsonElement>(history);
			readOnly.GetProperty("items").EnumerateArray().All(x => !x.GetProperty("canRequestRetry").GetBoolean()).ShouldBeTrue();
			using var denied = await Client.PostAsync(route, null);
			denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

			Permissions(Client, $"{View},{Add}");
			var result = await GetAndReadAsync<JsonElement>(history);
			var candidate = result.GetProperty("items").EnumerateArray()
				.Where(x => x.GetProperty("canRequestRetry").GetBoolean()).ShouldHaveSingleItem();
			candidate.GetProperty("dispatchId").GetGuid().ShouldBe(retried);
			var attemptId = candidate.GetProperty("attemptId").GetGuid();
			using var stale = await Client.PostAsync($"{route}?expectedAttemptId={Guid.NewGuid()}", null);
			await AssertConflictAsync(stale, NotificationErrorCodes.RetryRequiresReview);
			var command = $"{route}?expectedAttemptId={attemptId}";
			var queued = await PostAndReadAsync<AcademicNotificationRetryDto>(command, new { });
			queued.Status.ShouldBe(NotificationDispatchState.Pending);
			queued.NextAttemptAt.ShouldBeGreaterThanOrEqualTo(heldUntil);
			var replay = await PostAndReadAsync<AcademicNotificationRetryDto>(command, new { });
			replay.ShouldBe(queued);
			var refreshed = await GetAndReadAsync<JsonElement>(history);
			refreshed.GetProperty("items").EnumerateArray().All(x => !x.GetProperty("canRequestRetry").GetBoolean()).ShouldBeTrue();

			await WithDatabaseAsync(async (db, _) =>
			{
				var email = await db.NotificationDispatches.SingleAsync(x => x.Id == retried);
				email.AttemptNumber.ShouldBe(NotificationDispatch.MaximumAutomaticAttempts);
				email.IoStartedAt.ShouldBeNull();
				(await db.NotificationDispatches.SingleAsync(x => x.CampaignId == operation.OperationId &&
					x.Channel == CommunicationChannelType.InApp)).Status.ShouldBe(NotificationDispatchState.Delivered);
				(await db.Set<DeliveryAttempt>().SingleAsync(x => x.NotificationDispatchId == retried)).Status.ShouldBe(CommunicationDeliveryStatus.Failed);
				var logs = await db.Set<AuditLog>().AsNoTracking().Include(x => x.Actions).Where(x => x.TenantId == AcademicNotificationPreviewApiFactory.TenantId &&
					x.UserId == Guid.Parse(TestAuthHandler.TestUserId)).ToListAsync();
				var action = logs.SelectMany(x => x.Actions).Single(x => x.MethodName == "RetryDispatch" &&
					x.Parameters.Contains(retried.ToString()));
				action.Parameters.ShouldContain("SmtpTemporaryRejection");
				action.Parameters.ShouldNotContain("private@example.invalid");
				action.Parameters.ShouldNotContain("Private source text");
			});
		}
		finally
		{
			factory.ActiveSourceId = AcademicNotificationPreviewApiFactory.SourceId;
			factory.DenySourceSend = false;
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Cancellation_only_stops_unclaimed_work_and_preserves_accepted_and_ambiguous_attempts(bool inFlight)
	{
		await PrepareDeliveryActionsAsync();
		factory.ActiveSourceId = Guid.NewGuid();
		try
		{
			var operation = await ConfirmEmailAndInboxAsync();
			Guid inboxId = Guid.Empty;
			Guid emailId = Guid.Empty;
			await WithDatabaseAsync(async (db, services) =>
			{
				var intent = await db.NotificationIntents.SingleAsync(x => x.CampaignId == operation.OperationId);
				await PlanAsync(db, services, intent);
				var rows = await db.NotificationDispatches.Where(x => x.IntentId == intent.Id).ToListAsync();
				var email = rows.Single(x => x.Channel == CommunicationChannelType.Email);
				var inbox = rows.Single(x => x.Channel == CommunicationChannelType.InApp);
				inboxId = inbox.Id;
				emailId = email.Id;
				SeedOutcome(db, inbox, NotificationDispatchState.Delivered);
				if (inFlight) StartAttempt(db, email);
				await db.SaveChangesAsync();
			});

			Permissions(Client, Add);
			using var deliveredRetry = await Client.PostAsync($"{BasePath}/dispatches/{inboxId}/retry", null);
			await AssertConflictAsync(deliveredRetry, NotificationErrorCodes.RetryRequiresReview);
			var route = $"{BasePath}/operations/{operation.OperationId}/cancel";
			var cancelled = await PostAndReadAsync<AcademicNotificationCancellationDto>(route, new { });
			cancelled.CancelledDispatches.ShouldBe(inFlight ? 0 : 1);
			cancelled.CancelledIntents.ShouldBe(0);
			cancelled.RemainingInFlight.ShouldBe(inFlight ? 1 : 0);
			(await PostAndReadAsync<AcademicNotificationCancellationDto>(route, new { })).CancelledDispatches.ShouldBe(0);

			await WithDatabaseAsync(async (db, _) =>
			{
				var inbox = await db.NotificationDispatches.SingleAsync(x => x.Id == inboxId);
				var email = await db.NotificationDispatches.SingleAsync(x => x.Id == emailId);
				inbox.Status.ShouldBe(NotificationDispatchState.Delivered);
				inbox.AcceptedAt.ShouldNotBeNull();
				email.Status.ShouldBe(inFlight ? NotificationDispatchState.Processing : NotificationDispatchState.Skipped);
				if (inFlight)
				{
					email.RecordOutcome(email.IoLeaseId!.Value, DateTimeOffset.UtcNow, NotificationDispatchState.UnknownAcceptance,
						errorCode: NotificationErrorCodes.UnknownAcceptance);
					await db.SaveChangesAsync();
				}
				else email.ErrorCode.ShouldBe(NotificationErrorCodes.CancelledBeforeDelivery);
			});
			if (inFlight)
			{
				using var uncertain = await Client.PostAsync($"{BasePath}/dispatches/{emailId}/retry", null);
				await AssertConflictAsync(uncertain, NotificationErrorCodes.RetryRequiresReview);
			}
		}
		finally { factory.ActiveSourceId = AcademicNotificationPreviewApiFactory.SourceId; }
	}

	[Fact]
	public async Task Cancel_before_planning_requires_add_and_current_source_access()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await PublishGuardianTemplateAsync();
		factory.ActiveSourceId = Guid.NewGuid();
		try
		{
			Permissions(Client, $"{View},{Send}");
			var review = await ReviewAsync(Request(sourceId: factory.ActiveSourceId));
			var queued = await ConfirmAsync(Client, new { reviewToken = review.Id, idempotencyKey = $"cancel-ready-{review.Id:N}" },
				CommunicationCampaignStatus.Queued);
			var path = $"{BasePath}/operations/{queued.OperationId}/cancel";
			using var noAdd = await Client.PostAsync(path, null);
			noAdd.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			Permissions(Client, Add);
			factory.DenySourceView = true;
			using var noScope = await Client.PostAsync(path, null);
			noScope.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			factory.DenySourceView = false;
			var result = await PostAndReadAsync<AcademicNotificationCancellationDto>(path, new { });
			result.CancelledIntents.ShouldBe(1);
			result.CancelledDispatches.ShouldBe(0);
			result.Status.ShouldBe(CommunicationCampaignStatus.Cancelled);
		}
		finally
		{
			factory.ActiveSourceId = AcademicNotificationPreviewApiFactory.SourceId;
			factory.DenySourceView = false;
		}
	}

	private async Task PrepareDeliveryActionsAsync()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await PublishGuardianTemplateAsync();
		await PublishGuardianTemplateAsync(CommunicationChannelType.Email);
		await WithDatabaseAsync(async (db, _) =>
		{
			var tenant = AcademicNotificationPreviewApiFactory.TenantId;
			if (await db.Set<ChannelProviderSetting>().AnyAsync(x => x.TenantId == tenant && x.ProviderName == "SMTP" && x.IsDefault)) return;
			db.Add(new ChannelProviderSetting(Guid.NewGuid(), tenant)
			{
				Channel = CommunicationChannelType.Email, ProviderName = "SMTP", IsEnabled = true, IsDefault = true,
				ConsentRequired = false, Status = CommunicationProviderStatus.Enabled
			});
			await db.SaveChangesAsync();
		});
	}

	private async Task<AcademicNotificationOperationDto> ConfirmEmailAndInboxAsync()
	{
		Permissions(Client, $"{View},{Send}");
		var review = await ReviewAsync(new
		{
			eventKey = Event, sourceType = SourceType, sourceId = factory.ActiveSourceId,
			channels = new[] { CommunicationChannelType.InApp, CommunicationChannelType.Email },
			selectedRecipients = new[] { new NotificationRecipientRefDto("Student", AcademicNotificationPreviewApiFactory.StudentId) }
		});
		return await ConfirmAsync(Client, new { reviewToken = review.Id, idempotencyKey = $"control-{review.Id:N}" },
			CommunicationCampaignStatus.Queued);
	}

	private static void SeedOutcome(ShikshaMitraDbContext db, NotificationDispatch row, NotificationDispatchState outcome, string? reason = null,
		int attemptNumber = 1)
	{
		var attempt = StartAttempt(db, row, attemptNumber);
		var now = DateTimeOffset.UtcNow;
		row.RecordOutcome(row.IoLeaseId!.Value, now, outcome, errorCode: reason);
		attempt.Status = outcome == NotificationDispatchState.Delivered ? CommunicationDeliveryStatus.Delivered : CommunicationDeliveryStatus.Failed;
		attempt.ErrorCode = reason;
		attempt.SentAt = row.AcceptedAt;
		attempt.DeliveredAt = outcome == NotificationDispatchState.Delivered ? now : null;
	}

	private static DeliveryAttempt StartAttempt(ShikshaMitraDbContext db, NotificationDispatch row, int number = 1)
	{
		var now = DateTimeOffset.UtcNow;
		var lease = Guid.NewGuid();
		var entry = db.Entry(row);
		entry.Property(x => x.Status).CurrentValue = NotificationDispatchState.Processing;
		entry.Property(x => x.AttemptNumber).CurrentValue = number;
		entry.Property(x => x.LeaseId).CurrentValue = lease;
		entry.Property(x => x.LeaseExpiresAt).CurrentValue = now.AddMinutes(5);
		var attempt = new DeliveryAttempt(Guid.NewGuid(), row.TenantId)
		{
			CampaignId = row.CampaignId!.Value, RecipientSnapshotId = row.RecipientSnapshotId, NotificationDispatchId = row.Id,
			Channel = row.Channel, AttemptNumber = number, QueuedAt = now, Destination = "Hidden"
		};
		row.BeginIo(lease, now, attempt.Id);
		db.Add(attempt);
		return attempt;
	}
}
