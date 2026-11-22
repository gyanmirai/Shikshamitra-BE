using System;
using System.Text.Json.Nodes;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public sealed class AcademicProgressMilestoneConfiguration_Tests
{
	[Theory]
	[InlineData(1)]
	[InlineData(75)]
	[InlineData(100)]
	public void An_enabled_milestone_persists_its_configured_percentage(int percentage)
	{
		var catalogue = new AcademicNotificationCatalogue([]);
		var codec = new NotificationRuleConfigurationCodec();
		var behind = catalogue.Get(AcademicNotificationEventKeys.ProgressBehindSchedule);
		var milestone = catalogue.Get(AcademicNotificationEventKeys.ProgressMilestoneReached);
		var configuration = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
			["AssignedTeacher"], [CommunicationChannelType.InApp], [Guid.NewGuid()], NotificationRuleTimingConfiguration.Default, "en");
		var document = JsonNode.Parse(codec.Serialize(configuration, behind))!.AsObject();
		var timing = document["timing"]!.AsObject();
		timing["progressMilestonePercentage"] = percentage;
		var decoded = codec.Deserialize(document.ToJsonString(), milestone);
		var stored = JsonNode.Parse(codec.Serialize(decoded, milestone))!;
		stored["timing"]!["progressMilestonePercentage"]!.GetValue<int>().ShouldBe(percentage);

		Should.Throw<BusinessException>(() => codec.Deserialize(document.ToJsonString(), behind))
			.Code.ShouldBe(NotificationErrorCodes.RuleConfigurationInvalid);
		timing["progressMilestonePercentage"] = 0;
		Should.Throw<BusinessException>(() => codec.Deserialize(document.ToJsonString(), milestone))
			.Code.ShouldBe(NotificationErrorCodes.RuleConfigurationInvalid);
		timing["progressMilestonePercentage"] = 101;
		Should.Throw<BusinessException>(() => codec.Deserialize(document.ToJsonString(), milestone))
			.Code.ShouldBe(NotificationErrorCodes.RuleConfigurationInvalid);
		timing.Remove("progressMilestonePercentage");
		Should.Throw<BusinessException>(() => codec.Deserialize(document.ToJsonString(), milestone))
			.Code.ShouldBe(NotificationErrorCodes.RuleConfigurationInvalid);
	}
}
