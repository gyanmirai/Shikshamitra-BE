using System;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Communication.Notifications;

public interface INotificationTemplateAccessPolicy
{
	bool SupportsEvent(string eventKey);
	Task EnsureAsync(string eventKey, Guid? academicMediumId, NotificationTemplateAction action, CancellationToken cancellationToken);
}
