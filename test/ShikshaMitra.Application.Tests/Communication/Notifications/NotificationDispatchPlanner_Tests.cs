using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationDispatchPlanner_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);
	private const string EventKey = "Academic.Homework.Published";
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly Guid _contactUser = Guid.NewGuid();
	private readonly Guid _template = Guid.NewGuid();
	private readonly Guid _lease = Guid.NewGuid();
	private readonly List<NotificationDispatch> _dispatches = [];
	private readonly List<CommunicationRecipientSnapshot> _evidence = [];
	private readonly List<CommunicationTemplateRevision> _revisions = [];
	private readonly List<ChannelProviderSetting> _providers = [];
	private readonly List<ConsentRecord> _consents = [];
	private readonly List<WhatsAppTemplateBinding> _bindings = [];
	private readonly List<WebPushSubscription> _subscriptions = [];
	private readonly List<NotificationPreference> _preferences = [];
	private readonly List<NotificationRuntimeRecipient> _recipients = [];
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly IUnitOfWork _unit = Substitute.For<IUnitOfWork>();
	private readonly IClock _clock = Substitute.For<IClock>();
	private readonly INotificationSourceAdapter _adapter = Substitute.For<INotificationSourceAdapter>();
	private readonly NotificationDispatchPayloadProtector _protector;
	private readonly NotificationDispatchPlanner _planner;
	private readonly WhatsAppTemplateCompiler _compiler;
	private readonly NotificationIntent _intent;
	private readonly NotificationQueueClaim _claim;
	private readonly NotificationRecipientRefDto _child = new("Student", Guid.NewGuid());
	private readonly NotificationRecipientRefDto _sibling = new("Student", Guid.NewGuid());
	private NotificationSourceSnapshot _source;

	public NotificationDispatchPlanner_Tests()
	{
		_tenant.Id.Returns(_tenantId);
		_unit.Options.Returns(new AbpUnitOfWorkOptions { IsTransactional = true });
		var units = Substitute.For<IUnitOfWorkManager>();
		units.Current.Returns(_unit);
		_clock.Now.Returns(Now.UtcDateTime);
		_adapter.EventKeys.Returns(new[] { EventKey });
		var catalogue = new AcademicNotificationCatalogue([_adapter]);
		var definition = catalogue.Get(EventKey);
		var codec = new NotificationRuleConfigurationCodec();
		var snapshotCodec = new NotificationIntentSnapshotCodec(codec);
		var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
			[CommunicationChannelType.Email, CommunicationChannelType.InApp, CommunicationChannelType.WhatsApp, CommunicationChannelType.WebPush], [_template],
			NotificationRuleTimingConfiguration.Default, "en");
		var rule = new NotificationRule(Guid.NewGuid(), _tenantId, EventKey, NotificationRuleScope.Create());
		rule.Configure(true, codec.Serialize(config, definition), rule.ConcurrencyStamp);
		var pin = AddVersion(1, CommunicationChannelType.Email, "en", "Published: {{task_title}}");
		var inbox = AddVersion(2, CommunicationChannelType.InApp, "en", "{{task_title}}");
		AddVersion(3, CommunicationChannelType.Email, "en", "Unpinned future content");
		var whatsApp = AddVersion(4, CommunicationChannelType.WhatsApp, "en", "Homework: {{task_title}}");
		var webPush = AddVersion(5, CommunicationChannelType.WebPush, "en", "There is a new homework update.");
		pin.Retire();
		var sourceId = Guid.NewGuid();
		var operation = new CommunicationCampaign(Guid.NewGuid(), _tenantId) {
			CampaignType = NotificationCampaignTypes.Academic, SourceEntityId = sourceId, SourceEntityType = definition.SourceType
		};
		_intent = new(Guid.NewGuid(), _tenantId, rule.Id, rule.Revision, EventKey, definition.SourceType, sourceId, "v1",
			"test-deduplication-key", Now.AddHours(-1), Now.AddHours(-1),
			NotificationRuntimeJson.Serialize(new[] { _child, _sibling }, NotificationErrorCodes.RuntimeSnapshotInvalid),
			NotificationRuntimeJson.Serialize(new[] { pin.Id, inbox.Id, whatsApp.Id, webPush.Id }, NotificationErrorCodes.TemplateInvalid), Now.AddDays(1));
		_intent.BindRuntimeSnapshot(snapshotCodec.Serialize(new(1, new(), config), definition), NotificationIntentOrigin.Automatic);
		_intent.BindAutomaticOperation(operation.Id);
		SetClaim(nameof(NotificationIntent.Status), NotificationIntentState.Processing);
		SetClaim(nameof(NotificationIntent.LeaseId), _lease);
		SetClaim(nameof(NotificationIntent.LeaseExpiresAt), Now.AddMinutes(5));
		_claim = new(_intent.Id, _tenantId, _lease);
		_source = new(true, null, "v1", "/portal/homework", Variables("Shared task"),
			[_child, _sibling], Now.AddHours(2), [new(new(), [_child, _sibling])]);
		_adapter.LoadAsync(Arg.Any<NotificationIntentContext>(), Arg.Any<CancellationToken>()).Returns(_ => _source);
		var resolver = Substitute.For<INotificationRuntimeRecipientResolver>();
		resolver.Supports(EventKey).Returns(true);
		resolver.ResolveAsync(Arg.Any<NotificationCurrentSource>(), Arg.Any<CancellationToken>())
			.Returns(_ => (IReadOnlyList<NotificationRuntimeRecipient>)_recipients);
		var queries = new AsyncQueryableExecuter([]);
		var settings = Substitute.For<ISettingManager>();
		settings.GetOrNullAsync(NotificationSettingNames.SchoolTimeZone, "T", _tenantId.ToString(), false).Returns("UTC");
		using var vapid = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var pushKeys = vapid.ExportParameters(true);
		var app = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["App:ClientUrl"] = "https://school.example.invalid",
			["Notifications:WhatsApp:GraphApiVersion"] = "v26.0",
			[$"Notifications:WhatsApp:Tenants:{_tenantId:N}:AccessTokens:main"] = "fixture",
			["Notifications:WebPush:AllowedOrigins:0"] = "https://push.example.test",
			["Notifications:WebPush:KeyVersion"] = "local-v1",
			["Notifications:WebPush:PublicKey"] = Encode([4, .. pushKeys.Q.X!, .. pushKeys.Q.Y!]),
			["Notifications:WebPush:PrivateKey"] = Encode(pushKeys.D!)
		}).Build();
		var validator = new NotificationTemplateVariableValidator([catalogue], app);
		_compiler = new(validator);
		var revisionRows = Repository(_revisions);
		var renderer = new NotificationTemplateRenderer(_tenant, revisionRows, queries, validator);
		var ids = Substitute.For<IGuidGenerator>();
		ids.Create().Returns(_ => Guid.NewGuid());
		_protector = new(new EphemeralDataProtectionProvider(), _tenant);
		_providers.Add(new(Guid.NewGuid(), _tenantId) {
			Channel = CommunicationChannelType.Email, ProviderName = "SMTP", IsEnabled = true,
			Status = CommunicationProviderStatus.Enabled, ConsentRequired = false
		});
		_planner = new(_tenant, units, _clock, new(_tenant, [catalogue], [_adapter], snapshotCodec, new()),
			[resolver], new(_tenant, new(_tenant, Repository<NotificationRule>([rule]), queries), Repository(_preferences),
				Repository(_consents), queries, new(_tenant, settings), new()),
			new(_tenant, Repository(_providers), queries), new(_tenant, revisionRows, queries, renderer, new(app, validator), _protector, ids,
				new(_tenant, Repository(_providers), queries, app), new(_tenant, Repository(_bindings), queries),
				new(_tenant, Repository(_subscriptions), queries, new(app))),
			Repository<NotificationIntent>([_intent]), Repository<CommunicationCampaign>([operation]),
			Repository(_dispatches), Repository(_evidence), queries, ids, new NotificationOutcomeLock(Substitute.For<INotificationPersonalWriteLock>()));
	}

	[Fact]
	public async Task Web_push_plans_separate_attempts_for_active_browsers_of_the_exact_guardian()
	{
		var phone = Device(_tenantId, _contactUser, "local-v1");
		var desktop = Device(_tenantId, _contactUser, "local-v1");
		var disabled = Device(_tenantId, _contactUser, "local-v1");
		disabled.Disable();
		_subscriptions.AddRange([phone, desktop, disabled, Device(Guid.NewGuid(), _contactUser, "local-v1"),
			Device(_tenantId, Guid.NewGuid(), "local-v1"), Device(_tenantId, _contactUser, "previous-key")]);
		_recipients.Add(Guardian(_child, CommunicationChannelType.WebPush));

		await _planner.PlanAsync(_claim, default);

		_dispatches.Count.ShouldBe(2);
		_dispatches.Select(x => x.DeduplicationKey).Distinct().Count().ShouldBe(2);
		_dispatches.Select(x => _protector.Unprotect(x).Destination)
			.ShouldBe([phone.Id.ToString("N"), desktop.Id.ToString("N")], ignoreOrder: true);
		_dispatches.ShouldAllBe(x => x.ProviderSettingId == null && x.Channel == CommunicationChannelType.WebPush);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Consented_whatsapp_jobs_require_approval_and_pin_the_provider(bool approved)
	{
		var provider = new ChannelProviderSetting(Guid.NewGuid(), _tenantId) {
			Channel = CommunicationChannelType.WhatsApp, ProviderName = WhatsAppProviderRoute.ProviderName,
			Status = CommunicationProviderStatus.Enabled, IsEnabled = true, SecretReference = "main",
			ConfigurationJson = """{"schemaVersion":1,"phoneNumberId":"1001","businessAccountId":"2001"}"""
		};
		_providers.Add(provider);
		var recipient = Guardian(_child, CommunicationChannelType.WhatsApp);
		_recipients.Add(recipient);
		_consents.Add(new(Guid.NewGuid(), _tenantId) {
			Channel = CommunicationChannelType.WhatsApp, RecipientType = "ParentGuardian", RecipientId = _child.StudentId,
			Destination = recipient.Destination!, Status = CommunicationConsentStatus.OptedIn, ConsentCapturedAt = Now.AddDays(-1)
		});
		var revision = _revisions.Single(x => x.Channel == CommunicationChannelType.WhatsApp);
		var template = _compiler.Compile(revision);
		var binding = new WhatsAppTemplateBinding(Guid.NewGuid(), _tenantId, revision.Id, provider.Id, "academic_homework",
			template.ProviderLanguage, WhatsAppApprovedTemplates.Encode(template, _compiler, new("v26.0", "1001", "2001", "fixture")));
		binding.RecordProviderStatus("3001", approved ? NotificationProviderTemplateState.Approved : NotificationProviderTemplateState.Paused, Now);
		_bindings.Add(binding);

		await _planner.PlanAsync(_claim, default);

		if (!approved)
		{
			_dispatches.ShouldBeEmpty();
			_evidence.ShouldHaveSingleItem().BlockedReason.ShouldBe("WhatsAppTemplateNotApproved");
			_intent.Status.ShouldBe(NotificationIntentState.NeedsReview);
			return;
		}
		var dispatch = _dispatches.ShouldHaveSingleItem();
		dispatch.ProviderSettingId.ShouldBe(provider.Id);
		dispatch.TemplateRevisionId.ShouldBe(revision.Id);
		_protector.Unprotect(dispatch).WhatsAppVariables.ShouldNotBeNull();
	}

	[Fact]
	public async Task Persists_encrypted_pinned_content_and_safe_evidence_with_the_planning_transition()
	{
		_recipients.Add(Guardian(_child, CommunicationChannelType.Email));
		_source = _source with { RecipientContent = [new(_child, Variables("First child's homework"), "/portal/first")] };
		await _planner.PlanAsync(_claim, default);
		_intent.Status.ShouldBe(NotificationIntentState.AwaitingDelivery);
		_intent.PlanningCompletedAt.ShouldBe(Now);
		_intent.LeaseId.ShouldBeNull();
		var row = _dispatches.ShouldHaveSingleItem();
		row.Status.ShouldBe(NotificationDispatchState.Pending);
		row.TemplateRevisionId.ShouldBe(_revisions[0].Id);
		row.CampaignId.ShouldBe(_intent.CampaignId);
		row.ProviderSettingId.ShouldBe(_providers[0].Id);
		row.NextAttemptAt.ShouldBe(Now);
		row.ExpiresAt.ShouldBe(_source.ExpiresAt);
		var evidence = _evidence.ShouldHaveSingleItem();
		row.RecipientSnapshotId.ShouldBe(evidence.Id);
		evidence.Email.ShouldBeNull();
		evidence.ChannelEligibilityJson.ShouldNotContain("First child's homework");
		var opened = _protector.Unprotect(row);
		opened.Body.ShouldContain("First child");
		opened.Body.ShouldNotContain("Shared task");
		opened.LinkUrl.ShouldBe("https://school.example.invalid/portal/first");
		opened.Destination.ShouldBe("contact@example.invalid");
		row.ProtectedMessageJson.ShouldNotContain("contact@example.invalid");
		await _unit.Received(1).SaveChangesAsync(default);
	}

	[Fact]
	public async Task An_incomplete_override_cannot_inherit_shared_content_or_block_a_sibling()
	{
		_recipients.AddRange([Guardian(_child, CommunicationChannelType.InApp), Guardian(_sibling, CommunicationChannelType.InApp)]);
		_source = _source with { RecipientContent = [new(_child, new Dictionary<string, NotificationVariableValueDto> {
			["school_name"] = new(NotificationVariableType.Text, "Test school")
		}, "/portal/first")] };
		await _planner.PlanAsync(_claim, default);
		_protector.Unprotect(_dispatches.ShouldHaveSingleItem()).SourceRecipient.ShouldBe(_sibling);
		var excluded = _evidence.Single(x => !x.IsEligible);
		excluded.RecipientId.ShouldBe(_child.StudentId);
		excluded.BlockedReason.ShouldBe(NotificationErrorCodes.RequiredVariableMissing);
	}

	[Fact]
	public async Task Recipients_sharing_a_contact_are_deduplicated_per_child_and_channel()
	{
		_recipients.AddRange([Guardian(_child, CommunicationChannelType.Email), Guardian(_child, CommunicationChannelType.Email, "father"),
			Guardian(_sibling, CommunicationChannelType.Email), Guardian(_child, CommunicationChannelType.InApp)]);
		await _planner.PlanAsync(_claim, default);
		_dispatches.Count.ShouldBe(3);
		_dispatches.Where(x => x.Channel == CommunicationChannelType.Email).Select(x => _protector.Unprotect(x).SourceRecipient)
			.ShouldBe(new[] { _child, _sibling }, ignoreOrder: true);
		_dispatches.Select(x => x.DeduplicationKey).Distinct().Count().ShouldBe(3);
	}

	[Fact]
	public async Task Missing_email_provider_is_visible_without_blocking_inbox_delivery()
	{
		_providers.Clear();
		_recipients.AddRange([Guardian(_child, CommunicationChannelType.Email), Guardian(_child, CommunicationChannelType.InApp)]);
		await _planner.PlanAsync(_claim, default);
		_dispatches.ShouldHaveSingleItem().Channel.ShouldBe(CommunicationChannelType.InApp);
		_evidence.Single(x => !x.IsEligible).BlockedReason.ShouldBe(NotificationErrorCodes.ProviderUnavailable);
	}

	[Fact]
	public async Task Preferred_language_falls_back_to_frozen_variant_instead_of_a_new_publication()
	{
		var preference = new NotificationPreference(Guid.NewGuid(), _tenantId, _contactUser, "Academic.Homework", _child.StudentId);
		preference.Configure("""{"enabledChannels":[2],"quietHoursEnabled":false,"quietHoursStart":null,"quietHoursEnd":null}""", "fr");
		_preferences.Add(preference);
		AddVersion(4, CommunicationChannelType.Email, "fr", "Non-queued translation");
		_recipients.Add(Guardian(_child, CommunicationChannelType.Email));
		await _planner.PlanAsync(_claim, default);
		var dispatch = _dispatches.ShouldHaveSingleItem();
		dispatch.TemplateRevisionId.ShouldBe(_revisions[0].Id);
		_protector.Unprotect(dispatch).LanguageCode.ShouldBe("en");
		_evidence.ShouldHaveSingleItem().PreferredLanguageCode.ShouldBe("en");
	}

	[Fact]
	public async Task The_selected_email_providers_opt_in_policy_applies_before_any_email_dispatch_is_prepared()
	{
		_providers[0].ConsentRequired = true;
		_recipients.Add(Guardian(_child, CommunicationChannelType.Email));
		var exclusions = await _planner.PlanAsync(_claim, default);
		exclusions.ShouldHaveSingleItem().ShouldBe(new(CommunicationChannelType.Email, "ConsentRequired", 1));
		_dispatches.ShouldBeEmpty();
		_evidence.ShouldHaveSingleItem().BlockedReason.ShouldBe("ConsentRequired");
		_intent.Status.ShouldBe(NotificationIntentState.Completed);
		_intent.ErrorCode.ShouldBe("NoEligibleRecipients");
	}

	[Fact]
	public async Task A_lost_lease_cannot_create_dispatches_or_reload_the_source()
	{
		var error = await Should.ThrowAsync<BusinessException>(() => _planner.PlanAsync(_claim with { LeaseId = Guid.NewGuid() }, default));
		error.Code.ShouldBe(NotificationErrorCodes.LeaseLost);
		await _adapter.DidNotReceive().LoadAsync(Arg.Any<NotificationIntentContext>(), Arg.Any<CancellationToken>());
		_dispatches.ShouldBeEmpty();
		_evidence.ShouldBeEmpty();
	}

	[Fact]
	public async Task A_cancelled_source_is_recorded_as_cancelled_instead_of_awaiting_delivery()
	{
		_source = _source with { IsEligible = false };
		await _planner.PlanAsync(_claim, default);
		_intent.Status.ShouldBe(NotificationIntentState.Cancelled);
		_intent.ErrorCode.ShouldBe(NotificationErrorCodes.SourceUnavailable);
		_dispatches.ShouldBeEmpty();
	}

	private NotificationRuntimeRecipient Guardian(NotificationRecipientRefDto child, CommunicationChannelType channel, string relationship = "mother") =>
		new(child, new("Guardian", child.StudentId, relationship, _contactUser), channel, null, "association-revision",
			channel switch { CommunicationChannelType.Email => "contact@example.invalid", CommunicationChannelType.WhatsApp => "+919000000012", _ => null },
			"Guardian Test", "Child Example");

	private CommunicationTemplateRevision AddVersion(int revision, CommunicationChannelType channel, string language, string body)
	{
		var row = new CommunicationTemplateRevision(Guid.NewGuid(), _tenantId, _template, revision, EventKey, "Guardian", channel, language, "Homework", body, 1);
		row.Publish(Guid.NewGuid(), Now);
		_revisions.Add(row);
		return row;
	}

	private void SetClaim(string property, object value) => typeof(NotificationIntent).GetProperty(property)!.SetValue(_intent, value);

	private static WebPushSubscription Device(Guid tenant, Guid user, string version) =>
		new(Guid.NewGuid(), tenant, user, Guid.NewGuid(), Guid.NewGuid().ToString("N"), "protected", version, Now);

	private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

	private static Dictionary<string, NotificationVariableValueDto> Variables(string title) => new() {
		["school_name"] = new(NotificationVariableType.Text, "School Example"),
		["task_title"] = new(NotificationVariableType.Text, title)
	};

	private static IRepository<T, Guid> Repository<T>(List<T> rows) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(_ => rows.AsQueryable());
		repository.InsertAsync(Arg.Any<T>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => {
			var row = call.Arg<T>();
			rows.Add(row);
			return row;
		});
		return repository;
	}
}
