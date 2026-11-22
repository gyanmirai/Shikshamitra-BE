using System.Linq;

namespace ShikshaMitra.Communication.Notifications;

/// <summary>Source choices can narrow the catalogue and captured rule for this release.</summary>
internal static class NotificationSourceDeliveryPolicy
{
	internal static NotificationEventDefinition Restrict(NotificationEventDefinition definition, NotificationSourceSnapshot source)
	{
		if (source.DeliveryLimits is not { } limits) return definition;
		return definition with
		{
			RecipientSelectors = definition.RecipientSelectors.Intersect(limits.RecipientSelectors).ToArray(),
			SupportedChannels = definition.SupportedChannels.Intersect(limits.Channels).ToArray()
		};
	}

	internal static NotificationRuleRuntimeConfiguration Restrict(NotificationRuleRuntimeConfiguration configuration,
		NotificationEventDefinition definition, NotificationSourceSnapshot source)
	{
		if (source.DeliveryLimits == null) return configuration;
		var selectors = definition.RecipientSelectors;
		var allowsAdditional = selectors.Contains("PrimaryGuardian") && selectors.Contains("AdditionalGuardians");
		return configuration with
		{
			GuardianMode = allowsAdditional ? configuration.GuardianMode : NotificationGuardianMode.PrimaryOnly,
			AdditionalGuardianRelationships = allowsAdditional ? configuration.AdditionalGuardianRelationships : [],
			IncludeStudentAccount = configuration.IncludeStudentAccount && selectors.Contains("StudentAccount"),
			StaffAudienceSelectors = configuration.StaffAudienceSelectors.Intersect(selectors).ToArray(),
			Channels = configuration.Channels.Intersect(definition.SupportedChannels).ToArray()
		};
	}

	internal static bool Includes(NotificationRecipientRefDto recipient, NotificationRuleRuntimeConfiguration configuration,
		NotificationEventDefinition definition) => recipient.Kind == "Student"
		? definition.RecipientSelectors.Contains("PrimaryGuardian") || configuration.IncludeStudentAccount
		: configuration.StaffAudienceSelectors.Contains(recipient.Kind);
}
