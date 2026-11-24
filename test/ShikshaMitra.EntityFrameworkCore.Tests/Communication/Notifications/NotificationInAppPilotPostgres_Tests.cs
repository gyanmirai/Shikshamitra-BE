using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Communication.Notifications;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationInAppPilotPostgres_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	[NotificationPostgresFact]
	public async Task External_pilots_require_the_exact_channel_and_leave_the_other_channel_unclaimed()
	{
		var school = Guid.NewGuid();
		var recipient = Guid.NewGuid();
		var email = new NotificationTemplateTestPilot(school, Guid.NewGuid(), recipient, CommunicationChannelType.Email);
		var push = email with { OperationId = Guid.NewGuid(), Channel = CommunicationChannelType.WebPush };
		var missingRoute = email with { OperationId = Guid.NewGuid() };
		var now = DateTimeOffset.UtcNow;
		Guid emailId;
		Guid pushId;
		await using (var seed = fixture.CreateContext())
		{
			emailId = Seed(seed, email, now, CommunicationChannelType.Email, providerSettingId: Guid.NewGuid());
			pushId = Seed(seed, push, now, CommunicationChannelType.WebPush);
			Seed(seed, missingRoute, now, CommunicationChannelType.Email);
			await seed.SaveChangesAsync();
		}

		await using var connection = fixture.CreateConnection();
		await connection.OpenAsync();
		Task<System.Collections.Generic.IReadOnlyList<NotificationQueueClaim>> ClaimAsync(NotificationTemplateTestPilot selection) =>
			NotificationPostgresQueueClaims.ClaimAsync(connection, null, school, NotificationQueueKind.Dispatch,
				now, 1, TimeSpan.FromMinutes(5), default, pilot: selection);
		(await ClaimAsync(email with { Channel = CommunicationChannelType.WebPush })).ShouldBeEmpty();
		(await ClaimAsync(missingRoute)).ShouldBeEmpty();
		(await ClaimAsync(email)).ShouldHaveSingleItem().Id.ShouldBe(emailId);
		(await ClaimAsync(email)).ShouldBeEmpty();
		(await ClaimAsync(push)).ShouldHaveSingleItem().Id.ShouldBe(pushId);
		(await ClaimAsync(push)).ShouldBeEmpty();
	}

	[NotificationPostgresFact]
	public async Task Pilot_claims_only_the_selected_synthetic_in_app_dispatch_once()
	{
		var school = Guid.NewGuid();
		var otherSchool = Guid.NewGuid();
		var recipient = Guid.NewGuid();
		var selected = new NotificationTemplateTestPilot(school, Guid.NewGuid(), recipient);
		var email = selected with { OperationId = Guid.NewGuid() };
		var ordinary = selected with { OperationId = Guid.NewGuid() };
		var duplicate = selected with { OperationId = Guid.NewGuid() };
		var expired = selected with { OperationId = Guid.NewGuid() };
		var now = DateTimeOffset.UtcNow;
		Guid selectedDispatch;
		await using (var seed = fixture.CreateContext())
		{
			selectedDispatch = Seed(seed, selected, now);
			Seed(seed, selected with { TenantId = otherSchool }, now);
			Seed(seed, selected with { OperationId = Guid.NewGuid() }, now);
			Seed(seed, email, now, channel: CommunicationChannelType.Email);
			Seed(seed, ordinary, now, synthetic: false);
			Seed(seed, duplicate, now);
			Seed(seed, duplicate, now);
			Seed(seed, expired, now.AddMinutes(-11));
			await seed.SaveChangesAsync();
		}

		await using var connection = fixture.CreateConnection();
		await connection.OpenAsync();
		foreach (var blocked in new[] { selected with { RecipientUserId = Guid.NewGuid() }, email, ordinary, duplicate, expired })
			(await NotificationPostgresQueueClaims.ClaimAsync(connection, null, school, NotificationQueueKind.Dispatch,
				now, 1, TimeSpan.FromMinutes(5), default, pilot: blocked)).ShouldBeEmpty();

		await using var secondConnection = fixture.CreateConnection();
		await secondConnection.OpenAsync();
		var claims = await Task.WhenAll(
			NotificationPostgresQueueClaims.ClaimAsync(connection, null, school, NotificationQueueKind.Dispatch,
				now, 1, TimeSpan.FromMinutes(5), default, pilot: selected),
			NotificationPostgresQueueClaims.ClaimAsync(secondConnection, null, school, NotificationQueueKind.Dispatch,
				now, 1, TimeSpan.FromMinutes(5), default, pilot: selected));
		claims.SelectMany(x => x).ShouldHaveSingleItem().Id.ShouldBe(selectedDispatch);

		// A pending row with a retained attempt must never be claimed by the pilot again.
		await using var verify = fixture.CreateContext();
		var selectedRow = await verify.Set<NotificationDispatch>().SingleAsync(x => x.Id == selectedDispatch);
		selectedRow.Status.ShouldBe(NotificationDispatchState.Processing);
		selectedRow.AttemptNumber.ShouldBe(1);
		await verify.Set<NotificationDispatch>().Where(x => x.Id == selectedDispatch)
			.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, NotificationDispatchState.Pending));
		(await NotificationPostgresQueueClaims.ClaimAsync(connection, null, school, NotificationQueueKind.Dispatch,
			now, 1, TimeSpan.FromMinutes(5), default, pilot: selected)).ShouldBeEmpty();
		(await verify.Set<NotificationDispatch>().CountAsync(x =>
			(x.TenantId == school || x.TenantId == otherSchool) && x.AttemptNumber != 0)).ShouldBe(1);
	}

	[NotificationPostgresFact]
	public async Task A_regular_pilot_claims_only_its_single_regular_intent_and_in_app_dispatch()
	{
		var pilot = new StagingInAppNotificationPilot(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		var otherOperation = pilot with { OperationId = Guid.NewGuid() };
		var templateTest = pilot with { OperationId = Guid.NewGuid() };
		var withSiblings = pilot with { OperationId = Guid.NewGuid() };
		var email = pilot with { OperationId = Guid.NewGuid() };
		var now = DateTimeOffset.UtcNow;
		Guid selectedDispatch;
		await using (var seed = fixture.CreateContext())
		{
			selectedDispatch = Seed(seed, pilot, now, synthetic: false);
			Seed(seed, pilot with { TenantId = Guid.NewGuid() }, now, synthetic: false);
			Seed(seed, otherOperation, now, synthetic: false);
			Seed(seed, templateTest, now);
			Seed(seed, withSiblings, now, synthetic: false);
			Seed(seed, withSiblings, now, synthetic: false);
			Seed(seed, email, now, CommunicationChannelType.Email, synthetic: false);
			await seed.SaveChangesAsync();
		}

		await using var connection = fixture.CreateConnection();
		await connection.OpenAsync();
		Task<System.Collections.Generic.IReadOnlyList<NotificationQueueClaim>> ClaimAsync(NotificationQueueKind kind,
			StagingInAppNotificationPilot selection, DateTimeOffset? claimTime = null) =>
			NotificationPostgresQueueClaims.ClaimAsync(connection, null, pilot.TenantId,
				kind, claimTime ?? now, 1, TimeSpan.FromMinutes(5), default, pilot: selection);

		var wrongSource = pilot with { SourceId = Guid.NewGuid() };
		foreach (var blocked in new[] { templateTest, withSiblings, wrongSource })
		{
			(await ClaimAsync(NotificationQueueKind.Intent, blocked)).ShouldBeEmpty();
			(await ClaimAsync(NotificationQueueKind.Dispatch, blocked)).ShouldBeEmpty();
		}
		(await ClaimAsync(NotificationQueueKind.Dispatch, email)).ShouldBeEmpty();
		(await ClaimAsync(NotificationQueueKind.Dispatch, pilot with { RecipientUserId = Guid.NewGuid() })).ShouldBeEmpty();
		var intent = (await ClaimAsync(NotificationQueueKind.Intent, pilot)).ShouldHaveSingleItem();
		(await ClaimAsync(NotificationQueueKind.Intent, pilot)).ShouldBeEmpty();
		var reclaimed = (await ClaimAsync(NotificationQueueKind.Intent, pilot, now.AddMinutes(6))).ShouldHaveSingleItem();
		reclaimed.Id.ShouldBe(intent.Id);
		reclaimed.LeaseId.ShouldNotBe(intent.LeaseId);
		(await ClaimAsync(NotificationQueueKind.Dispatch, pilot)).ShouldHaveSingleItem().Id.ShouldBe(selectedDispatch);
		(await ClaimAsync(NotificationQueueKind.Dispatch, pilot)).ShouldBeEmpty();

		await using var verify = fixture.CreateContext();
		var selected = await verify.Set<NotificationDispatch>().SingleAsync(x => x.Id == selectedDispatch);
		selected.IntentId.ShouldBe(intent.Id);
		(await verify.Set<NotificationDispatch>().CountAsync(x => x.TenantId == pilot.TenantId && x.AttemptNumber != 0)).ShouldBe(1);
	}

	private static Guid Seed(NotificationPersistenceTestDbContext context, NotificationDispatchPilot pilot, DateTimeOffset now,
		CommunicationChannelType channel = CommunicationChannelType.InApp, bool synthetic = true, Guid? providerSettingId = null)
	{
		const string eventKey = "Academic.Homework.Published";
		var header = new CommunicationTemplate(Guid.NewGuid(), pilot.TenantId) { TemplateName = "Homework", Subject = "Homework", Body = "Body" };
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), pilot.TenantId, header.Id, 1,
			eventKey, "PrimaryGuardian", channel, "en", "Homework", "Body", 1);
		var expires = now.AddMinutes(10);
		var sourceId = pilot is StagingInAppNotificationPilot staging ? staging.SourceId : header.Id;
		var intent = synthetic
			? NotificationIntent.CreateTemplateTest(Guid.NewGuid(), pilot.TenantId, eventKey, sourceId, "hash", "[]", "[]",
				now, expires, pilot.RecipientUserId, pilot.OperationId)
			: NotificationIntent.CreateReviewedManual(Guid.NewGuid(), pilot.TenantId, null, 0, eventKey, "Academic.Homework",
				sourceId, "hash", Guid.NewGuid().ToString("N"), now, "[]", "[]", expires, pilot.RecipientUserId,
				pilot.OperationId, """{"schemaVersion":1}""");
		var snapshot = new CommunicationRecipientSnapshot(Guid.NewGuid(), pilot.TenantId) {
			CampaignId = pilot.OperationId, UserId = pilot.RecipientUserId, RecipientId = pilot.RecipientUserId,
			RecipientType = "TestAccount", IsEligible = true, DisplayName = "Test account"
		};
		var dispatch = new NotificationDispatch(Guid.NewGuid(), pilot.TenantId, intent.Id, revision.Id, "{}",
			intent.Id.ToString("N"), channel, Guid.NewGuid().ToString("N"), "protected", "hash",
			intent.DeduplicationKey, now, expires);
		dispatch.BindPlanningEvidence(pilot.OperationId, snapshot.Id, providerSettingId);
		context.AddRange(header, revision, intent, snapshot, dispatch);
		return dispatch.Id;
	}
}
