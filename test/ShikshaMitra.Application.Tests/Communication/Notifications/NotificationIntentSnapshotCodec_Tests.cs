using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationIntentSnapshotCodec_Tests
{
	private readonly NotificationIntentSnapshotCodec _codec = new(new());
	private readonly NotificationEventDefinition _definition = new AcademicNotificationCatalogue([]).Get("Academic.Homework.Published");
	[Fact]
	public void A_snapshot_freezes_the_rule_and_authoritative_source_scope_together()
	{
		var snapshot = Valid(); var json = _codec.Serialize(snapshot, _definition); var result = _codec.Deserialize(json, _definition);
		result.SchemaVersion.ShouldBe(1); result.SourceScope.ShouldBe(snapshot.SourceScope);
		result.Configuration.GuardianMode.ShouldBe(NotificationGuardianMode.PrimaryOnly);
		result.Configuration.IncludeStudentAccount.ShouldBeFalse(); result.Configuration.Channels.ShouldBe(snapshot.Configuration.Channels);
		result.Configuration.TemplateIds.ShouldBe(snapshot.Configuration.TemplateIds);
	}
	[Theory]
	[InlineData("schema")]
	[InlineData("missing")]
	[InlineData("scope-null")]
	[InlineData("scope-parent")]
	[InlineData("tenant")]
	[InlineData("unknown")]
	[InlineData("duplicate")]
	[InlineData("rule")]
	[InlineData("legacy")]
	public void A_stored_snapshot_rejects_ambiguous_incomplete_or_untrusted_shapes(string mutation)
	{
		var json = _codec.Serialize(Valid(), _definition); var node = JsonNode.Parse(json)!.AsObject();
		switch (mutation)
		{
			case "schema": node["schemaVersion"] = 2; break;
			case "missing": node.Remove("configuration"); break;
			case "scope-null": node["sourceScope"] = null; break;
			case "scope-parent": node["sourceScope"]!["academicYearId"] = null; break;
			case "tenant": node["sourceScope"]!["tenantId"] = Guid.NewGuid(); break;
			case "unknown": node["configuration"]!["destination"] = "private@example.test"; break;
			case "rule": node["configuration"]!["schemaVersion"] = 2; break;
		}
		json = mutation switch {
			"duplicate" => json.Insert(1, "\"schemaVersion\":2,"),
			"legacy" => JsonSerializer.Serialize(Valid().Configuration, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
			_ => node.ToJsonString()
		};
		var error = Should.Throw<BusinessException>(() => _codec.Deserialize(json, _definition));
		error.Code.ShouldBe(NotificationErrorCodes.RuntimeSnapshotInvalid); error.InnerException.ShouldBeNull();
	}
	[Fact]
	public void Invalid_input_cannot_be_serialized_into_an_executable_snapshot()
	{
		var input = Valid();
		Should.Throw<BusinessException>(() => _codec.Serialize(input with { SourceScope = new(ClassId: Guid.NewGuid()) }, _definition));
		Should.Throw<BusinessException>(() => _codec.Serialize(input with { Configuration = input.Configuration with { IncludeStudentAccount = true,
			Channels = [] } }, _definition));
	}
	private static NotificationIntentRuntimeSnapshot Valid() => new(1, new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
		new(1, NotificationGuardianMode.PrimaryOnly, [], false, [], [CommunicationChannelType.InApp], [Guid.NewGuid()], NotificationRuleTimingConfiguration.Default, "en"));
}
