using System.Collections.Generic;

namespace ShikshaMitra.Communication.Notifications;

internal sealed record NotificationPreferenceConfiguration(IReadOnlyList<CommunicationChannelType> EnabledChannels,
	bool QuietHoursEnabled, string? QuietHoursStart, string? QuietHoursEnd);
