using System.Collections.Generic;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;

namespace ShikshaMitra.Academic.Notifications;

public sealed record NotificationRecipientEligibilityDto(NotificationRecipientRefDto Recipient,
	CommunicationChannelType Channel, bool IsEligible, string? ExclusionCode);

public sealed record NotificationAudiencePreviewDto(
	int EligibleRecipientCount, int ExcludedRecipientCount,
	IReadOnlyList<NotificationRecipientEligibilityDto> Recipients,
	IReadOnlyList<string> BlockingIssues);
