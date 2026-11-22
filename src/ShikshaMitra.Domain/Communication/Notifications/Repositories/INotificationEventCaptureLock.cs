using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Communication.Notifications;

public interface INotificationEventCaptureLock
{
	Task AcquireAsync(string eventKeyHash, CancellationToken cancellationToken);
}
