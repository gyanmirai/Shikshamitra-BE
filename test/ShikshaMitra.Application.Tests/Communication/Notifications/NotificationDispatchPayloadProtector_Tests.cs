using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationDispatchPayloadProtector_Tests
{
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly EphemeralDataProtectionProvider _keys = new();
	private readonly NotificationDispatchPayloadKey _context;
	private readonly NotificationDispatchPayload _payload;

	public NotificationDispatchPayloadProtector_Tests()
	{
		_tenant.Id.Returns(_tenantId);
		var student = Guid.NewGuid();
		_context = new(_tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CommunicationChannelType.WhatsApp, "render-hash");
		_payload = new(new("Student", student), new("Guardian", student, "mother"), "current-association-revision",
			"+919812345678", "Private title", "Private rendered body", "https://school.example.invalid/portal",
			"en", new Dictionary<string, NotificationVariableValueDto> { ["student_name"] = new(NotificationVariableType.Text, "Test Child") });
	}

	[Fact]
	public void Sealed_content_round_trips_and_never_exposes_personalized_values_in_the_row_or_string_representation()
	{
		var writer = new NotificationDispatchPayloadProtector(_keys, _tenant);
		var sealedContent = writer.Protect(_context, _payload);
		foreach (var value in new[] { _payload.Destination, _payload.Body, "Test Child", _payload.LinkUrl })
		{
			sealedContent.ShouldNotContain(value!);
			_payload.ToString().ShouldNotContain(value!);
		}
		var reader = new NotificationDispatchPayloadProtector(_keys, _tenant);
		var opened = reader.Unprotect(Row(_context, sealedContent));
		opened.Recipient.ShouldBe(_payload.Recipient);
		opened.SourceRecipient.ShouldBe(_payload.SourceRecipient);
		opened.AssociationRevision.ShouldBe(_payload.AssociationRevision);
		opened.Destination.ShouldBe(_payload.Destination);
		opened.Body.ShouldBe(_payload.Body);
		opened.WhatsAppVariables!["student_name"].ShouldBe(_payload.WhatsAppVariables!["student_name"]);
	}

	[Theory]
	[InlineData("dispatch")]
	[InlineData("intent")]
	[InlineData("revision")]
	[InlineData("channel")]
	[InlineData("hash")]
	[InlineData("tenant")]
	[InlineData("ciphertext")]
	public void Copying_or_altering_persisted_content_cannot_open_the_payload(string alteration)
	{
		var service = new NotificationDispatchPayloadProtector(_keys, _tenant);
		var sealedContent = service.Protect(_context, _payload);
		var original = _context;
		var changed = alteration switch {
			"dispatch" => original with { DispatchId = Guid.NewGuid() },
			"intent" => original with { IntentId = Guid.NewGuid() },
			"revision" => original with { TemplateRevisionId = Guid.NewGuid() },
			"channel" => original with { Channel = CommunicationChannelType.Email },
			"hash" => original with { ContentHash = "different-hash" },
			"tenant" => original with { TenantId = Guid.NewGuid() },
			_ => original
		};
		_tenant.Id.Returns(changed.TenantId);
		if (alteration == "ciphertext") sealedContent = sealedContent[..20] + "!" + sealedContent[21..];
		var error = Should.Throw<BusinessException>(() => service.Unprotect(Row(changed, sealedContent)));
		error.Code.ShouldBe("DispatchPayloadInvalid");
		error.InnerException.ShouldBeNull();
	}

	[Fact]
	public void Current_tenant_is_required_and_out_of_range_content_is_rejected_before_queue_persistence()
	{
		var service = new NotificationDispatchPayloadProtector(_keys, _tenant);
		var large = _payload with { Body = new string('水', 65000) };
		Should.Throw<BusinessException>(() => service.Protect(_context, large)).Code.ShouldBe("DispatchPayloadTooLarge");
		_tenant.Id.Returns((Guid?)null);
		Should.Throw<BusinessException>(() => service.Protect(_context, _payload)).Code.ShouldBe(NotificationErrorCodes.TenantRequired);
	}

	private static NotificationDispatch Row(NotificationDispatchPayloadKey key, string sealedContent) =>
		new(key.DispatchId, key.TenantId, key.IntentId, key.TemplateRevisionId, "{}", "safe-context", key.Channel,
			"fingerprint", sealedContent, key.ContentHash, "deduplication-key", DateTimeOffset.UtcNow);
}
