using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Auditing;
using Volo.Abp.AuditLogging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;

namespace ShikshaMitra.Academic.Notifications;

public sealed class AcademicNotificationStaffAudit(IAuditLogInfoToAuditLogConverter converter,
	IAuditLogRepository repository, IClock clock) : ITransientDependency
{
	internal async Task WriteAsync(Guid tenantId, Guid actorId, string action, Guid operationId, Guid? dispatchId,
		string eventKey, string reasonCode, int? cancelledIntents, int? cancelledDispatches, CancellationToken cancellationToken)
	{
		var time = clock.Now.ToUniversalTime();
		var entry = new AuditLogInfo { TenantId = tenantId, UserId = actorId, ExecutionTime = time, HttpMethod = "POST" };
		entry.Actions.Add(new AuditLogActionInfo {
			ServiceName = "AcademicNotifications", MethodName = action, ExecutionTime = time,
			Parameters = JsonSerializer.Serialize(new { operationId, dispatchId, eventKey, reasonCode, cancelledIntents, cancelledDispatches })
		});
		var row = await converter.ConvertAsync(entry);
		await repository.InsertAsync(row, cancellationToken: cancellationToken);
	}
}
