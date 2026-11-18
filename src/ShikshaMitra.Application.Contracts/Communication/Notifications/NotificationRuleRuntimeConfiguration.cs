using System;
using System.Collections.Generic;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationRuleTimingConfiguration(int DelayMinutes, int ReminderSchoolDaysBefore,
	TimeOnly ReminderLocalTime, bool QuietHoursEnabled, TimeOnly QuietStart, TimeOnly QuietEnd, bool BypassQuietHours)
{
	public static NotificationRuleTimingConfiguration Default => new(0, 1, new(17, 0), true, new(20, 0), new(7, 0), false);
}

public sealed record NotificationRuleRuntimeConfiguration(int SchemaVersion, NotificationGuardianMode GuardianMode,
	IReadOnlyList<string> AdditionalGuardianRelationships, bool IncludeStudentAccount,
	IReadOnlyList<string> StaffAudienceSelectors, IReadOnlyList<CommunicationChannelType> Channels,
	IReadOnlyList<Guid> TemplateIds, NotificationRuleTimingConfiguration Timing, string FallbackLanguage);
