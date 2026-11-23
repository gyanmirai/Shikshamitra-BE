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
	public async Task Pilot_claims_only_the_selected_synthetic_in_app_dispatch_once()
	{
		var school = Guid.NewGuid();
		var otherSchool = Guid.NewGuid();
		var recipient = Guid.NewGuid();
		var selected = new NotificationInAppPilot(school, Guid.NewGuid(), recipient);
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
		(await verify.Set<NotificationDispatch>().CountAsync(x => x.AttemptNumber != 0)).ShouldBe(1);
	}

	private static Guid Seed(NotificationPersistenceTestDbContext context, NotificationInAppPilot pilot, DateTimeOffset now,
		CommunicationChannelType channel = CommunicationChannelType.InApp, bool synthetic = true)
	{
		const string eventKey = "Academic.Homework.Published";
		var header = new CommunicationTemplate(Guid.NewGuid(), pilot.TenantId) { TemplateName = "Homework", Subject = "Homework", Body = "Body" };
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), pilot.TenantId, header.Id, 1,
			eventKey, "PrimaryGuardian", channel, "en", "Homework", "Body", 1);
		var expires = now.AddMinutes(10);
		var intent = synthetic
			? NotificationIntent.CreateTemplateTest(Guid.NewGuid(), pilot.TenantId, eventKey, header.Id, "hash", "[]", "[]",
				now, expires, pilot.RecipientUserId, pilot.OperationId)
			: NotificationIntent.CreateReviewedManual(Guid.NewGuid(), pilot.TenantId, null, 0, eventKey, "Academic.Homework",
				header.Id, "hash", Guid.NewGuid().ToString("N"), now, "[]", "[]", expires, pilot.RecipientUserId,
				pilot.OperationId, """{"schemaVersion":1}""");
		var snapshot = new CommunicationRecipientSnapshot(Guid.NewGuid(), pilot.TenantId) {
			CampaignId = pilot.OperationId, UserId = pilot.RecipientUserId, RecipientId = pilot.RecipientUserId,
			RecipientType = "TestAccount", IsEligible = true, DisplayName = "Test account"
		};
		var dispatch = new NotificationDispatch(Guid.NewGuid(), pilot.TenantId, intent.Id, revision.Id, "{}",
			intent.Id.ToString("N"), channel, Guid.NewGuid().ToString("N"), "protected", "hash",
			intent.DeduplicationKey, now, expires);
		dispatch.BindPlanningEvidence(pilot.OperationId, snapshot.Id, null);
		context.AddRange(header, revision, intent, snapshot, dispatch);
		return dispatch.Id;
	}
}
