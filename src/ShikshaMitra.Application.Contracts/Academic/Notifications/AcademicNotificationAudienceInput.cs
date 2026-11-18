using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;

namespace ShikshaMitra.Academic.Notifications;

public sealed record AcademicNotificationRecipientPolicy(
	NotificationGuardianMode GuardianMode,
	IReadOnlyList<string> AdditionalGuardianRelationships,
	bool IncludeStudentAccount = false);

public sealed record AcademicNotificationAudienceInput(
	string EventKey, string SourceType, Guid SourceId,
	AcademicNotificationRecipientPolicy Policy,
	IReadOnlyList<CommunicationChannelType> Channels,
	IReadOnlyList<NotificationRecipientRefDto> AffectedRecipients);

public interface IAcademicNotificationRecipientResolver
{
	Task<NotificationAudiencePreviewDto> ResolveAsync(AcademicNotificationAudienceInput input,
		CancellationToken cancellationToken);
}
