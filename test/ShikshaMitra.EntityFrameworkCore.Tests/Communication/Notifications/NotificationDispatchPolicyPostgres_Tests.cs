using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationDispatchPolicyPostgres_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	[NotificationPostgresFact]
	public async Task Native_queries_batch_scope_preferences_and_check_case_insensitive_email_opt_out()
	{
		var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
		var tenantId = Guid.NewGuid();
		var user = Guid.NewGuid();
		var child = Guid.NewGuid();
		var year = Guid.NewGuid();
		var definition = new AcademicNotificationCatalogue([]).Get("Academic.Homework.Published");
		var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
			[CommunicationChannelType.Email, CommunicationChannelType.InApp, CommunicationChannelType.WhatsApp], [Guid.NewGuid()],
			NotificationRuleTimingConfiguration.Default, "en");
		var rule = new NotificationRule(Guid.NewGuid(), tenantId, definition.EventKey, NotificationRuleScope.Create());
		rule.Configure(true, new NotificationRuleConfigurationCodec().Serialize(config, definition), rule.ConcurrencyStamp);
		var row = new NotificationPreference(Guid.NewGuid(), tenantId, user, "Academic.Homework", child);
		row.Configure("""{"enabledChannels":[0,2,3],"quietHoursEnabled":false,"quietHoursStart":null,"quietHoursEnd":null}""", "hi");
		var siblingPreference = new NotificationPreference(Guid.NewGuid(), tenantId, user, "Academic.Homework", Guid.NewGuid());
		siblingPreference.Configure("""{"enabledChannels":[],"quietHoursEnabled":false,"quietHoursStart":null,"quietHoursEnd":null}""", "fr");
		var revoked = Consent(tenantId, "CONTACT@EXAMPLE.INVALID", CommunicationChannelType.Email, CommunicationConsentStatus.OptedOut);
		var optedIn = Consent(tenantId, "+919812345678", CommunicationChannelType.WhatsApp, CommunicationConsentStatus.OptedIn);
		var otherTenantRevocation = Consent(Guid.NewGuid(), "+919812345678", CommunicationChannelType.WhatsApp, CommunicationConsentStatus.Revoked);
		await using (var seed = fixture.CreateContext())
		{
			seed.AddRange(rule, row, siblingPreference, revoked, optedIn, otherTenantRevocation);
			await seed.SaveChangesAsync();
		}
		await using var db = fixture.CreateContext();
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(tenantId);
		var settings = Substitute.For<ISettingManager>();
		settings.GetOrNullAsync(NotificationSettingNames.SchoolTimeZone, "T", tenantId.ToString(), false).Returns("UTC");
		var queries = new AsyncQueryableExecuter([]);
		var policy = new NotificationDispatchPolicy(tenant, new(tenant, Repository<NotificationRule>(db), queries),
			Repository<NotificationPreference>(db), Repository<ConsentRecord>(db), queries, new(tenant, settings), new());
		var intent = new NotificationIntent(Guid.NewGuid(), tenantId, rule.Id, rule.Revision, definition.EventKey, definition.SourceType,
			Guid.NewGuid(), "v1", "dispatch-policy", now.AddMinutes(-1), now.AddMinutes(-1), "[]", "[]");
		var sourceReference = new NotificationRecipientRefDto("Student", child);
		var recipientRef = new NotificationRecipientRefDto("Guardian", child, "mother", user);
		var source = new NotificationCurrentSource(tenantId, definition, new(1, new(year), config),
			[sourceReference], [sourceReference], null, null, null);
		var results = await policy.EvaluateAsync(intent, source, [
			new(sourceReference, recipientRef, CommunicationChannelType.Email, null, "revision", "contact@example.invalid"),
			new(sourceReference, recipientRef, CommunicationChannelType.WhatsApp, null, "revision", "+919812345678"),
			new(sourceReference, recipientRef, CommunicationChannelType.InApp, null, "revision")
		], now, false, default);
		results.ExclusionCode.ShouldBeNull();
		results.Recipients.Count.ShouldBe(3);
		results.Recipients.Single(x => x.Recipient.Channel == CommunicationChannelType.Email).ExclusionCode.ShouldBe("ConsentRevoked");
		var reachable = results.Recipients.Where(x => x.ExclusionCode == null).ToArray();
		reachable.Length.ShouldBe(2);
		reachable.ShouldAllBe(x => x.DueAt == now && x.LanguageCode == "hi");

		ConsentRecord Consent(Guid school, string destination, CommunicationChannelType channel, CommunicationConsentStatus status) =>
			new(Guid.NewGuid(), school) {
				RecipientType = "ParentGuardian", RecipientId = child, Destination = destination, Channel = channel, Status = status,
				ConsentCapturedAt = now.AddDays(-2), RevokedAt = status == CommunicationConsentStatus.OptedIn ? null : now.AddDays(-1)
			};
	}

	private static IRepository<T, Guid> Repository<T>(DbContext db) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(db.Set<T>());
		return repository;
	}
}
