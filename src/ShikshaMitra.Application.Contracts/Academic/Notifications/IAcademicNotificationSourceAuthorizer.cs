using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Notifications;

public interface IAcademicNotificationSourceAuthorizer
{
	IReadOnlyCollection<string> EventKeys { get; }
	Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken cancellationToken);
}
