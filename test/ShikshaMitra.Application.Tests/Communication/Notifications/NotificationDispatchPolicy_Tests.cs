using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationDispatchPolicy_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
	private const string EventKey = "Academic.Homework.Published";
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly Guid _studentId = Guid.NewGuid();
	private readonly Guid _userId = Guid.NewGuid();
	private readonly Guid _yearId = Guid.NewGuid();
	private readonly List<NotificationRule> _rules = [];
	private readonly List<NotificationPreference> _preferences = [];
	private readonly List<ConsentRecord> _consents = [];
	private readonly NotificationRule _rule;
	private readonly NotificationIntent _intent;
	private readonly NotificationCurrentSource _source;
	private readonly NotificationDispatchPolicy _policy;

	public NotificationDispatchPolicy_Tests()
	{
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(_tenantId);
		var settings = Substitute.For<ISettingManager>();
		settings.GetOrNullAsync(NotificationSettingNames.SchoolTimeZone, "T", _tenantId.ToString(), false).Returns("UTC");
		var definition = new AcademicNotificationCatalogue([]).Get(EventKey);
		var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
			[CommunicationChannelType.InApp, CommunicationChannelType.Email, CommunicationChannelType.WhatsApp], [Guid.NewGuid()],
			NotificationRuleTimingConfiguration.Default, "en");
		var runtime = new NotificationIntentRuntimeSnapshot(1, new(_yearId), config);
		var codec = new NotificationRuleConfigurationCodec();
		_rule = new(Guid.NewGuid(), _tenantId, EventKey, NotificationRuleScope.Create());
		_rule.Configure(true, codec.Serialize(config, definition), _rule.ConcurrencyStamp);
		_rules.Add(_rule);
		var child = new NotificationRecipientRefDto("Student", _studentId);
		_source = new(_tenantId, definition, runtime, [child], [child], null, Now.AddDays(3), null);
		_intent = new(Guid.NewGuid(), _tenantId, _rule.Id, _rule.Revision, EventKey, definition.SourceType, Guid.NewGuid(),
			"published-v1", "test-key", Now.AddHours(-1), Now.AddHours(-1), "[]", "[]", Now.AddDays(3));
		var executer = new AsyncQueryableExecuter([]);
		_policy = new(tenant, new(tenant, Repository(_rules), executer), Repository(_preferences), Repository(_consents),
			executer, new(tenant, settings), new());
	}

	[Theory]
	[InlineData("deleted")]
	[InlineData("originalDisabled")]
	[InlineData("narrowerDisabled")]
	public async Task Pending_work_stops_when_the_original_or_effective_rule_is_disabled(string action)
	{
		var narrower = new NotificationRule(Guid.NewGuid(), _tenantId, EventKey, NotificationRuleScope.Create(_yearId));
		narrower.Configure(action != "narrowerDisabled", _rule.ConfigurationJson, narrower.ConcurrencyStamp);
		_rules.Add(narrower);
		if (action == "deleted") _rule.IsDeleted = true;
		if (action == "originalDisabled") _rule.Configure(false, _rule.ConfigurationJson, _rule.ConcurrencyStamp);
		var result = await _policy.EvaluateAsync(_intent, _source, [Guardian(CommunicationChannelType.InApp)], Now, false, default);
		result.ExclusionCode.ShouldBe("RuleDisabled");
		result.Recipients.ShouldBeEmpty();
	}

	[Fact]
	public async Task A_new_enabled_rule_does_not_replace_the_frozen_configuration()
	{
		var narrower = new NotificationRule(Guid.NewGuid(), _tenantId, EventKey, NotificationRuleScope.Create(_yearId));
		narrower.Configure(true, _rule.ConfigurationJson.Replace("\"en\"", "\"fr\""), narrower.ConcurrencyStamp);
		_rules.Add(narrower);
		(await Evaluate(Guardian(CommunicationChannelType.Email))).LanguageCode.ShouldBe("en");
	}

	[Fact]
	public async Task Child_and_general_preferences_can_only_restrict_and_child_language_takes_precedence()
	{
		AddPreference(null, [CommunicationChannelType.InApp, CommunicationChannelType.Email], "fr");
		AddPreference(_studentId, [CommunicationChannelType.InApp, CommunicationChannelType.WhatsApp], "hi");
		AddPreference(Guid.NewGuid(), [], "de");
		AddPreference(_studentId, [], "de", tenant: Guid.NewGuid());
		(await Evaluate(Guardian(CommunicationChannelType.Email))).ExclusionCode.ShouldBe("ChannelDisabledByPreference");
		(await Evaluate(Guardian(CommunicationChannelType.WhatsApp))).ExclusionCode.ShouldBe("ChannelDisabledByPreference");
		var inbox = await Evaluate(Guardian(CommunicationChannelType.InApp));
		inbox.ExclusionCode.ShouldBeNull();
		inbox.LanguageCode.ShouldBe("hi");
	}

	[Fact]
	public async Task Multiple_quiet_hours_move_the_send_to_the_first_time_all_windows_allow()
	{
		AddPreference(null, [CommunicationChannelType.Email], "en", start: "06:00", end: "09:00");
		AddPreference(_studentId, [CommunicationChannelType.Email], "en", start: "08:00", end: "10:00");
		var at = Now.AddHours(9);
		var result = await Evaluate(Guardian(CommunicationChannelType.Email), at);
		result.ExclusionCode.ShouldBeNull();
		result.DueAt.ShouldBe(Now.AddDays(1).AddHours(-2));
		var expired = await Evaluate(Guardian(CommunicationChannelType.Email), at, source: _source with { ExpiresAt = Now.AddDays(1).AddHours(-3) });
		expired.ExclusionCode.ShouldBe("SourceExpired");
		expired.DueAt.ShouldBeNull();
	}

	[Fact]
	public async Task A_completely_covered_recurring_quiet_schedule_has_no_send_time()
	{
		AddPreference(null, [CommunicationChannelType.Email], "en", start: "06:00", end: "21:00");
		var decision = await Evaluate(Guardian(CommunicationChannelType.Email), source: _source with { ExpiresAt = null });
		decision.ExclusionCode.ShouldBe("SourceExpired");
		decision.DueAt.ShouldBeNull();
	}

	[Fact]
	public async Task WhatsApp_requires_consent_for_exact_tenant_identity_and_current_number()
	{
		var recipient = Guardian(CommunicationChannelType.WhatsApp);
		AddConsent(recipient, "ParentGuardian", Guid.NewGuid(), CommunicationConsentStatus.OptedIn);
		AddConsent(recipient, "Student", _studentId, CommunicationConsentStatus.OptedIn);
		AddConsent(recipient, "ParentGuardian", null, CommunicationConsentStatus.OptedIn);
		AddConsent(recipient, "ParentGuardian", _studentId, CommunicationConsentStatus.OptedIn, tenant: Guid.NewGuid());
		AddConsent(recipient, "ParentGuardian", _studentId, CommunicationConsentStatus.OptedIn, destination: "+919999999999");
		(await Evaluate(recipient)).ExclusionCode.ShouldBe("ConsentRequired");
		AddConsent(recipient, "ParentGuardian", _studentId, CommunicationConsentStatus.OptedIn);
		(await Evaluate(recipient)).ExclusionCode.ShouldBeNull();
	}

	[Theory]
	[InlineData(CommunicationConsentStatus.OptedOut)]
	[InlineData(CommunicationConsentStatus.Revoked)]
	public async Task An_explicit_opt_out_for_a_shared_number_blocks_delivery_even_with_another_opt_in(CommunicationConsentStatus status)
	{
		var recipient = Guardian(CommunicationChannelType.WhatsApp);
		AddConsent(recipient, "ParentGuardian", _studentId, CommunicationConsentStatus.OptedIn);
		var revoked = AddConsent(recipient, "ParentGuardian", Guid.NewGuid(), status);
		(await Evaluate(recipient)).ExclusionCode.ShouldBe("ConsentRevoked");
		revoked.IsDeleted = true;
		(await Evaluate(recipient)).ExclusionCode.ShouldBeNull();
	}

	[Fact]
	public async Task Email_obeys_provider_opt_in_and_address_opt_out_across_case_differences()
	{
		var recipient = Guardian(CommunicationChannelType.Email);
		(await Evaluate(recipient)).ExclusionCode.ShouldBeNull();
		(await Evaluate(recipient, emailRequiresOptIn: true)).ExclusionCode.ShouldBe("ConsentRequired");
		AddConsent(recipient, "ParentGuardian", _studentId, CommunicationConsentStatus.OptedIn, destination: "MOTHER@EXAMPLE.INVALID");
		(await Evaluate(recipient, emailRequiresOptIn: true)).ExclusionCode.ShouldBeNull();
		AddConsent(recipient, "ParentGuardian", null, CommunicationConsentStatus.OptedOut, destination: "Mother@Example.Invalid");
		(await Evaluate(recipient)).ExclusionCode.ShouldBe("ConsentRevoked");
	}

	[Fact]
	public async Task Staff_may_use_current_employee_or_user_consent_but_a_student_scope_alone_grants_nothing()
	{
		var employee = Guid.NewGuid();
		var teacher = new NotificationRecipientRefDto("AssignedTeacher", UserId: _userId, EmployeeId: employee);
		var from = _source with { CapturedRecipients = [teacher], CurrentRecipients = [teacher] };
		var recipient = new NotificationRuntimeRecipient(teacher, teacher, CommunicationChannelType.WhatsApp, null, "revision", "+919812345678");
		AddConsent(recipient, "ParentGuardian", _studentId, CommunicationConsentStatus.OptedIn);
		(await Evaluate(recipient, source: from)).ExclusionCode.ShouldBe("ConsentRequired");
		AddConsent(recipient, "Employee", employee, CommunicationConsentStatus.OptedIn);
		(await Evaluate(recipient, source: from)).ExclusionCode.ShouldBeNull();
	}

	[Fact]
	public async Task Invalid_preference_configuration_never_falls_back_to_unrestricted_delivery()
	{
		var malformed = new NotificationPreference(Guid.NewGuid(), _tenantId, _userId, "Academic.Homework", _studentId);
		_preferences.Add(malformed);
		(await Evaluate(Guardian(CommunicationChannelType.Email))).ExclusionCode.ShouldBe("PreferenceConfigurationInvalid");
	}

	[Fact]
	public async Task Work_from_a_foreign_tenant_cannot_use_local_consent_or_preferences()
	{
		var error = await Should.ThrowAsync<BusinessException>(() =>
			_policy.EvaluateAsync(_intent, _source with { TenantId = Guid.NewGuid() }, [Guardian(CommunicationChannelType.Email)], Now, false, default));
		error.Code.ShouldBe(NotificationErrorCodes.TenantRequired);
	}

	private async Task<NotificationRecipientDispatchDecision> Evaluate(NotificationRuntimeRecipient recipient, DateTimeOffset? now = null,
		bool emailRequiresOptIn = false, NotificationCurrentSource? source = null) =>
		(await _policy.EvaluateAsync(_intent, source ?? _source, [recipient], now ?? Now, emailRequiresOptIn, default)).Recipients.Single();

	private NotificationRuntimeRecipient Guardian(CommunicationChannelType channel)
	{
		var child = _source.CapturedRecipients.Single();
		var recipient = new NotificationRecipientRefDto("Guardian", _studentId, "mother", _userId);
		var destination = channel switch {
			CommunicationChannelType.Email => "mother@example.invalid",
			CommunicationChannelType.WhatsApp => "+919812345678",
			_ => null
		};
		return new(child, recipient, channel, null, "revision", destination);
	}

	private void AddPreference(Guid? student, CommunicationChannelType[] channels, string language,
		string? start = null, string? end = null, Guid? tenant = null)
	{
		var row = new NotificationPreference(Guid.NewGuid(), tenant ?? _tenantId, _userId, "Academic.Homework", student);
		row.Configure(JsonSerializer.Serialize(new { enabledChannels = channels, quietHoursEnabled = start != null, quietHoursStart = start, quietHoursEnd = end }), language);
		_preferences.Add(row);
	}

	private ConsentRecord AddConsent(NotificationRuntimeRecipient recipient, string kind, Guid? owner, CommunicationConsentStatus status,
		Guid? tenant = null, string? destination = null)
	{
		var row = new ConsentRecord(Guid.NewGuid(), tenant ?? _tenantId) {
			RecipientType = kind, RecipientId = owner, Channel = recipient.Channel, Destination = destination ?? recipient.Destination!,
			Status = status, ConsentCapturedAt = Now.AddDays(-1), RevokedAt = status == CommunicationConsentStatus.OptedIn ? null : Now.AddHours(-1)
		};
		_consents.Add(row);
		return row;
	}

	private static IRepository<TEntity, Guid> Repository<TEntity>(List<TEntity> rows)
		where TEntity : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<TEntity, Guid>>();
		repository.GetQueryableAsync().Returns(_ => rows.AsQueryable());
		return repository;
	}
}
