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
			"Staff status changed: EmployeeId={EmployeeId}, {Previous} -> {Current}, ChangeReason={ChangeReason}",
			eventData.EmployeeId, eventData.PreviousStatus, eventData.CurrentStatus, eventData.ChangeReason);

		return Task.CompletedTask;
	}
}
