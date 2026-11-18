using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationRuleConfiguration_Tests
{
	private readonly NotificationRuleConfigurationCodec _codec = new();
	private readonly AcademicNotificationCatalogue _catalogue = new([]);
	private NotificationEventDefinition Definition => _catalogue.Get("Academic.Homework.Published");
	[Fact]
	public void A_saved_snapshot_preserves_explicit_recipients_timing_and_channel_choices()
	{
		var original = Valid() with { GuardianMode = NotificationGuardianMode.PrimaryAndSelected,
			AdditionalGuardianRelationships = ["mother"], IncludeStudentAccount = true, StaffAudienceSelectors = ["AssignedTeacher"] };
		var json = _codec.Serialize(original, Definition); var result = _codec.Deserialize(json, Definition);
		result.GuardianMode.ShouldBe(original.GuardianMode); result.AdditionalGuardianRelationships.ShouldBe(["mother"]);
		result.IncludeStudentAccount.ShouldBeTrue(); result.StaffAudienceSelectors.ShouldBe(["AssignedTeacher"]);
		result.Channels.ShouldBe(original.Channels); result.TemplateIds.ShouldBe(original.TemplateIds);
		result.Timing.ShouldBe(original.Timing); result.FallbackLanguage.ShouldBe("en");
	}
	[Theory]
	[MemberData(nameof(InvalidConfigurations))]
	public void Invalid_configuration_is_rejected_before_it_can_be_persisted(NotificationRuleRuntimeConfiguration configuration)
	{
		var error = Should.Throw<BusinessException>(() => _codec.Serialize(configuration, Definition));
		error.Code.ShouldBe(NotificationErrorCodes.RuleConfigurationInvalid);
	}
	[Theory]
	[InlineData("unknown")]
	[InlineData("missing")]
	[InlineData("null")]
	[InlineData("duplicate")]
	[InlineData("case")]
	public void Stored_json_is_strict_and_does_not_silently_apply_defaults(string mutation)
	{
		var json = _codec.Serialize(Valid(), Definition); var document = JsonNode.Parse(json)!.AsObject();
		switch (mutation)
		{
			case "unknown": document["destination"] = "private@example.test"; break;
			case "missing": document.Remove("includeStudentAccount"); break;
			case "null": document["timing"] = null; break;
			case "case": document["Channels"] = document["channels"]!.DeepClone(); document.Remove("channels"); break;
		}
		json = mutation == "duplicate" ? json.Insert(1, "\"schemaVersion\":2,") : document.ToJsonString();
		var error = Should.Throw<BusinessException>(() => _codec.Deserialize(json, Definition));
		error.Code.ShouldBe(NotificationErrorCodes.RuleConfigurationInvalid); error.InnerException.ShouldBeNull();
	}
	[Fact]
	public void Unsupported_staff_audiences_and_urgent_bypass_are_event_specific()
	{
		var staff = _catalogue.Get("Academic.LessonPlan.Submitted");
		Should.Throw<BusinessException>(() => _codec.Serialize(Valid() with { IncludeStudentAccount = true }, staff));
		var closure = _catalogue.Get("Academic.Calendar.EmergencyClosureDeclared");
		var config = Valid() with { Timing = NotificationRuleTimingConfiguration.Default with { BypassQuietHours = true } };
		_codec.Deserialize(_codec.Serialize(config, closure), closure).Timing.BypassQuietHours.ShouldBeTrue();
	}
	public static IEnumerable<object[]> InvalidConfigurations()
	{
		yield return [Valid() with { SchemaVersion = 2 }];
		yield return [Valid() with { GuardianMode = (NotificationGuardianMode)42 }];
		yield return [Valid() with { AdditionalGuardianRelationships = ["mother"] }];
		yield return [Valid() with { GuardianMode = NotificationGuardianMode.PrimaryAndSelected, AdditionalGuardianRelationships = ["sibling"] }];
		yield return [Valid() with { GuardianMode = NotificationGuardianMode.PrimaryAndSelected, AdditionalGuardianRelationships = ["mother", "mother"] }];
		yield return [Valid() with { Channels = [] }];
		yield return [Valid() with { Channels = [CommunicationChannelType.Email] }];
		yield return [Valid() with { Channels = [CommunicationChannelType.InApp, CommunicationChannelType.Sms] }];
		yield return [Valid() with { Channels = [CommunicationChannelType.InApp, CommunicationChannelType.InApp] }];
		yield return [Valid() with { TemplateIds = [] }];
		yield return [Valid() with { TemplateIds = [Guid.Empty] }];
		var id = Guid.NewGuid(); yield return [Valid() with { TemplateIds = [id, id] }];
		yield return [Valid() with { FallbackLanguage = "en\r\n" }];
		yield return [Valid() with { FallbackLanguage = "en-abcdefgh-abcde" }];
		yield return [Valid() with { StaffAudienceSelectors = ["AllAdmins"] }];
		yield return [Valid() with { Timing = NotificationRuleTimingConfiguration.Default with { DelayMinutes = -1 } }];
		yield return [Valid() with { Timing = NotificationRuleTimingConfiguration.Default with { ReminderSchoolDaysBefore = 366 } }];
		yield return [Valid() with { Timing = NotificationRuleTimingConfiguration.Default with { QuietEnd = new(20, 0) } }];
		yield return [Valid() with { Timing = NotificationRuleTimingConfiguration.Default with { BypassQuietHours = true } }];
	}
	private static NotificationRuleRuntimeConfiguration Valid() => new(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
		[CommunicationChannelType.InApp, CommunicationChannelType.WebPush], [Guid.NewGuid()], NotificationRuleTimingConfiguration.Default, "en");
}
