using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationIntentSourceReader_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);
	private const string EventKey = "Academic.Homework.Published";
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly INotificationSourceAdapter _adapter = Substitute.For<INotificationSourceAdapter>();
	private readonly NotificationRecipientRefDto _child = new("Student", Guid.NewGuid());
	private readonly NotificationRecipientRefDto _other = new("Student", Guid.NewGuid());
	private readonly NotificationSourceScopeDto _scope = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
	private readonly NotificationIntentSnapshotCodec _codec = new(new());
	private readonly AcademicNotificationCatalogue _catalogue;
	private readonly NotificationIntentSourceReader _reader;
	private NotificationSourceSnapshot _current;

	public NotificationIntentSourceReader_Tests()
	{
		_tenant.Id.Returns(_tenantId);
		_adapter.EventKeys.Returns(new[] { EventKey });
		_catalogue = new([_adapter]);
		_current = new(true, null, "v1", "/academic/homework", new Dictionary<string, NotificationVariableValueDto>(),
			[_child, _other], Now.AddHours(1), [new(_scope, [_child, _other])]);
		_adapter.LoadAsync(Arg.Any<NotificationIntentContext>(), Arg.Any<CancellationToken>()).Returns(_ => _current);
		_reader = new(_tenant, [_catalogue], [_adapter], _codec, new());
	}

	[Fact]
	public async Task Uses_the_frozen_roster_and_scope_with_current_source_content()
	{
		var intent = Intent();
		var result = await _reader.LoadAsync(intent, Now, default);
		result.ExclusionCode.ShouldBeNull();
		result.CurrentRecipients.ShouldBe(new[] { _child });
		result.Snapshot!.RelativeLink.ShouldBe("/academic/homework");
		await _adapter.Received(1).LoadAsync(Arg.Is<NotificationIntentContext>(x => x.IntentId == intent.Id &&
			x.TenantId == _tenantId && x.SourceId == intent.SourceId && x.AffectedRecipients.Count == 1 &&
			x.AffectedRecipients[0] == _child), default);
	}

	[Theory]
	[InlineData("removed")]
	[InlineData("moved")]
	public async Task Does_not_replace_a_lost_recipient_or_follow_a_move_to_another_scope(string kind)
	{
		_current = kind == "removed"
			? _current with { EligibleRecipients = [_other], AudienceSlices = [new(_scope, [_other])] }
			: _current with { AudienceSlices = [new(_scope, [_other]), new(_scope with { DivisionId = Guid.NewGuid() }, [_child])] };
		var result = await _reader.LoadAsync(Intent(), Now, default);
		result.ExclusionCode.ShouldBeNull();
		result.CurrentRecipients.ShouldBeEmpty();
		result.CapturedRecipients.ShouldBe(new[] { _child });
	}

	[Theory]
	[InlineData("superseded", "SourceRevisionChanged")]
	[InlineData("cancelled", NotificationErrorCodes.SourceUnavailable)]
	[InlineData("earlierDeadline", "SourceExpired")]
	[InlineData("intentDeadline", "SourceExpired")]
	public async Task Invalidated_or_expired_source_exposes_no_delivery_content(string kind, string code)
	{
		_current = kind switch {
			"superseded" => _current with { SourceRevision = "v2" },
			"cancelled" => _current with { IsEligible = false, ExclusionCode = "unsafe adapter detail" },
			"earlierDeadline" => _current with { ExpiresAt = Now },
			_ => _current
		};
		var result = await _reader.LoadAsync(Intent(expiresAt: kind == "intentDeadline" ? Now : null), Now, default);
		result.ExclusionCode.ShouldBe(code);
		result.CurrentRecipients.ShouldBeEmpty();
		result.Snapshot.ShouldBeNull();
	}

	[Fact]
	public async Task Cross_tenant_and_invalid_persisted_references_fail_before_calling_the_adapter()
	{
		var error = await Should.ThrowAsync<BusinessException>(() => _reader.LoadAsync(Intent(Guid.NewGuid()), Now, default));
		error.Code.ShouldBe(NotificationErrorCodes.TenantRequired);
		foreach (var recipients in new[] { "[]", "[null]", "[{\"kind\":\"Guardian\",\"studentId\":\"" + _child.StudentId + "\"}]" })
		{
			error = await Should.ThrowAsync<BusinessException>(() => _reader.LoadAsync(Intent(recipientsJson: recipients), Now, default));
			error.Code.ShouldBe(NotificationErrorCodes.RuntimeSnapshotInvalid);
		}
		await _adapter.DidNotReceive().LoadAsync(Arg.Any<NotificationIntentContext>(), Arg.Any<CancellationToken>());
	}

	private NotificationIntent Intent(Guid? tenant = null, string? recipientsJson = null, DateTimeOffset? expiresAt = null)
	{
		var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
			[CommunicationChannelType.InApp], [Guid.NewGuid()], NotificationRuleTimingConfiguration.Default, "en");
		var intent = new NotificationIntent(Guid.NewGuid(), tenant ?? _tenantId, Guid.NewGuid(), 1, EventKey, "Academic.Homework",
			Guid.NewGuid(), "v1", "test", Now.AddDays(-1), Now.AddDays(-1), recipientsJson ??
			JsonSerializer.Serialize(new[] { _child }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), "[]", expiresAt);
		intent.BindRuntimeSnapshot(_codec.Serialize(new(1, _scope, config), _catalogue.Get(EventKey)), NotificationIntentOrigin.Automatic);
		return intent;
	}
}
