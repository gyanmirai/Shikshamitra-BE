using System.Collections.Generic;

namespace ShikshaMitra.Communication.Notifications;

public interface INotificationEventDefinitionProvider
{
	IReadOnlyList<NotificationEventDefinition> GetAll();
}
