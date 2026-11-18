using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Notifications;

public interface IAcademicNotificationSourceAuthorizer
{
	IReadOnlyCollection<string> EventKeys { get; }
	Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken cancellationToken);
	Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken cancellationToken);
}
