using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Reports;

public class HrReportScheduleBatchProcessor(
	IRepository<HrReportSchedule, Guid> scheduleRepository,
	IDataFilter<IMultiTenant> multiTenantFilter,
	IAsyncQueryableExecuter asyncExecuter,
	ICurrentTenant currentTenant,
	IUnitOfWorkManager unitOfWorkManager,
	HrReportScheduleRunner scheduleRunner,
	HrReportScheduleFailureHandler failureHandler,
	ILogger<HrReportScheduleBatchProcessor> logger) : ITransientDependency
{
	private const int MaxDueSchedulesPerRun = 25;

	public async Task<int> ProcessAsync(DateTime dueAt)
	{
		var scheduleReferences = await GetDueScheduleReferencesAsync(dueAt);
		var processed = 0;
		foreach (var scheduleReference in scheduleReferences)
		{
			using (currentTenant.Change(scheduleReference.TenantId))
			{
				try
				{
					using var executionUow = unitOfWorkManager.Begin(
						requiresNew: true,
						isTransactional: true);
					var schedule = await scheduleRepository.FindAsync(scheduleReference.Id);
					if (!IsStillDue(schedule, dueAt))
					{
						await executionUow.CompleteAsync();
						continue;
					}

					await scheduleRunner.RunScheduleAsync(
						schedule!,
						"schedule_auto_run",
						advanceNextRun: true,
						runAt: dueAt);
					await executionUow.CompleteAsync();
					processed++;
				}
				catch (Exception exception)
				{
					await HandleFailureAsync(scheduleReference.Id, exception, dueAt);
				}
			}
		}

		return processed;
	}

	private async Task<List<ScheduleReference>> GetDueScheduleReferencesAsync(DateTime dueAt)
	{
		using var queryUow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
		using var filter = multiTenantFilter.Disable();
		var query = await scheduleRepository.GetQueryableAsync();
		var schedules = await asyncExecuter.ToListAsync(
			query
				.Where(schedule =>
					schedule.Status == HrReportScheduleStatus.Active &&
					schedule.NextRunAt <= dueAt)
				.OrderBy(schedule => schedule.NextRunAt)
				.Take(MaxDueSchedulesPerRun));
		var references = schedules
			.Select(schedule => new ScheduleReference(schedule.Id, schedule.TenantId))
			.ToList();
		await queryUow.CompleteAsync();
		return references;
	}

	private async Task HandleFailureAsync(Guid scheduleId, Exception exception, DateTime failedAt)
	{
		try
		{
			using var failureUow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
			var schedule = await scheduleRepository.FindAsync(scheduleId);
			if (IsStillDue(schedule, failedAt))
			{
				await failureHandler.HandleAsync(schedule!, exception, failedAt);
			}
			await failureUow.CompleteAsync();
		}
		catch (Exception failureStateException)
		{
			logger.LogError(
				failureStateException,
				"Failed to persist isolated failure state for HR report schedule {ScheduleId}",
				scheduleId);
		}
	}

	private static bool IsStillDue(HrReportSchedule? schedule, DateTime dueAt) =>
		schedule != null &&
		schedule.Status == HrReportScheduleStatus.Active &&
		schedule.NextRunAt <= dueAt;

	private sealed record ScheduleReference(Guid Id, Guid? TenantId);
}
