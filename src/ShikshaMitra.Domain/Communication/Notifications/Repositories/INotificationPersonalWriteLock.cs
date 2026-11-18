using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Communication.Notifications;

public interface INotificationPersonalWriteLock
{
	Task AcquireAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken);
}
