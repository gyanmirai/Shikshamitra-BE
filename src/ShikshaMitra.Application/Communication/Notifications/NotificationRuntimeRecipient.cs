using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Communication.Notifications;

/// <summary>Internal delivery data. Public previews expose only eligibility.</summary>
public sealed class NotificationRuntimeRecipient(NotificationRecipientRefDto sourceRecipient,
	NotificationRecipientRefDto recipient, CommunicationChannelType channel, string? exclusionCode,
	string? associationRevision = null, string? destination = null, string? recipientName = null, string? studentName = null)
{
	public NotificationRecipientRefDto SourceRecipient { get; } = sourceRecipient;
	public NotificationRecipientRefDto Recipient { get; } = recipient;
	public CommunicationChannelType Channel { get; } = channel;
	public string? ExclusionCode { get; } = exclusionCode;
	public string? AssociationRevision { get; } = associationRevision;
	public bool IsEligible => ExclusionCode == null;
	public string? Destination { get; } = exclusionCode == null ? destination : null;
	public string? RecipientName { get; } = exclusionCode == null ? recipientName : null;
	public string? StudentName { get; } = exclusionCode == null ? studentName : null;
}

public interface INotificationRuntimeRecipientResolver
{
	bool Supports(string eventKey);
	Task<IReadOnlyList<NotificationRuntimeRecipient>> ResolveAsync(NotificationCurrentSource source, CancellationToken cancellationToken);
}
