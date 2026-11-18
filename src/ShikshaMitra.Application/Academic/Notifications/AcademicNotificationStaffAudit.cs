using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.Auditing;
using Volo.Abp.AuditLogging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;

namespace ShikshaMitra.Academic.Notifications;

public sealed class AcademicNotificationStaffAudit(IAuditLogInfoToAuditLogConverter converter,
	IAuditLogRepository repository, IClock clock) : ITransientDependency
{
	internal Task WriteAsync(Guid tenantId, Guid actorId, string action, Guid operationId, Guid? dispatchId,
		string eventKey, string reasonCode, int? cancelledIntents, int? cancelledDispatches, CancellationToken cancellationToken) =>
		WriteEntryAsync(tenantId, actorId, "POST", action, new { operationId, dispatchId, eventKey, reasonCode, cancelledIntents, cancelledDispatches },
			cancellationToken);

	internal Task WriteRuleAsync(Guid tenantId, Guid actorId, NotificationRule rule, CancellationToken cancellationToken) =>
		WriteEntryAsync(tenantId, actorId, "PUT", "SaveRule",
			new { ruleId = rule.Id, eventKey = rule.EventKey, enabled = rule.IsEnabled, revision = rule.Revision }, cancellationToken);

	internal Task WriteTemplateAsync(Guid tenantId, Guid actorId, NotificationTemplateDto template, CancellationToken cancellationToken) =>
		WriteEntryAsync(tenantId, actorId, "POST", "PublishTemplate", new { templateId = template.TemplateId, revisionId = template.RevisionId,
			eventKey = template.EventKey, revision = template.Revision, channel = template.Channel, languageCode = template.LanguageCode },
			cancellationToken);

	private async Task WriteEntryAsync<T>(Guid tenantId, Guid actorId, string httpMethod, string action, T metadata,
		CancellationToken cancellationToken)
	{
		var time = clock.Now.ToUniversalTime();
		var entry = new AuditLogInfo { TenantId = tenantId, UserId = actorId, ExecutionTime = time, HttpMethod = httpMethod };
		entry.Actions.Add(new AuditLogActionInfo {
			ServiceName = "AcademicNotifications", MethodName = action, ExecutionTime = time,
			Parameters = JsonSerializer.Serialize(metadata)
		});
		var row = await converter.ConvertAsync(entry);
		await repository.InsertAsync(row, cancellationToken: cancellationToken);
	}
}
