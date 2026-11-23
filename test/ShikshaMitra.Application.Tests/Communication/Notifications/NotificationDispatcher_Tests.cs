using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing.Smtp;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationDispatcher_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);
	private const string EventKey = "Academic.Homework.Published";
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly Guid _lease = Guid.NewGuid();
	private readonly Guid _userId = Guid.NewGuid();
	private readonly Guid _dispatchId = Guid.NewGuid();
	private readonly Guid _templateRevisionId = Guid.NewGuid();
	private readonly NotificationRecipientRefDto _student = new("Student", Guid.NewGuid());
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly IUnitOfWorkManager _units = Substitute.For<IUnitOfWorkManager>();
	private readonly IClock _clock = Substitute.For<IClock>();
	private readonly List<INotificationChannelSender> _senders = [];
	private readonly INotificationChannelSender _sender = Substitute.For<INotificationChannelSender>();
	private readonly ISmtpEmailSenderConfiguration _smtp = Substitute.For<ISmtpEmailSenderConfiguration>();
	private readonly List<NotificationPreference> _preferences = [];
	private readonly List<ConsentRecord> _consents = [];
	private readonly List<string> _steps = [];
	private readonly NotificationRule _rule;
	private readonly NotificationIntent _intent;
	private readonly ChannelProviderSetting _provider;
	private readonly NotificationDispatch _row;
	private readonly NotificationDispatcher _dispatcher;
	private readonly NotificationRecipientRefDto _guardian;
	private IUnitOfWork? _active;
	private NotificationSourceSnapshot _source;
	private NotificationRuntimeRecipient _current;
	private Func<CancellationToken, Task<NotificationChannelSendResult>> _tryProvider =
		_ => Task.FromResult(new NotificationChannelSendResult(NotificationProviderAcceptance.Accepted, "smtp:accepted", null));
	private readonly List<DeliveryAttempt> _attempts = [];

	public NotificationDispatcher_Tests()
	{
		_tenant.Id.Returns(_tenantId);
		_clock.Now.Returns(Now.UtcDateTime);
		_units.Current.Returns(_ => _active);
		_units.Begin(Arg.Any<AbpUnitOfWorkOptions>(), true).Returns(NewUnit);
		var adapter = Substitute.For<INotificationSourceAdapter>();
		adapter.EventKeys.Returns(new[] { EventKey });
		var catalogue = new AcademicNotificationCatalogue([adapter]);
		var definition = catalogue.Get(EventKey);
		var codec = new NotificationRuleConfigurationCodec();
		var snapshots = new NotificationIntentSnapshotCodec(codec);
		var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
			[CommunicationChannelType.Email, CommunicationChannelType.InApp], [Guid.NewGuid()],
			NotificationRuleTimingConfiguration.Default, "en");
		_rule = new(Guid.NewGuid(), _tenantId, EventKey, NotificationRuleScope.Create());
		_rule.Configure(true, codec.Serialize(config, definition), _rule.ConcurrencyStamp);
		var sourceId = Guid.NewGuid();
		var campaign = new CommunicationCampaign(Guid.NewGuid(), _tenantId) {
			CampaignType = NotificationCampaignTypes.Academic, SourceEntityId = sourceId, SourceEntityType = definition.SourceType
		};
		var intent = _intent = new NotificationIntent(Guid.NewGuid(), _tenantId, _rule.Id, _rule.Revision, EventKey, definition.SourceType,
			sourceId, "v1", "test", Now.AddHours(-1), Now.AddHours(-1),
			NotificationRuntimeJson.Serialize(new[] { _student }, NotificationErrorCodes.RuntimeSnapshotInvalid),
			NotificationRuntimeJson.Serialize(new[] { _templateRevisionId }, NotificationErrorCodes.TemplateInvalid));
		intent.BindRuntimeSnapshot(snapshots.Serialize(new(1, new(), config), definition), NotificationIntentOrigin.Automatic);
		intent.BindAutomaticOperation(campaign.Id);
		Set(intent, nameof(NotificationIntent.Status), NotificationIntentState.Processing);
		Set(intent, nameof(NotificationIntent.LeaseId), _lease);
		Set(intent, nameof(NotificationIntent.LeaseExpiresAt), Now.AddMinutes(5));
		intent.CompletePlanning(_lease, Now.AddMinutes(-1));
		_source = new(true, null, "v1", "/portal/homework", new Dictionary<string, NotificationVariableValueDto>(),
			[_student], Now.AddDays(2), [new(new(), [_student])]);
		adapter.LoadAsync(Arg.Any<NotificationIntentContext>(), Arg.Any<CancellationToken>()).Returns(_ => _source);
		_guardian = new("Guardian", _student.StudentId, "mother", _userId);
		_current = Candidate();
		var resolver = Substitute.For<INotificationRuntimeRecipientResolver>();
		resolver.Supports(EventKey).Returns(true);
		resolver.ResolveAsync(Arg.Any<NotificationCurrentSource>(), Arg.Any<CancellationToken>()).Returns(_ => (IReadOnlyList<NotificationRuntimeRecipient>)[_current]);
		var protector = new NotificationDispatchPayloadProtector(new EphemeralDataProtectionProvider(), _tenant);
		var cipher = protector.Protect(new(_tenantId, _dispatchId, intent.Id, _templateRevisionId, CommunicationChannelType.Email, "hash"),
			new(_student, _guardian, "association", _current.Destination, "Homework", "Rendered homework notice",
				"https://school.example.invalid/portal/homework", "en", null));
		_row = new(_dispatchId, _tenantId, intent.Id, _templateRevisionId, "{}", "context", CommunicationChannelType.Email,
			"fingerprint", cipher, "hash", "dedup", Now, Now.AddDays(1));
		_provider = new(Guid.NewGuid(), _tenantId) {
			Channel = CommunicationChannelType.Email, ProviderName = "SMTP", IsEnabled = true, Status = CommunicationProviderStatus.Enabled, ConsentRequired = false
		};
		_row.BindPlanningEvidence(campaign.Id, Guid.NewGuid(), _provider.Id);
		Set(_row, nameof(NotificationDispatch.Status), NotificationDispatchState.Processing);
		Set(_row, nameof(NotificationDispatch.LeaseId), _lease);
		Set(_row, nameof(NotificationDispatch.LeaseExpiresAt), Now.AddMinutes(5));
		Set(_row, nameof(NotificationDispatch.AttemptNumber), 1);
		var queries = new AsyncQueryableExecuter([]);
		var settings = Substitute.For<ISettingManager>();
		settings.GetOrNullAsync(NotificationSettingNames.SchoolTimeZone, "T", _tenantId.ToString(), false).Returns("UTC");
		var app = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["App:ClientUrl"] = "https://school.example.invalid"
		}).Build();
		var validator = new NotificationTemplateVariableValidator([catalogue], app);
		var reader = new NotificationIntentSourceReader(_tenant, [catalogue], [adapter], snapshots, new());
		var policy = new NotificationDispatchPolicy(_tenant, new(_tenant, Repository<NotificationRule>([_rule]), queries),
			Repository(_preferences), Repository(_consents), queries, new(_tenant, settings), new());
		_smtp.GetHostAsync().Returns("smtp.example.invalid");
		_smtp.GetPortAsync().Returns(25);
		_smtp.GetDefaultFromAddressAsync().Returns("fixture@example.invalid");
		_smtp.GetDefaultFromDisplayNameAsync().Returns("Fixture school");
		_smtp.GetUseDefaultCredentialsAsync().Returns(true);
		var emailRoute = new NotificationEmailRoute(_tenant, Repository<ChannelProviderSetting>([_provider]), queries, _smtp);
		var whatsAppRoute = new WhatsAppProviderRoute(_tenant, Repository<ChannelProviderSetting>([_provider]), queries, app);
		var approvals = new WhatsAppApprovedTemplates(_tenant, Repository<WhatsAppTemplateBinding>([]), queries);
		var webPush = new WebPushDispatchSubscriptions(_tenant, Repository<WebPushSubscription>([]), queries, new(app, _tenant));
		var readiness = new NotificationDispatchReadiness(protector, reader, [resolver], policy,
			emailRoute, new(app, validator), whatsAppRoute, approvals, webPush,
			new(app, Repository<Volo.Abp.Identity.IdentityUser>([]), Repository<CommunicationTemplateRevision>([]),
				queries, validator, policy, emailRoute, whatsAppRoute, approvals, webPush));
		_sender.Channel.Returns(CommunicationChannelType.Email);
		_sender.SendAsync(Arg.Any<NotificationChannelSendRequest>(), Arg.Any<CancellationToken>()).Returns(call => {
			_active.ShouldBeNull();
			_row.IoStartedAt.ShouldBe(Now);
			_steps.Add("provider");
			return _tryProvider(call.Arg<CancellationToken>());
		});
		_senders.Add(_sender);
		var attempts = Repository(_attempts);
		attempts.InsertAsync(Arg.Any<DeliveryAttempt>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => {
			var attempt = call.Arg<DeliveryAttempt>(); _attempts.Add(attempt); return attempt;
		});
		var guids = Substitute.For<Volo.Abp.Guids.IGuidGenerator>(); guids.Create().Returns(_ => Guid.NewGuid());
		var delivery = new NotificationDeliveryProjection(_tenant, Substitute.For<INotificationPersonalWriteLock>(), new(_tenant, settings),
			Repository<ChannelProviderSetting>([_provider]), attempts, queries, guids, _clock);
		_dispatcher = new(_tenant, _units, _clock, readiness, _senders, new(Substitute.For<INotificationPersonalWriteLock>()), Repository<NotificationDispatch>([_row]),
			Repository<NotificationIntent>([intent]), Repository<CommunicationCampaign>([campaign]), queries, delivery);
	}

	[Fact]
	public async Task Commits_the_io_marker_and_releases_the_transaction_before_provider_work()
	{
		await _dispatcher.DispatchAsync(_dispatchId, _lease, default);
		_steps.ShouldBe(new[] { "begin", "commit", "dispose", "provider", "begin", "commit", "dispose" });
		_row.Status.ShouldBe(NotificationDispatchState.ProviderAccepted);
		_row.ProviderMessageId.ShouldBe("smtp:accepted");
		_row.AcceptedAt.ShouldBe(Now);
		var attempt = _attempts.Single();
		attempt.Status.ShouldBe(CommunicationDeliveryStatus.Sent);
		attempt.DeliveredAt.ShouldBeNull();
		attempt.Destination.ShouldBe("Hidden");
		await _sender.Received(1).SendAsync(Arg.Is<NotificationChannelSendRequest>(x =>
			x.DispatchId == _dispatchId && x.Destination == "guardian@example.invalid" && x.TemplateRevisionId == _templateRevisionId &&
			x.AttemptNumber == 1 && x.ProviderSettingId == _provider.Id), default);
	}

	[Fact]
	public async Task An_exhausted_daily_quota_defers_without_starting_or_consuming_a_provider_attempt()
	{
		_provider.DailyQuota = 1;
		_attempts.Add(new DeliveryAttempt(Guid.NewGuid(), _tenantId) {
			NotificationDispatchId = Guid.NewGuid(), NotificationProviderSettingId = _provider.Id,
			QueuedAt = Now.AddHours(-1), Status = CommunicationDeliveryStatus.Sent
		});

		await _dispatcher.DispatchAsync(_dispatchId, _lease, default);

		_steps.ShouldBe(new[] { "begin", "commit", "dispose" });
		_row.Status.ShouldBe(NotificationDispatchState.Pending);
		_row.IoStartedAt.ShouldBeNull();
		_row.AttemptNumber.ShouldBe(0);
		_row.ErrorCode.ShouldBe(NotificationErrorCodes.ProviderQuotaExhausted);
		_row.NextAttemptAt.ShouldBe(new DateTimeOffset(Now.UtcDateTime.Date.AddDays(1)));
		_attempts.Count.ShouldBe(1);
	}

	[Theory]
	[InlineData("destination", "RecipientAssociationChanged")]
	[InlineData("association", "RecipientAssociationChanged")]
	[InlineData("guardian", "RecipientAssociationChanged")]
	[InlineData("link", "SourceRevisionChanged")]
	[InlineData("revision", "SourceRevisionChanged")]
	[InlineData("expired", "SourceExpired")]
	[InlineData("consent", "ConsentRevoked")]
	[InlineData("provider", NotificationErrorCodes.ProviderUnavailable)]
	[InlineData("smtp-configuration", NotificationErrorCodes.ProviderUnavailable)]
	[InlineData("sender", NotificationErrorCodes.ProviderUnavailable)]
	[InlineData("rule", "RuleDisabled")]
	public async Task Send_time_changes_produce_a_safe_skip_without_provider_io(string change, string reason)
	{
		switch (change)
		{
			case "destination": _current = Candidate(destination: "changed@example.invalid"); break;
			case "association": _current = Candidate(revision: "new-link"); break;
			case "guardian": _current = Candidate(_guardian with { FamilyRelationship = "father" }); break;
			case "link": _source = _source with { RelativeLink = "/portal/others" }; break;
			case "revision": _source = _source with { SourceRevision = "v2" }; break;
			case "expired": _source = _source with { ExpiresAt = Now }; break;
			case "consent":
				_consents.Add(new(Guid.NewGuid(), _tenantId) {
					Channel = CommunicationChannelType.Email, Destination = _current.Destination!, Status = CommunicationConsentStatus.OptedOut
				});
				break;
			case "provider": _provider.IsEnabled = false; break;
			case "smtp-configuration": _smtp.GetHostAsync().Returns(string.Empty); break;
			case "sender": _senders.Clear(); break;
			case "rule": _rule.Configure(false, _rule.ConfigurationJson, _rule.ConcurrencyStamp); break;
		}
		await _dispatcher.DispatchAsync(_dispatchId, _lease, default);
		_row.Status.ShouldBe(NotificationDispatchState.Skipped);
		_row.ErrorCode.ShouldBe(reason);
		_row.AttemptNumber.ShouldBe(0);
		_row.IoStartedAt.ShouldBeNull();
		await _sender.DidNotReceive().SendAsync(Arg.Any<NotificationChannelSendRequest>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task A_new_quiet_window_defers_work_without_using_a_transmission_attempt()
	{
		var row = new NotificationPreference(Guid.NewGuid(), _tenantId, _userId, "Academic.Homework", _student.StudentId);
		row.Configure("""{"enabledChannels":[2],"quietHoursEnabled":true,"quietHoursStart":"09:00","quietHoursEnd":"11:00"}""", "en");
		_preferences.Add(row);
		await _dispatcher.DispatchAsync(_dispatchId, _lease, default);
		_row.Status.ShouldBe(NotificationDispatchState.Pending);
		_row.NextAttemptAt.ShouldBe(Now.AddHours(1));
		_row.AttemptNumber.ShouldBe(0);
		_row.LeaseId.ShouldBeNull();
		await _sender.DidNotReceive().SendAsync(Arg.Any<NotificationChannelSendRequest>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Confirmed_transient_rejection_retries_after_the_providers_wait()
	{
		_tryProvider = _ => Task.FromResult(new NotificationChannelSendResult(NotificationProviderAcceptance.Rejected, null,
			"ProviderBusy", true, TimeSpan.FromMinutes(4)));
		await _dispatcher.DispatchAsync(_dispatchId, _lease, default);
		_row.Status.ShouldBe(NotificationDispatchState.Pending);
		_row.NextAttemptAt.ShouldBe(Now.AddMinutes(4));
		_row.IoStartedAt.ShouldBeNull();
		_row.AttemptNumber.ShouldBe(1);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	public async Task Last_attempt_and_retries_beyond_expiry_are_terminal(int attempt)
	{
		Set(_row, nameof(NotificationDispatch.AttemptNumber), attempt);
		_tryProvider = _ => Task.FromResult(new NotificationChannelSendResult(NotificationProviderAcceptance.Rejected, null,
			"ProviderBusy", true, TimeSpan.FromDays(attempt == 1 ? 2 : 0)));
		await _dispatcher.DispatchAsync(_dispatchId, _lease, default);
		_row.Status.ShouldBe(NotificationDispatchState.Failed);
		_row.ErrorCode.ShouldBe("ProviderBusy");
	}

	[Fact]
	public async Task Exception_after_io_records_unknown_acceptance_without_exposing_transport_text()
	{
		_tryProvider = _ => throw new InvalidOperationException("guardian@example.invalid");
		await _dispatcher.DispatchAsync(_dispatchId, _lease, default);
		_row.Status.ShouldBe(NotificationDispatchState.UnknownAcceptance);
		_row.ErrorCode.ShouldBe(NotificationErrorCodes.UnknownAcceptance);
		_row.IoLeaseId.ShouldBe(_lease);
		_row.LeaseId.ShouldBeNull();
	}

	[Fact]
	public async Task Worker_cancellation_after_the_provider_started_persists_unknown_acceptance()
	{
		using var cancellation = new CancellationTokenSource();
		_tryProvider = token => {
			cancellation.Cancel();
			return Task.FromCanceled<NotificationChannelSendResult>(token);
		};
		await Should.ThrowAsync<OperationCanceledException>(() => _dispatcher.DispatchAsync(_dispatchId, _lease, cancellation.Token));
		_row.Status.ShouldBe(NotificationDispatchState.UnknownAcceptance);
		_steps.Count(x => x == "commit").ShouldBe(2);
	}

	[Fact]
	public async Task The_original_provider_receipt_can_resolve_recovered_unknown_acceptance()
	{
		_tryProvider = _ => {
			_row.RecoverExpiredLease(Now.AddMinutes(6)).ShouldBeTrue();
			_intent.CompleteDelivery(NotificationIntentState.NeedsReview, NotificationErrorCodes.UnknownAcceptance);
			_clock.Now.Returns(Now.AddMinutes(6).UtcDateTime);
			return Task.FromResult(new NotificationChannelSendResult(NotificationProviderAcceptance.Accepted, "late-id", null));
		};
		await _dispatcher.DispatchAsync(_dispatchId, _lease, default);
		_row.Status.ShouldBe(NotificationDispatchState.ProviderAccepted);
		_row.ProviderMessageId.ShouldBe("late-id");
		_intent.Status.ShouldBe(NotificationIntentState.AwaitingDelivery);
		_intent.ErrorCode.ShouldBeNull();
	}

	[Fact]
	public async Task Rejects_stale_lease_and_ambient_transaction_before_provider_work()
	{
		(await Should.ThrowAsync<BusinessException>(() => _dispatcher.DispatchAsync(_dispatchId, Guid.NewGuid(), default)))
			.Code.ShouldBe(NotificationErrorCodes.LeaseLost);
		_active = Substitute.For<IUnitOfWork>();
		(await Should.ThrowAsync<BusinessException>(() => _dispatcher.DispatchAsync(_dispatchId, _lease, default)))
			.Code.ShouldBe(NotificationErrorCodes.StateConflict);
		await _sender.DidNotReceive().SendAsync(Arg.Any<NotificationChannelSendRequest>(), Arg.Any<CancellationToken>());
	}

	private IUnitOfWork NewUnit(NSubstitute.Core.CallInfo call)
	{
		call.Arg<AbpUnitOfWorkOptions>().IsTransactional.ShouldBeTrue();
		_active.ShouldBeNull();
		var unit = Substitute.For<IUnitOfWork>();
		_active = unit;
		_steps.Add("begin");
		unit.CompleteAsync(Arg.Any<CancellationToken>()).Returns(_ => { _steps.Add("commit"); return Task.CompletedTask; });
		unit.When(x => x.Dispose()).Do(_ => { _active = null; _steps.Add("dispose"); });
		return unit;
	}

	private NotificationRuntimeRecipient Candidate(NotificationRecipientRefDto? guardian = null, string revision = "association",
		string destination = "guardian@example.invalid") =>
		new(_student, guardian ?? _guardian, CommunicationChannelType.Email, null, revision, destination);

	private static IRepository<T, Guid> Repository<T>(List<T> rows) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(_ => rows.AsQueryable());
		return repository;
	}

	private static void Set<T>(T instance, string property, object value) => typeof(T).GetProperty(property)!.SetValue(instance, value);
}
