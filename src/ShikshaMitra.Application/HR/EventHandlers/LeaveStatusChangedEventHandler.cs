using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Events;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace ShikshaMitra.HR.EventHandlers;

/// <summary>
/// Handles leave status change events (e.g., email notification to employee).
/// </summary>
public class LeaveStatusChangedEventHandler(
	ILogger<LeaveStatusChangedEventHandler> logger)
	: IDistributedEventHandler<LeaveStatusChangedEto>, ITransientDependency
{
	public Task HandleEventAsync(LeaveStatusChangedEto eventData)
	{
		logger.LogInformation(
			"Leave status changed: LeaveId={LeaveId}, Employee={EmployeeId}, {Previous} -> {New}",
			eventData.LeaveId, eventData.EmployeeId, eventData.PreviousStatus, eventData.NewStatus);

		return Task.CompletedTask;
	}
}
