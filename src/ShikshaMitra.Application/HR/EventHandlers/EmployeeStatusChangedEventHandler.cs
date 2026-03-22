using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Events;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace ShikshaMitra.HR.EventHandlers;

/// <summary>
/// Handles employee status change events (e.g., audit trail, notifications).
/// </summary>
public class EmployeeStatusChangedEventHandler(
	ILogger<EmployeeStatusChangedEventHandler> logger)
	: IDistributedEventHandler<EmployeeStatusChangedEto>, ITransientDependency
{
	public Task HandleEventAsync(EmployeeStatusChangedEto eventData)
	{
		logger.LogInformation(
			"Employee status changed: EmployeeId={EmployeeId}, {Previous} -> {Current}",
			eventData.EmployeeId, eventData.PreviousStatus, eventData.CurrentStatus);

		return Task.CompletedTask;
	}
}
