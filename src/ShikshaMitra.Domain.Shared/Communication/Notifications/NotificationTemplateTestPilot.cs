using System;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationTemplateTestPilot(Guid TenantId, Guid OperationId, Guid RecipientUserId,
	CommunicationChannelType Channel = CommunicationChannelType.InApp)
{
	public bool HasSupportedChannel => Channel is CommunicationChannelType.InApp or CommunicationChannelType.Email
		or CommunicationChannelType.WebPush;
}
