using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;

namespace ShikshaMitra.Academic.Notifications;

internal static partial class AcademicNotificationDefinitions
{
	internal static IReadOnlyList<NotificationEventDefinition> Create() => Array.AsReadOnly(
		Curriculum().Concat(Timetable()).Concat(Calendar()).Concat(LearningTasks())
			.Concat(Examination()).Concat(Operations()).ToArray());

	private static NotificationEventDefinition Event(string key, string selectors,
		NotificationDeliveryProfile profile = NotificationDeliveryProfile.Routine,
		NotificationTriggerType trigger = NotificationTriggerType.Change,
		params string[] materialFields)
	{
		var family = key.Split('.')[1];
		var source = $"Academic.{family}";
		var channels = profile is NotificationDeliveryProfile.Internal or NotificationDeliveryProfile.Digest
			? new[] { CommunicationChannelType.InApp, CommunicationChannelType.WebPush, CommunicationChannelType.Email }
			: new[] { CommunicationChannelType.InApp, CommunicationChannelType.WebPush, CommunicationChannelType.Email, CommunicationChannelType.WhatsApp };
		return new(key, family, source, source, 1, trigger, profile,
			Array.AsReadOnly(selectors.Split(',').Distinct(StringComparer.Ordinal).ToArray()),
			Array.AsReadOnly(channels), Variables(family), Array.AsReadOnly(materialFields));
	}

	private const string Family = "PrimaryGuardian,AdditionalGuardians,StudentAccount";
	private const string Teacher = "AssignedTeacher";
	private const string Reviewer = "ConfiguredReviewer";
	private const string ClassTeacher = "ClassTeacher";
	private const string Invigilator = "Invigilator";
	private const NotificationDeliveryProfile StaffOnly = NotificationDeliveryProfile.Internal;
}
