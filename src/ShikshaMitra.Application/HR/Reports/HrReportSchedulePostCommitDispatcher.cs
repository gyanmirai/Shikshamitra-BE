using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Reports;

public class HrReportSchedulePostCommitDispatcher(
	IUnitOfWorkManager unitOfWorkManager,
	ILogger<HrReportSchedulePostCommitDispatcher> logger) : ITransientDependency
{
	public Task DispatchAsync(Func<Task> delivery, Guid scheduleId)
	{
		var currentUnitOfWork = unitOfWorkManager.Current;
		if (currentUnitOfWork == null)
		{
			return ExecuteSafelyAsync(delivery, scheduleId);
		}

		currentUnitOfWork.OnCompleted(() => ExecuteSafelyAsync(delivery, scheduleId));
		return Task.CompletedTask;
	}

	private async Task ExecuteSafelyAsync(Func<Task> delivery, Guid scheduleId)
	{
		try
		{
			await delivery();
		}
		catch (Exception exception)
		{
			logger.LogWarning(
				exception,
				"HR report schedule {ScheduleId} committed, but its best-effort email delivery failed.",
				scheduleId);
		}
	}
}
