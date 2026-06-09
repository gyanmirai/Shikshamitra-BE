using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Reports;

public class HrReportScheduleRunner(
	IRepository<HrReportSchedule, Guid> scheduleRepository,
	HrReportExportService exportService,
	HrReportScheduleDeliveryService deliveryService,
	HrAuditWriter auditWriter) : ApplicationService, ITransientDependency
{
	public async Task<HrReportExportCreateResponseDto> RunScheduleAsync(HrReportSchedule schedule, string auditAction)
	{
		var request = HrReportScheduleMapper.DeserializeDefinition(schedule);
		request.Format = string.IsNullOrWhiteSpace(request.Format) ? HrReportFormat.Excel : request.Format;
		var recipients = schedule.GetRecipientUserIds();
		var export = await exportService.CreateExportForUserAsync(
			request,
			schedule.OwnerUserId,
			schedule.TenantId,
			recipients);
		var nextRunAt = HrReportScheduleMapper.ComputeNextRun(schedule.Frequency, Clock.Now);
		schedule.MarkRun(Clock.Now, nextRunAt);
		await scheduleRepository.UpdateAsync(schedule, autoSave: true);
		await auditWriter.WriteAsync(
			null,
			"hr_report_schedule",
			schedule.Id,
			"HR Report Schedule",
			auditAction,
			$"Ran HR report schedule {schedule.Name}",
			context: new
			{
				schedule.Name,
				export.JobId,
				schedule.DeliveryMode,
				RecipientUserIds = recipients,
				request.DataSource,
				request.Columns
			});

		if (schedule.DeliveryMode == HrReportDeliveryMode.InAppAndEmail)
		{
			await deliveryService.SendDeliveryEmailsAsync(schedule, export.JobId, recipients);
		}

		return export;
	}
}
