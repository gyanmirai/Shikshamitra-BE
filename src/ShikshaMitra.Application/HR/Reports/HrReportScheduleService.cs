using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using Volo.Abp.Data;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Reports;

public class HrReportScheduleService(
	IRepository<HrReportSchedule, Guid> scheduleRepository,
	IDataFilter<IMultiTenant> multiTenantFilter,
	IGuidGenerator guidGenerator,
	HrReportScheduleRecipientValidator recipientValidator,
	HrReportScheduleRunner scheduleRunner,
	HrAuditWriter auditWriter) : ApplicationService, ITransientDependency
{
	private const int MaxDueSchedulesPerRun = 25;

	public async Task<HrReportScheduleListDto> GetSchedulesAsync()
	{
		var userId = GetRequiredUserId();
		var schedules = await scheduleRepository.GetListAsync(x => x.OwnerUserId == userId);
		return new HrReportScheduleListDto
		{
			Items = schedules
				.OrderBy(x => x.Status)
				.ThenBy(x => x.NextRunAt)
				.Select(HrReportScheduleMapper.ToDto)
				.ToList()
		};
	}

	public async Task<HrReportScheduleDto> CreateScheduleAsync(HrReportScheduleRequestDto input)
	{
		var userId = GetRequiredUserId();
		var nextRunAt = input.FirstRunAt ?? HrReportScheduleMapper.ComputeNextRun(input.Frequency, Clock.Now);
		var schedule = new HrReportSchedule(
			guidGenerator.Create(),
			CurrentTenant.Id,
			userId,
			input.Name,
			input.TemplateId,
			HrReportScheduleMapper.NormalizeFrequency(input.Frequency),
			HrReportScheduleMapper.NormalizeDeliveryMode(input.DeliveryMode),
			await recipientValidator.NormalizeRecipientsAsync(input.RecipientUserIds, userId),
			JsonSerializer.Serialize(input.Definition, HrReportScheduleMapper.JsonOptions),
			nextRunAt);

		await scheduleRepository.InsertAsync(schedule, autoSave: true);
		await WriteAuditAsync(schedule.Id, "schedule_created", $"Created HR report schedule {schedule.Name}", new { schedule.Name, schedule.Frequency, schedule.DeliveryMode });
		return HrReportScheduleMapper.ToDto(schedule);
	}

	public async Task<HrReportScheduleDto> UpdateScheduleAsync(Guid id, HrReportScheduleRequestDto input)
	{
		var schedule = await GetOwnedScheduleAsync(id);
		var nextRunAt = input.FirstRunAt ?? schedule.NextRunAt;
		schedule.Update(
			input.Name,
			input.TemplateId,
			HrReportScheduleMapper.NormalizeFrequency(input.Frequency),
			HrReportScheduleMapper.NormalizeDeliveryMode(input.DeliveryMode),
			await recipientValidator.NormalizeRecipientsAsync(input.RecipientUserIds, schedule.OwnerUserId),
			JsonSerializer.Serialize(input.Definition, HrReportScheduleMapper.JsonOptions),
			nextRunAt);
		await scheduleRepository.UpdateAsync(schedule, autoSave: true);
		await WriteAuditAsync(schedule.Id, "schedule_updated", $"Updated HR report schedule {schedule.Name}", new { schedule.Name, schedule.Frequency, schedule.DeliveryMode });
		return HrReportScheduleMapper.ToDto(schedule);
	}

	public async Task<HrReportScheduleDto> PauseScheduleAsync(Guid id)
	{
		var schedule = await GetOwnedScheduleAsync(id);
		schedule.Pause();
		await scheduleRepository.UpdateAsync(schedule, autoSave: true);
		await WriteAuditAsync(schedule.Id, "schedule_paused", $"Paused HR report schedule {schedule.Name}", new { schedule.Name });
		return HrReportScheduleMapper.ToDto(schedule);
	}

	public async Task<HrReportScheduleDto> ResumeScheduleAsync(Guid id)
	{
		var schedule = await GetOwnedScheduleAsync(id);
		schedule.Resume(HrReportScheduleMapper.ComputeNextRun(schedule.Frequency, Clock.Now));
		await scheduleRepository.UpdateAsync(schedule, autoSave: true);
		await WriteAuditAsync(schedule.Id, "schedule_resumed", $"Resumed HR report schedule {schedule.Name}", new { schedule.Name, schedule.NextRunAt });
		return HrReportScheduleMapper.ToDto(schedule);
	}

	public async Task<HrReportScheduleRunResponseDto> RunScheduleNowAsync(Guid id)
	{
		var schedule = await GetOwnedScheduleAsync(id);
		var export = await scheduleRunner.RunScheduleAsync(schedule, "schedule_run_now");
		return new HrReportScheduleRunResponseDto
		{
			ScheduleId = schedule.Id,
			JobId = export.JobId,
			NextRunAt = schedule.NextRunAt
		};
	}

	public async Task<int> ProcessDueSchedulesAsync(DateTime? dueAt = null)
	{
		var now = dueAt ?? Clock.Now;
		List<HrReportSchedule> dueSchedules;
		using (multiTenantFilter.Disable())
		{
			var query = await scheduleRepository.GetQueryableAsync();
			dueSchedules = await AsyncExecuter.ToListAsync(
				query
					.Where(x => x.Status == HrReportScheduleStatus.Active && x.NextRunAt <= now)
					.OrderBy(x => x.NextRunAt)
					.Take(MaxDueSchedulesPerRun));
		}

		var processed = 0;
		foreach (var schedule in dueSchedules)
		{
			using (CurrentTenant.Change(schedule.TenantId))
			{
				await scheduleRunner.RunScheduleAsync(schedule, "schedule_auto_run");
				processed++;
			}
		}

		return processed;
	}

	public async Task DeleteScheduleAsync(Guid id)
	{
		var schedule = await GetOwnedScheduleAsync(id);
		await scheduleRepository.DeleteAsync(schedule, autoSave: true);
		await WriteAuditAsync(schedule.Id, "schedule_deleted", $"Deleted HR report schedule {schedule.Name}", new { schedule.Name });
	}

	private async Task<HrReportSchedule> GetOwnedScheduleAsync(Guid id)
	{
		var userId = GetRequiredUserId();
		var schedule = await scheduleRepository.GetAsync(id);
		if (schedule.OwnerUserId != userId)
		{
			throw new BusinessException(HRErrorCodes.ReportAccessDenied);
		}

		return schedule;
	}

	private Guid GetRequiredUserId() =>
		CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.ReportUserRequired);

	private Task WriteAuditAsync(Guid entityId, string action, string summary, object? context) =>
		auditWriter.WriteAsync(
			null,
			"hr_report_schedule",
			entityId,
			"HR Report Schedule",
			action,
			summary,
			context: context);
}
