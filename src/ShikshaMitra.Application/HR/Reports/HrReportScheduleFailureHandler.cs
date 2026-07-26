using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Audit;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Validation;

namespace ShikshaMitra.HR.Reports;

public class HrReportScheduleFailureHandler(
	IRepository<HrReportSchedule, Guid> scheduleRepository,
	HrAuditWriter auditWriter,
	ILogger<HrReportScheduleFailureHandler> logger) : ITransientDependency
{
	private static readonly TimeSpan TransientRetryDelay = TimeSpan.FromMinutes(15);

	public async Task HandleAsync(
		HrReportSchedule schedule,
		Exception exception,
		DateTime failedAt)
	{
		var permanent = IsPermanent(exception);
		if (permanent)
		{
			schedule.Pause();
		}
		else
		{
			schedule.DeferRetry(failedAt.Add(TransientRetryDelay));
		}

		try
		{
			await scheduleRepository.UpdateAsync(schedule, autoSave: true);
		}
		catch (Exception persistenceException)
		{
			logger.LogError(
				persistenceException,
				"Failed to persist failure state for HR report schedule {ScheduleId}",
				schedule.Id);
			return;
		}

		logger.LogWarning(
			exception,
			"HR report schedule {ScheduleId} failed. Permanent={Permanent}",
			schedule.Id,
			permanent);
		await WriteFailureAuditBestEffortAsync(schedule, exception, permanent);
	}

	private async Task WriteFailureAuditBestEffortAsync(
		HrReportSchedule schedule,
		Exception exception,
		bool permanent)
	{
		try
		{
			await auditWriter.WriteAsync(
				null,
				"hr_report_schedule",
				schedule.Id,
				"HR Report Schedule",
				permanent ? "schedule_auto_run_paused" : "schedule_auto_run_deferred",
				permanent
					? $"Paused HR report schedule {schedule.Name} after a permanent run failure"
					: $"Deferred HR report schedule {schedule.Name} after a transient run failure",
				context: new
				{
					schedule.Name,
					FailureType = exception.GetType().Name,
					ErrorCode = (exception as BusinessException)?.Code,
					schedule.Status,
					schedule.NextRunAt
				});
		}
		catch (Exception auditException)
		{
			logger.LogError(
				auditException,
				"Failed to audit failure state for HR report schedule {ScheduleId}",
				schedule.Id);
		}
	}

	private static bool IsPermanent(Exception exception) =>
		exception is BusinessException or
			AbpAuthorizationException or
			AbpValidationException or
			JsonException or
			FormatException;
}
