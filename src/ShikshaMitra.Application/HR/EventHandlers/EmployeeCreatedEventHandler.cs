using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Events;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace ShikshaMitra.HR.EventHandlers;

/// <summary>
/// Handles employee creation events for cross-cutting concerns
/// (e.g., audit logging, notification to admins, onboarding workflows).
/// </summary>
public class EmployeeCreatedEventHandler(
	ILogger<EmployeeCreatedEventHandler> logger)
	: IDistributedEventHandler<EmployeeCreatedEto>, ITransientDependency
{
	public Task HandleEventAsync(EmployeeCreatedEto eventData)
	{
		logger.LogInformation(
			"Employee created event received: EmployeeId={EmployeeId}, Code={Code}, Tenant={TenantId}",
			eventData.EmployeeId, eventData.EmployeeCode, eventData.TenantId);

		return Task.CompletedTask;
	}
}
